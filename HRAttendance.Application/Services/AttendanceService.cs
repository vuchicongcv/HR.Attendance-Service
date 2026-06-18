using HRPayroll.Application.Events;
using HRAttendance.Application.DTOs;
using HRAttendance.Application.Interfaces;
using HRAttendance.Domain.Entities;
using HRAttendance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRAttendance.Application.Services;

public class AttendanceService
{
    private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();
    private readonly IAttendanceDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IEventPublisher _eventPublisher;

    public AttendanceService(
        IAttendanceDbContext context,
        ICurrentUser currentUser,
        IDateTimeProvider clock,
        IEventPublisher eventPublisher)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
        _eventPublisher = eventPublisher;
    }

    public async Task<AttendanceRecordDto> CheckInAsync(CancellationToken cancellationToken = default)
    {
        var employee = await GetActiveEmployeeAsync(RequireEmployeeId(), cancellationToken);
        return await CheckInForEmployeeAsync(employee, cancellationToken);
    }

    // Core check-in cho 1 nhân viên cụ thể — dùng chung cho self check-in và kiosk (theo mã NV).
    private async Task<AttendanceRecordDto> CheckInForEmployeeAsync(EmployeeReference employee, CancellationToken cancellationToken)
    {
        var nowUtc = NormalizeUtc(_clock.UtcNow);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, VietnamTimeZone);
        var workDate = DateOnly.FromDateTime(localNow);

        var existing = await _context.AttendanceRecords
            .FirstOrDefaultAsync(x => x.EmployeeId == employee.EmployeeId && x.WorkDate == workDate, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException("Nhân viên đã check-in trong ngày này.");

        var shift = await GetDefaultShiftAsync(cancellationToken);
        var record = new AttendanceRecord
        {
            EmployeeId = employee.EmployeeId,
            WorkDate = workDate,
            ShiftId = shift?.Id,
            CheckInTime = nowUtc,
            LateMinutes = shift == null ? 0 : CalculateLateMinutes(workDate, shift, localNow),
            Status = AttendanceStatus.Present
        };

        _context.AttendanceRecords.Add(record);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(record);
    }

    public async Task<AttendanceRecordDto> CheckOutAsync(CancellationToken cancellationToken = default)
    {
        var employeeId = RequireEmployeeId();
        await GetActiveEmployeeAsync(employeeId, cancellationToken);
        return await CheckOutForEmployeeAsync(employeeId, cancellationToken);
    }

    // Core check-out cho 1 nhân viên cụ thể — dùng chung cho self check-out và kiosk.
    private async Task<AttendanceRecordDto> CheckOutForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var nowUtc = NormalizeUtc(_clock.UtcNow);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, VietnamTimeZone);
        var workDate = DateOnly.FromDateTime(localNow);

        var record = await _context.AttendanceRecords
            .Include(x => x.Shift)
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.WorkDate == workDate, cancellationToken);
        if (record == null)
        {
            var previousDate = workDate.AddDays(-1);
            record = await _context.AttendanceRecords
                .Include(x => x.Shift)
                .FirstOrDefaultAsync(
                    x => x.EmployeeId == employeeId
                        && x.WorkDate == previousDate
                        && x.CheckInTime != null
                        && x.CheckOutTime == null,
                    cancellationToken);
        }
        if (record == null || record.CheckInTime == null)
            throw new InvalidOperationException("Chưa có bản ghi check-in để check-out.");
        if (record.CheckOutTime != null)
            throw new InvalidOperationException("Nhân viên đã check-out trong ngày này.");

        var shift = record.Shift;
        if (shift == null && record.ShiftId.HasValue)
            shift = await _context.Shifts.FirstOrDefaultAsync(x => x.Id == record.ShiftId.Value, cancellationToken);

        record.CheckOutTime = nowUtc;
        ApplyWorkedTime(record, shift);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(record);
    }

    public async Task<List<AttendanceRecordDto>> GetMineAsync(int month, int year, CancellationToken cancellationToken = default)
    {
        var employeeId = RequireEmployeeId();
        return await QueryByEmployeeAndMonth(employeeId, month, year)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AttendanceRecordDto>> GetAsync(
        Guid? employeeId,
        int month,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsInRole("Manager") && employeeId.HasValue)
            await EnsureManagerCanAccessEmployeeAsync(employeeId.Value, cancellationToken);

        var query = _context.AttendanceRecords.AsNoTracking()
            .Where(x => x.WorkDate.Month == month && x.WorkDate.Year == year);

        if (employeeId.HasValue)
        {
            query = query.Where(x => x.EmployeeId == employeeId.Value);
        }
        else if (_currentUser.IsInRole("Manager"))
        {
            // Manager không truyền employeeId → thấy nhân viên thuộc phòng mình quản lý + TẤT CẢ team con
            // (Admin/HR không vào nhánh này nên vẫn xem được toàn bộ).
            var managedEmployeeIds = await GetManagedEmployeeIdsAsync(cancellationToken);
            query = query.Where(x => managedEmployeeIds.Contains(x.EmployeeId));
        }

        return await query.OrderBy(x => x.WorkDate).Select(x => ToDto(x)).ToListAsync(cancellationToken);
    }

    public async Task<List<AttendanceRecordDto>> GetByDepartmentAsync(
        Guid departmentId,
        int month,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsInRole("Manager"))
            await EnsureManagerOwnsDepartmentAsync(departmentId, cancellationToken);

        var employeeIds = await _context.EmployeeReferences
            .Where(x => x.DepartmentId == departmentId)
            .Select(x => x.EmployeeId)
            .ToListAsync(cancellationToken);

        return await _context.AttendanceRecords.AsNoTracking()
            .Where(x => employeeIds.Contains(x.EmployeeId) && x.WorkDate.Month == month && x.WorkDate.Year == year)
            .OrderBy(x => x.WorkDate)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<AttendanceRecordDto> UpsertManualAsync(
        ManualAttendanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole("Admin", "HR"))
            throw new UnauthorizedAccessException("Chỉ Admin/HR được nhập hoặc sửa chấm công thủ công.");

        await GetActiveEmployeeAsync(request.EmployeeId, cancellationToken);
        var shift = request.ShiftId.HasValue
            ? await _context.Shifts.FirstOrDefaultAsync(x => x.Id == request.ShiftId.Value, cancellationToken)
            : await GetDefaultShiftAsync(cancellationToken);
        if (request.ShiftId.HasValue && shift == null)
            throw new InvalidOperationException("Ca làm việc không tồn tại.");

        var record = await _context.AttendanceRecords
            .FirstOrDefaultAsync(x => x.EmployeeId == request.EmployeeId && x.WorkDate == request.WorkDate, cancellationToken);
        if (record == null)
        {
            record = new AttendanceRecord
            {
                EmployeeId = request.EmployeeId,
                WorkDate = request.WorkDate
            };
            _context.AttendanceRecords.Add(record);
        }

        record.ShiftId = shift?.Id;
        record.CheckInTime = request.CheckInTime.HasValue ? NormalizeUtc(request.CheckInTime.Value) : null;
        record.CheckOutTime = request.CheckOutTime.HasValue ? NormalizeUtc(request.CheckOutTime.Value) : null;
        record.Status = request.Status;
        record.Note = request.Note;
        ApplyWorkedTime(record, shift);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(record);
    }

    public async Task<List<MonthlyAttendanceSummaryDto>> CloseMonthAsync(
        int month,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole("Admin", "HR"))
            throw new UnauthorizedAccessException("Chỉ Admin/HR được chốt công tháng.");
        ValidateMonthYear(month, year);

        var employees = await _context.EmployeeReferences
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var standardWorkdays = CountWeekdays(month, year);
        var results = new List<MonthlyAttendanceSummary>();

        // Loại nghỉ CÓ lương (động) — phân biệt PaidLeaveDays / UnpaidLeaveDays khi chốt công.
        var paidLeaveTypes = await _context.LeavePolicies
            .Where(x => x.IsPaid)
            .Select(x => x.LeaveType)
            .ToListAsync(cancellationToken);

        foreach (var employee in employees)
        {
            var records = await QueryByEmployeeAndMonth(employee.EmployeeId, month, year)
                .ToListAsync(cancellationToken);
            var leaves = await _context.LeaveRequests
                .Where(x => x.EmployeeId == employee.EmployeeId
                    && x.Status == LeaveStatus.Approved
                    && x.FromDate.Month <= month
                    && x.ToDate.Month >= month
                    && x.FromDate.Year <= year
                    && x.ToDate.Year >= year)
                .ToListAsync(cancellationToken);

            var paidLeaveDays = leaves
                .Where(x => paidLeaveTypes.Contains(x.LeaveType))
                .Sum(x => CountOverlapWeekdays(x.FromDate, x.ToDate, month, year));
            var unpaidLeaveDays = leaves
                .Where(x => !paidLeaveTypes.Contains(x.LeaveType))
                .Sum(x => CountOverlapWeekdays(x.FromDate, x.ToDate, month, year));
            var actualWorkdays = records.Count(x => x.Status == AttendanceStatus.Present);
            var absentDays = Math.Max(0, standardWorkdays - actualWorkdays - paidLeaveDays - unpaidLeaveDays);

            var summary = await _context.MonthlyAttendanceSummaries
                .FirstOrDefaultAsync(x => x.EmployeeId == employee.EmployeeId && x.Month == month && x.Year == year, cancellationToken);
            if (summary == null)
            {
                summary = new MonthlyAttendanceSummary
                {
                    EmployeeId = employee.EmployeeId,
                    Month = month,
                    Year = year
                };
                _context.MonthlyAttendanceSummaries.Add(summary);
            }

            summary.StandardWorkdays = standardWorkdays;
            summary.ActualWorkdays = actualWorkdays;
            summary.TotalWorkedHours = records.Sum(x => x.WorkedHours);
            summary.OvertimeHours = records.Sum(x => x.OvertimeHours);
            summary.PaidLeaveDays = paidLeaveDays;
            summary.UnpaidLeaveDays = unpaidLeaveDays;
            summary.AbsentDays = absentDays;
            summary.IsClosed = true;
            summary.ClosedAt = NormalizeUtc(_clock.UtcNow);
            results.Add(summary);
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var summary in results)
        {
            await _eventPublisher.PublishAsync(new AttendanceMonthlyClosedEvent
            {
                EmployeeId = summary.EmployeeId,
                Month = summary.Month,
                Year = summary.Year,
                StandardWorkdays = summary.StandardWorkdays,
                ActualWorkdays = summary.ActualWorkdays,
                OvertimeHours = summary.OvertimeHours,
                PaidLeaveDays = summary.PaidLeaveDays,
                UnpaidLeaveDays = summary.UnpaidLeaveDays
            }, cancellationToken);
        }

        return results.Select(ToDto).ToList();
    }

    public async Task<List<MonthlyAttendanceSummaryDto>> GetSummaryAsync(
        int month,
        int year,
        CancellationToken cancellationToken = default)
    {
        ValidateMonthYear(month, year);
        var query = _context.MonthlyAttendanceSummaries.AsNoTracking()
            .Where(x => x.Month == month && x.Year == year);

        if (_currentUser.IsInRole("Manager"))
        {
            var managerId = RequireEmployeeId();
            var departmentIds = await _context.DepartmentReferences
                .Where(x => x.ManagerEmployeeId == managerId)
                .Select(x => x.DepartmentId)
                .ToListAsync(cancellationToken);
            var employeeIds = await _context.EmployeeReferences
                .Where(x => x.DepartmentId.HasValue && departmentIds.Contains(x.DepartmentId.Value))
                .Select(x => x.EmployeeId)
                .ToListAsync(cancellationToken);
            query = query.Where(x => employeeIds.Contains(x.EmployeeId));
        }

        return await query.Select(x => ToDto(x)).ToListAsync(cancellationToken);
    }

    public static AttendanceRecordDto ToDto(AttendanceRecord record)
    {
        return new AttendanceRecordDto(
            record.Id,
            record.EmployeeId,
            record.WorkDate,
            record.ShiftId,
            record.CheckInTime,
            record.CheckOutTime,
            record.WorkedHours,
            record.OvertimeHours,
            record.LateMinutes,
            record.EarlyLeaveMinutes,
            record.Status,
            record.Note);
    }

    private static MonthlyAttendanceSummaryDto ToDto(MonthlyAttendanceSummary summary)
    {
        return new MonthlyAttendanceSummaryDto(
            summary.Id,
            summary.EmployeeId,
            summary.Month,
            summary.Year,
            summary.StandardWorkdays,
            summary.ActualWorkdays,
            summary.TotalWorkedHours,
            summary.OvertimeHours,
            summary.PaidLeaveDays,
            summary.UnpaidLeaveDays,
            summary.AbsentDays,
            summary.IsClosed,
            summary.ClosedAt);
    }

    private IQueryable<AttendanceRecord> QueryByEmployeeAndMonth(Guid employeeId, int month, int year)
    {
        return _context.AttendanceRecords.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.WorkDate.Month == month && x.WorkDate.Year == year)
            .OrderBy(x => x.WorkDate);
    }

    private async Task<EmployeeReference> GetActiveEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await _context.EmployeeReferences
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);
        if (employee == null)
            throw new InvalidOperationException("Nhân viên chưa được đồng bộ sang Attendance.");
        if (!employee.IsActive)
            throw new InvalidOperationException("Nhân viên đã nghỉ việc, không được chấm công.");
        return employee;
    }

    // === Kiosk: chấm công CÔNG KHAI bằng MÃ nhân viên (cho NV không có tài khoản) ===
    // Trạng thái bật/tắt kiosk — admin khóa/mở. In-memory (đủ cho demo; reset khi service khởi động lại).
    private static bool _kioskEnabled = true;
    public bool GetKioskEnabled() => _kioskEnabled;
    public void SetKioskEnabled(bool enabled) => _kioskEnabled = enabled;

    public async Task<KioskCheckResultDto> KioskCheckInAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        if (!_kioskEnabled)
            throw new InvalidOperationException("Kiosk đang tạm khóa. Vui lòng liên hệ quản trị viên.");
        var employee = await GetActiveEmployeeByCodeAsync(employeeCode, cancellationToken);
        var record = await CheckInForEmployeeAsync(employee, cancellationToken);
        return new KioskCheckResultDto(employee.FullName, employee.EmployeeCode, "check-in", record);
    }

    public async Task<KioskCheckResultDto> KioskCheckOutAsync(string employeeCode, CancellationToken cancellationToken = default)
    {
        if (!_kioskEnabled)
            throw new InvalidOperationException("Kiosk đang tạm khóa. Vui lòng liên hệ quản trị viên.");
        var employee = await GetActiveEmployeeByCodeAsync(employeeCode, cancellationToken);
        var record = await CheckOutForEmployeeAsync(employee.EmployeeId, cancellationToken);
        return new KioskCheckResultDto(employee.FullName, employee.EmployeeCode, "check-out", record);
    }

    private async Task<EmployeeReference> GetActiveEmployeeByCodeAsync(string employeeCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
            throw new InvalidOperationException("Vui lòng nhập mã nhân viên.");
        var code = employeeCode.Trim();
        var employee = await _context.EmployeeReferences
            .FirstOrDefaultAsync(x => x.EmployeeCode == code, cancellationToken);
        if (employee == null)
            throw new InvalidOperationException($"Không tìm thấy nhân viên với mã '{code}'.");
        if (!employee.IsActive)
            throw new InvalidOperationException("Nhân viên đã nghỉ việc, không được chấm công.");
        return employee;
    }

    private async Task<Shift?> GetDefaultShiftAsync(CancellationToken cancellationToken)
    {
        return await _context.Shifts
            .Where(x => x.IsActive)
            .OrderBy(x => x.ShiftCode == "DAY" ? 0 : 1)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task EnsureManagerCanAccessEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await _context.EmployeeReferences
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);
        if (employee?.DepartmentId == null)
            throw new UnauthorizedAccessException("Manager không có quyền truy cập nhân viên này.");
        await EnsureManagerOwnsDepartmentAsync(employee.DepartmentId.Value, cancellationToken);
    }

    private async Task EnsureManagerOwnsDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var managedDeptIds = await GetManagedDepartmentIdsAsync(cancellationToken);
        if (!managedDeptIds.Contains(departmentId))
            throw new UnauthorizedAccessException("Manager chỉ được truy cập dữ liệu phòng mình quản lý.");
    }

    // Phòng ban trong quyền manager (quản lý trực tiếp + toàn bộ team con).
    private async Task<HashSet<Guid>> GetManagedDepartmentIdsAsync(CancellationToken cancellationToken)
    {
        var managerId = RequireEmployeeId();
        var allDepts = await _context.DepartmentReferences.AsNoTracking().ToListAsync(cancellationToken);
        return ManagerScope.ResolveDepartmentIds(allDepts, managerId);
    }

    // Nhân viên trong quyền manager (thuộc các phòng ở trên).
    private async Task<List<Guid>> GetManagedEmployeeIdsAsync(CancellationToken cancellationToken)
    {
        var deptIds = (await GetManagedDepartmentIdsAsync(cancellationToken)).ToList();
        return await _context.EmployeeReferences
            .Where(e => e.DepartmentId != null && deptIds.Contains(e.DepartmentId.Value))
            .Select(e => e.EmployeeId)
            .ToListAsync(cancellationToken);
    }

    private Guid RequireEmployeeId()
    {
        return _currentUser.EmployeeId
            ?? throw new UnauthorizedAccessException("Token không có employee_id.");
    }

    private static void ApplyWorkedTime(AttendanceRecord record, Shift? shift)
    {
        record.WorkedHours = 0;
        record.OvertimeHours = 0;
        record.EarlyLeaveMinutes = 0;
        if (record.CheckInTime == null || record.CheckOutTime == null || shift == null)
            return;

        var checkInUtc = NormalizeUtc(record.CheckInTime.Value);
        var checkOutUtc = NormalizeUtc(record.CheckOutTime.Value);
        if (checkOutUtc <= checkInUtc)
            throw new InvalidOperationException("Giờ ra phải lớn hơn giờ vào.");

        var durationHours = (decimal)(checkOutUtc - checkInUtc).TotalHours;
        record.WorkedHours = Math.Max(0, Math.Round(durationHours - 1m, 2));
        record.OvertimeHours = Math.Max(0, record.WorkedHours - shift.StandardHours);

        var checkOutLocal = TimeZoneInfo.ConvertTimeFromUtc(checkOutUtc, VietnamTimeZone);
        var scheduledEnd = BuildLocalShiftBoundary(record.WorkDate, shift.EndTime, shift.EndTime < shift.StartTime);
        if (checkOutLocal < scheduledEnd)
            record.EarlyLeaveMinutes = (int)Math.Ceiling((scheduledEnd - checkOutLocal).TotalMinutes);
    }

    private static int CalculateLateMinutes(DateOnly workDate, Shift shift, DateTime localCheckIn)
    {
        var scheduledStart = BuildLocalShiftBoundary(workDate, shift.StartTime, false);
        return localCheckIn > scheduledStart
            ? (int)Math.Ceiling((localCheckIn - scheduledStart).TotalMinutes)
            : 0;
    }

    private static DateTime BuildLocalShiftBoundary(DateOnly workDate, TimeOnly time, bool nextDay)
    {
        var date = workDate.ToDateTime(time);
        return nextDay ? date.AddDays(1) : date;
    }

    private static decimal CountWeekdays(int month, int year)
    {
        ValidateMonthYear(month, year);
        var days = DateTime.DaysInMonth(year, month);
        return Enumerable.Range(1, days)
            .Select(day => new DateOnly(year, month, day))
            .Count(IsWeekday);
    }

    private static decimal CountOverlapWeekdays(DateOnly from, DateOnly to, int month, int year)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var start = from > monthStart ? from : monthStart;
        var end = to < monthEnd ? to : monthEnd;
        if (start > end)
            return 0;

        var total = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (IsWeekday(day))
                total++;
        }
        return total;
    }

    private static bool IsWeekday(DateOnly day)
    {
        return day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static void ValidateMonthYear(int month, int year)
    {
        if (month is < 1 or > 12)
            throw new InvalidOperationException("Tháng không hợp lệ.");
        if (year < 2000)
            throw new InvalidOperationException("Năm không hợp lệ.");
    }

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");
        }
    }
}
