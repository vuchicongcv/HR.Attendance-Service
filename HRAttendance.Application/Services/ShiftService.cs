using HRAttendance.Application.DTOs;
using HRAttendance.Application.Interfaces;
using HRAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRAttendance.Application.Services;

public class ShiftService
{
    private readonly IAttendanceDbContext _context;

    public ShiftService(IAttendanceDbContext context)
    {
        _context = context;
    }

    public async Task<List<ShiftDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Shifts.AsNoTracking()
            .OrderBy(x => x.ShiftCode)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<ShiftDto> CreateAsync(UpsertShiftRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var exists = await _context.Shifts.AnyAsync(x => x.ShiftCode == request.ShiftCode, cancellationToken);
        if (exists)
            throw new InvalidOperationException("Mã ca làm việc đã tồn tại.");

        var shift = new Shift
        {
            ShiftCode = request.ShiftCode.Trim().ToUpperInvariant(),
            ShiftName = request.ShiftName.Trim(),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            StandardHours = request.StandardHours,
            IsActive = request.IsActive
        };
        _context.Shifts.Add(shift);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(shift);
    }

    public async Task<ShiftDto> UpdateAsync(Guid id, UpsertShiftRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var shift = await _context.Shifts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy ca làm việc.");
        var code = request.ShiftCode.Trim().ToUpperInvariant();
        var exists = await _context.Shifts.AnyAsync(x => x.Id != id && x.ShiftCode == code, cancellationToken);
        if (exists)
            throw new InvalidOperationException("Mã ca làm việc đã tồn tại.");

        shift.ShiftCode = code;
        shift.ShiftName = request.ShiftName.Trim();
        shift.StartTime = request.StartTime;
        shift.EndTime = request.EndTime;
        shift.StandardHours = request.StandardHours;
        shift.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(shift);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var shift = await _context.Shifts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy ca làm việc.");
        shift.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(UpsertShiftRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ShiftCode))
            throw new InvalidOperationException("Mã ca làm việc là bắt buộc.");
        if (string.IsNullOrWhiteSpace(request.ShiftName))
            throw new InvalidOperationException("Tên ca làm việc là bắt buộc.");
        if (request.StandardHours <= 0)
            throw new InvalidOperationException("Số giờ chuẩn phải lớn hơn 0.");
    }

    private static ShiftDto ToDto(Shift shift)
    {
        return new ShiftDto(
            shift.Id,
            shift.ShiftCode,
            shift.ShiftName,
            shift.StartTime,
            shift.EndTime,
            shift.StandardHours,
            shift.IsActive);
    }
}
