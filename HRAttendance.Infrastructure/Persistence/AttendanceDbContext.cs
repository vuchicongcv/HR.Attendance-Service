using HRAttendance.Application.Interfaces;
using HRAttendance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRAttendance.Infrastructure.Persistence;

public class AttendanceDbContext : DbContext, IAttendanceDbContext
{
    public AttendanceDbContext(DbContextOptions<AttendanceDbContext> options) : base(options)
    {
    }

    public DbSet<EmployeeReference> EmployeeReferences => Set<EmployeeReference>();
    public DbSet<DepartmentReference> DepartmentReferences => Set<DepartmentReference>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeavePolicy> LeavePolicies => Set<LeavePolicy>();
    public DbSet<MonthlyAttendanceSummary> MonthlyAttendanceSummaries => Set<MonthlyAttendanceSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmployeeReference>(entity =>
        {
            entity.HasKey(x => x.EmployeeId);
            entity.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DepartmentName).HasMaxLength(200);
            entity.Property(x => x.WorkingStatus).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<DepartmentReference>(entity =>
        {
            entity.HasKey(x => x.DepartmentId);
            entity.Property(x => x.DepartmentName).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ShiftCode).IsUnique();
            entity.Property(x => x.ShiftCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ShiftName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.StandardHours).HasColumnType("numeric(5,2)");
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.EmployeeId, x.WorkDate }).IsUnique();
            entity.Property(x => x.WorkedHours).HasColumnType("numeric(6,2)");
            entity.Property(x => x.OvertimeHours).HasColumnType("numeric(6,2)");
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TotalDays).HasColumnType("numeric(5,2)");
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.Property(x => x.RejectReason).HasMaxLength(500);
        });

        modelBuilder.Entity<LeavePolicy>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.LeaveType).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AnnualQuotaDays).HasColumnType("numeric(5,2)");
            entity.Property(x => x.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<MonthlyAttendanceSummary>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.EmployeeId, x.Month, x.Year }).IsUnique();
            entity.Property(x => x.StandardWorkdays).HasColumnType("numeric(5,2)");
            entity.Property(x => x.ActualWorkdays).HasColumnType("numeric(5,2)");
            entity.Property(x => x.TotalWorkedHours).HasColumnType("numeric(7,2)");
            entity.Property(x => x.OvertimeHours).HasColumnType("numeric(7,2)");
            entity.Property(x => x.PaidLeaveDays).HasColumnType("numeric(5,2)");
            entity.Property(x => x.UnpaidLeaveDays).HasColumnType("numeric(5,2)");
            entity.Property(x => x.AbsentDays).HasColumnType("numeric(5,2)");
        });
    }
}
