using HRAttendance.Application.DTOs;
using HRAttendance.Application.Interfaces;
using HRAttendance.Domain.Entities;
using HRAttendance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HRAttendance.Application.Services;

public class LeaveRequestService
{
    private readonly IAttendanceDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public LeaveRequestService(IAttendanceDbContext context, ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _context = context;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequest request, CancellationToken cancellationToken = default)
    {
        var employeeId = RequireEmployeeId();
        if (request.FromDate > request.ToDate)
            throw new InvalidOperationException("Ngày bắt đầu không được lớn hơn ngày kết thúc.");

        var employee = await _context.EmployeeReferences.FirstOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);
        if (employee == null || !employee.IsActive)
            throw new InvalidOperationException("Nhân viên không tồn tại hoặc đã nghỉ việc.");

        var validType = await _context.LeavePolicies
            .AnyAsync(x => x.LeaveType == request.LeaveType && x.IsActive, cancellationToken);
        if (!validType)
            throw new InvalidOperationException("Loại nghỉ không hợp lệ hoặc đã ngừng hoạt động.");

        await EnsureNoOverlappingLeaveAsync(employeeId, request.FromDate, request.ToDate, cancellationToken);
        var totalDays = CountWeekdays(request.FromDate, request.ToDate);
        await EnsureWithinLeaveQuotaAsync(
            employeeId,
            request.LeaveType,
            request.FromDate,
            totalDays,
            null,
            cancellationToken);

        var leave = new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveType = request.LeaveType,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            TotalDays = totalDays,
            Reason = request.Reason,
            CreatedAt = _clock.UtcNow
        };

        _context.LeaveRequests.Add(leave);
        await _context.SaveChangesAsync(cancellationToken);
        var leaveNames = await GetTypeNamesAsync(cancellationToken);
        return ToDto(leave, TypeName(leaveNames, leave.LeaveType));
    }

    public async Task<List<LeaveRequestDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var employeeId = RequireEmployeeId();
        var leaves = await _context.LeaveRequests.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var names = await GetTypeNamesAsync(cancellationToken);
        return leaves.Select(x => ToDto(x, TypeName(names, x.LeaveType))).ToList();
    }

    public async Task<List<LeaveRequestDto>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var query = _context.LeaveRequests.AsNoTracking()
            .Where(x => x.Status == LeaveStatus.Pending);

        if (_currentUser.IsInRole("Manager"))
        {
            var employeeIds = await GetManagedEmployeeIdsAsync(cancellationToken);
            query = query.Where(x => employeeIds.Contains(x.EmployeeId));
        }

        var leaves = await query.OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        var names = await GetTypeNamesAsync(cancellationToken);
        return leaves.Select(x => ToDto(x, TypeName(names, x.LeaveType))).ToList();
    }

    public async Task<LeaveRequestDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var leave = await GetPendingForDecisionAsync(id, cancellationToken);
        await EnsureCanApproveEmployeeAsync(leave.EmployeeId, cancellationToken);

        var hasPresentAttendance = await _context.AttendanceRecords.AnyAsync(
            x => x.EmployeeId == leave.EmployeeId
                && x.Status == AttendanceStatus.Present
                && x.WorkDate >= leave.FromDate
                && x.WorkDate <= leave.ToDate,
            cancellationToken);
        if (hasPresentAttendance)
            throw new InvalidOperationException("Đơn nghỉ trùng ngày đã chấm công Present. HR cần sửa/xóa công trước khi duyệt.");

        await EnsureWithinLeaveQuotaAsync(
            leave.EmployeeId,
            leave.LeaveType,
            leave.FromDate,
            leave.TotalDays,
            leave.Id,
            cancellationToken);

        leave.Status = LeaveStatus.Approved;
        leave.ApproverEmployeeId = _currentUser.EmployeeId;
        leave.ApprovedAt = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        var leaveNames = await GetTypeNamesAsync(cancellationToken);
        return ToDto(leave, TypeName(leaveNames, leave.LeaveType));
    }

    public async Task<List<LeaveBalanceDto>> GetMyBalanceAsync(int year, CancellationToken cancellationToken = default)
    {
        var employeeId = RequireEmployeeId();
        return await GetBalanceAsync(employeeId, year, cancellationToken);
    }

    public async Task<List<LeaveBalanceDto>> GetBalanceAsync(
        Guid employeeId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (year < 2000 || year > 9999)
            throw new InvalidOperationException("Năm không hợp lệ.");

        await EnsureCanViewEmployeeAsync(employeeId, cancellationToken);

        var policies = await _context.LeavePolicies.AsNoTracking()
            .Where(x => x.IsActive)   // chỉ trả loại nghỉ đang hoạt động → loại đã tắt tự ẩn
            .OrderBy(x => x.LeaveType)
            .ToListAsync(cancellationToken);
        var result = new List<LeaveBalanceDto>();

        foreach (var policy in policies)
        {
            var usedDays = await GetApprovedLeaveDaysAsync(
                employeeId,
                policy.LeaveType,
                year,
                null,
                cancellationToken);
            var remainingDays = policy.AnnualQuotaDays.HasValue
                ? Math.Max(0, policy.AnnualQuotaDays.Value - usedDays)
                : (decimal?)null;

            result.Add(new LeaveBalanceDto(
                policy.LeaveType,
                policy.Name,
                policy.AnnualQuotaDays,
                usedDays,
                remainingDays));
        }

        return result;
    }

    public async Task<LeaveRequestDto> RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var leave = await GetPendingForDecisionAsync(id, cancellationToken);
        await EnsureCanApproveEmployeeAsync(leave.EmployeeId, cancellationToken);

        leave.Status = LeaveStatus.Rejected;
        leave.ApproverEmployeeId = _currentUser.EmployeeId;
        leave.ApprovedAt = _clock.UtcNow;
        leave.RejectReason = reason;
        await _context.SaveChangesAsync(cancellationToken);
        var leaveNames = await GetTypeNamesAsync(cancellationToken);
        return ToDto(leave, TypeName(leaveNames, leave.LeaveType));
    }

    private async Task<LeaveRequest> GetPendingForDecisionAsync(Guid id, CancellationToken cancellationToken)
    {
        var leave = await _context.LeaveRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (leave == null)
            throw new InvalidOperationException("Không tìm thấy đơn nghỉ phép.");
        if (leave.Status != LeaveStatus.Pending)
            throw new InvalidOperationException("Chỉ đơn Pending mới được duyệt hoặc từ chối.");
        return leave;
    }

    private async Task EnsureCanApproveEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (_currentUser.IsInRole("Admin", "HR"))
            return;
        if (!_currentUser.IsInRole("Manager"))
            throw new UnauthorizedAccessException("Bạn không có quyền duyệt đơn nghỉ.");

        var employeeIds = await GetManagedEmployeeIdsAsync(cancellationToken);
        if (!employeeIds.Contains(employeeId))
            throw new UnauthorizedAccessException("Manager chỉ duyệt đơn của nhân viên thuộc phòng mình.");
    }

    private async Task EnsureCanViewEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (_currentUser.IsInRole("Admin", "HR"))
            return;

        if (_currentUser.EmployeeId == employeeId)
            return;

        if (!_currentUser.IsInRole("Manager"))
            throw new UnauthorizedAccessException("Bạn không có quyền xem số phép của nhân viên này.");

        var employeeIds = await GetManagedEmployeeIdsAsync(cancellationToken);
        if (!employeeIds.Contains(employeeId))
            throw new UnauthorizedAccessException("Manager chỉ xem số phép của nhân viên thuộc phòng mình.");
    }

    private async Task<List<Guid>> GetManagedEmployeeIdsAsync(CancellationToken cancellationToken)
    {
        // Phòng quản lý trực tiếp + toàn bộ team con (manager khối duyệt nghỉ cho cả các team con).
        var managerId = RequireEmployeeId();
        var allDepts = await _context.DepartmentReferences.AsNoTracking().ToListAsync(cancellationToken);
        var departmentIds = ManagerScope.ResolveDepartmentIds(allDepts, managerId).ToList();
        return await _context.EmployeeReferences
            .Where(x => x.DepartmentId.HasValue && departmentIds.Contains(x.DepartmentId.Value))
            .Select(x => x.EmployeeId)
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureNoOverlappingLeaveAsync(
        Guid employeeId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var overlaps = await _context.LeaveRequests.AnyAsync(
            x => x.EmployeeId == employeeId
                && (x.Status == LeaveStatus.Pending || x.Status == LeaveStatus.Approved)
                && x.FromDate <= toDate
                && fromDate <= x.ToDate,
            cancellationToken);
        if (overlaps)
            throw new InvalidOperationException("Khoảng nghỉ bị trùng với đơn Pending hoặc Approved.");
    }

    private async Task EnsureWithinLeaveQuotaAsync(
        Guid employeeId,
        int leaveType,
        DateOnly fromDate,
        decimal requestedDays,
        Guid? excludingLeaveRequestId,
        CancellationToken cancellationToken)
    {
        var policy = await _context.LeavePolicies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LeaveType == leaveType && x.IsActive, cancellationToken);
        if (policy?.AnnualQuotaDays is not decimal quota)
            return;

        var year = fromDate.Year;
        var usedDays = await GetApprovedLeaveDaysAsync(
            employeeId,
            leaveType,
            year,
            excludingLeaveRequestId,
            cancellationToken);

        if (usedDays + requestedDays > quota)
        {
            throw new InvalidOperationException(
                $"Vượt hạn mức {policy.Name}: đã dùng {usedDays}, xin thêm {requestedDays}, hạn mức {quota} ngày/năm.");
        }
    }

    private async Task<decimal> GetApprovedLeaveDaysAsync(
        Guid employeeId,
        int leaveType,
        int year,
        Guid? excludingLeaveRequestId,
        CancellationToken cancellationToken)
    {
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);

        var query = _context.LeaveRequests.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId
                && x.LeaveType == leaveType
                && x.Status == LeaveStatus.Approved
                && x.FromDate >= from
                && x.FromDate <= to);

        if (excludingLeaveRequestId.HasValue)
            query = query.Where(x => x.Id != excludingLeaveRequestId.Value);

        return await query.SumAsync(x => x.TotalDays, cancellationToken);
    }

    private Guid RequireEmployeeId()
    {
        return _currentUser.EmployeeId
            ?? throw new UnauthorizedAccessException("Token không có employee_id.");
    }

    private static decimal CountWeekdays(DateOnly fromDate, DateOnly toDate)
    {
        var total = 0;
        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            if (day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                total++;
        }
        return total;
    }

    private static LeaveRequestDto ToDto(LeaveRequest leave, string leaveTypeName)
    {
        return new LeaveRequestDto(
            leave.Id,
            leave.EmployeeId,
            leave.LeaveType,
            leaveTypeName,
            leave.FromDate,
            leave.ToDate,
            leave.TotalDays,
            leave.Reason,
            leave.Status,
            leave.ApproverEmployeeId,
            leave.ApprovedAt,
            leave.RejectReason,
            leave.CreatedAt);
    }

    // Map mã loại nghỉ -> tên (động, từ bảng LeavePolicy)
    private async Task<Dictionary<int, string>> GetTypeNamesAsync(CancellationToken cancellationToken)
        => await _context.LeavePolicies.AsNoTracking()
            .ToDictionaryAsync(p => p.LeaveType, p => p.Name, cancellationToken);

    private static string TypeName(Dictionary<int, string> names, int code)
        => names.TryGetValue(code, out var n) && !string.IsNullOrEmpty(n) ? n : $"Loại {code}";
}
