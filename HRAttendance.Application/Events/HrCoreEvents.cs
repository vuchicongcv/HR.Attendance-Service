namespace HRCore.Domain.Events;

public class EmployeeCreatedEvent
{
    public Guid EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? ContractTypeId { get; set; }
    public DateTime HireDate { get; set; }
    public object? WorkingStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EmployeeUpdatedEvent
{
    public Guid EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? ContractTypeId { get; set; }
    public object? WorkingStatus { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EmployeeResignedEvent
{
    public Guid EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Guid? PositionId { get; set; }
    public DateTime? ResignedDate { get; set; }
    public string? ResignedReason { get; set; }
    public object? WorkingStatus { get; set; }
}

public class DepartmentUpdatedEvent
{
    public Guid DepartmentId { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public Guid? ParentDepartmentId { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}
