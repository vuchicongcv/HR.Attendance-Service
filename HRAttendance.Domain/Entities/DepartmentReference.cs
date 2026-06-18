namespace HRAttendance.Domain.Entities;

public class DepartmentReference
{
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    // Phòng ban cha — để tính cây con (manager khối thấy toàn bộ team con). Đồng bộ từ DepartmentUpdatedEvent.
    public Guid? ParentDepartmentId { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}
