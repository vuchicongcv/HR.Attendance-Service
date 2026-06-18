namespace HRAttendance.Domain.Entities;

// Loại nghỉ phép (động): mỗi bản ghi là một loại nghỉ do Admin/HR tự định nghĩa.
public class LeavePolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Mã loại nghỉ (số nguyên, tự tăng khi tạo loại mới). Thay cho enum cố định trước đây.
    public int LeaveType { get; set; }

    // Tên loại nghỉ hiển thị (vd "Phép năm", "Nghỉ ốm", "Nghỉ cưới"...).
    public string Name { get; set; } = string.Empty;

    // Nghỉ CÓ lương hay không → quyết định ngày nghỉ tính vào PaidLeaveDays hay UnpaidLeaveDays khi chốt công.
    public bool IsPaid { get; set; } = true;

    public decimal? AnnualQuotaDays { get; set; }  // null = không giới hạn
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
