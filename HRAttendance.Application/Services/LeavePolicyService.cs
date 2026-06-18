using HRAttendance.Application.DTOs;
using HRAttendance.Application.Interfaces;
using HRAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRAttendance.Application.Services;

public class LeavePolicyService
{
    private readonly IAttendanceDbContext _context;

    public LeavePolicyService(IAttendanceDbContext context)
    {
        _context = context;
    }

    public async Task<List<LeavePolicyDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.LeavePolicies.AsNoTracking()
            .OrderBy(x => x.LeaveType)
            .Select(x => new LeavePolicyDto(
                x.Id, x.LeaveType, x.Name, x.IsPaid, x.AnnualQuotaDays, x.Description, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<LeavePolicyDto> CreateAsync(
        CreateLeavePolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Tên loại nghỉ không được để trống.");
        if (request.AnnualQuotaDays is < 0)
            throw new InvalidOperationException("Hạn mức nghỉ phép không được âm.");

        // Mã loại nghỉ tự tăng: max hiện có + 1 (lần đầu = 0).
        var maxCode = await _context.LeavePolicies.AnyAsync(cancellationToken)
            ? await _context.LeavePolicies.MaxAsync(x => x.LeaveType, cancellationToken)
            : -1;

        var policy = new LeavePolicy
        {
            Id = Guid.NewGuid(),
            LeaveType = maxCode + 1,
            Name = request.Name.Trim(),
            IsPaid = request.IsPaid,
            AnnualQuotaDays = request.AnnualQuotaDays,
            Description = request.Description,
            IsActive = true
        };
        _context.LeavePolicies.Add(policy);
        await _context.SaveChangesAsync(cancellationToken);

        return new LeavePolicyDto(policy.Id, policy.LeaveType, policy.Name, policy.IsPaid,
            policy.AnnualQuotaDays, policy.Description, policy.IsActive);
    }

    public async Task<LeavePolicyDto> UpdateAsync(
        Guid id,
        UpdateLeavePolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var policy = await _context.LeavePolicies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (policy == null)
            throw new InvalidOperationException("Không tìm thấy loại nghỉ.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Tên loại nghỉ không được để trống.");
        if (request.AnnualQuotaDays is < 0)
            throw new InvalidOperationException("Hạn mức nghỉ phép không được âm.");

        // KHÔNG cho đổi mã loại nghỉ (LeaveType) — chỉ sửa các thuộc tính.
        policy.Name = request.Name.Trim();
        policy.IsPaid = request.IsPaid;
        policy.AnnualQuotaDays = request.AnnualQuotaDays;
        policy.Description = request.Description;
        policy.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        return new LeavePolicyDto(policy.Id, policy.LeaveType, policy.Name, policy.IsPaid,
            policy.AnnualQuotaDays, policy.Description, policy.IsActive);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var policy = await _context.LeavePolicies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (policy == null)
            throw new InvalidOperationException("Không tìm thấy loại nghỉ.");

        // Chặn xóa nếu đã có đơn nghỉ dùng loại này (toàn vẹn dữ liệu) → khuyên dùng tắt hoạt động.
        var inUse = await _context.LeaveRequests.AnyAsync(x => x.LeaveType == policy.LeaveType, cancellationToken);
        if (inUse)
            throw new InvalidOperationException("Loại nghỉ này đã có đơn sử dụng, không thể xóa. Hãy tắt hoạt động thay vì xóa.");

        _context.LeavePolicies.Remove(policy);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
