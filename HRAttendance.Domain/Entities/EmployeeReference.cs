namespace HRAttendance.Domain.Entities;

public class EmployeeReference
{
    public Guid EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string WorkingStatus { get; set; } = "Active";
    public bool IsActive { get; set; } = true;
}
