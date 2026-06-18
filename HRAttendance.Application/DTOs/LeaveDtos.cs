using HRAttendance.Domain.Enums;

namespace HRAttendance.Application.DTOs;

public class CreateLeaveRequest
{
    public int LeaveType { get; set; }   // mã loại nghỉ (lấy từ danh sách loại đang hoạt động)
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class RejectLeaveRequest
{
    public string Reason { get; set; } = string.Empty;
}

public record LeaveRequestDto(
    Guid Id,
    Guid EmployeeId,
    int LeaveType,
    string LeaveTypeName,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalDays,
    string Reason,
    LeaveStatus Status,
    Guid? ApproverEmployeeId,
    DateTime? ApprovedAt,
    string? RejectReason,
    DateTime CreatedAt);

// Tạo loại nghỉ mới (động)
public class CreateLeavePolicyRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsPaid { get; set; } = true;
    public decimal? AnnualQuotaDays { get; set; }
    public string? Description { get; set; }
}

public class UpdateLeavePolicyRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsPaid { get; set; } = true;
    public decimal? AnnualQuotaDays { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public record LeavePolicyDto(
    Guid Id,
    int LeaveType,
    string Name,
    bool IsPaid,
    decimal? AnnualQuotaDays,
    string? Description,
    bool IsActive);

public record LeaveBalanceDto(
    int LeaveType,
    string Name,
    decimal? EntitledDays,
    decimal UsedDays,
    decimal? RemainingDays);
