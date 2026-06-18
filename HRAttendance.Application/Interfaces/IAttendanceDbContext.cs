using HRAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRAttendance.Application.Interfaces;

public interface IAttendanceDbContext
{
    DbSet<EmployeeReference> EmployeeReferences { get; }
    DbSet<DepartmentReference> DepartmentReferences { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<LeavePolicy> LeavePolicies { get; }
    DbSet<MonthlyAttendanceSummary> MonthlyAttendanceSummaries { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
