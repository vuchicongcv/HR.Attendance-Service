namespace HRAttendance.Application.Interfaces;

public interface ICurrentUser
{
    Guid? EmployeeId { get; }
    string Role { get; }
    bool IsInRole(params string[] roles);
}
