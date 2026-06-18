using FluentAssertions;
using HRAttendance.Application.DTOs;
using HRAttendance.Application.Services;
using HRAttendance.Domain.Entities;
using HRAttendance.Domain.Enums;
using HRAttendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRAttendance.Tests;

public class LeaveRequestServiceTests
{
    [Fact]
    public async Task Create_WhenDateRangeInvalid_ShouldThrow()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployee(db, employeeId);
        await db.SaveChangesAsync();
        var service = CreateService(db, employeeId);

        Func<Task> act = () => service.CreateAsync(new CreateLeaveRequest
        {
            FromDate = new DateOnly(2026, 6, 20),
            ToDate = new DateOnly(2026, 6, 19),
            Reason = "Invalid"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Ngày bắt đầu*");
    }

    [Fact]
    public async Task Create_WhenOverlapPendingLeave_ShouldThrow()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployee(db, employeeId);
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = employeeId,
            FromDate = new DateOnly(2026, 6, 18),
            ToDate = new DateOnly(2026, 6, 20),
            TotalDays = 3,
            Reason = "Existing",
            Status = LeaveStatus.Pending
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, employeeId);

        Func<Task> act = () => service.CreateAsync(new CreateLeaveRequest
        {
            FromDate = new DateOnly(2026, 6, 19),
            ToDate = new DateOnly(2026, 6, 21),
            Reason = "Overlap"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Khoảng nghỉ bị trùng*");
    }

    [Fact]
    public async Task Approve_WhenManagerApprovesOutsideDepartment_ShouldThrow()
    {
        var managerId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployee(db, employeeId, Guid.NewGuid());
        db.DepartmentReferences.Add(new DepartmentReference
        {
            DepartmentId = Guid.NewGuid(),
            DepartmentName = "Managed",
            ManagerEmployeeId = managerId
        });
        var leave = new LeaveRequest
        {
            EmployeeId = employeeId,
            FromDate = new DateOnly(2026, 6, 18),
            ToDate = new DateOnly(2026, 6, 18),
            TotalDays = 1,
            Reason = "Need leave",
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(leave);
        await db.SaveChangesAsync();
        var service = CreateService(db, managerId, "Manager");

        Func<Task> act = () => service.ApproveAsync(leave.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Manager chỉ duyệt*");
    }

    [Fact]
    public async Task Approve_WhenPresentAttendanceExists_ShouldThrow()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployee(db, employeeId);
        var leave = new LeaveRequest
        {
            EmployeeId = employeeId,
            FromDate = new DateOnly(2026, 6, 18),
            ToDate = new DateOnly(2026, 6, 18),
            TotalDays = 1,
            Reason = "Need leave",
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(leave);
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = employeeId,
            WorkDate = new DateOnly(2026, 6, 18),
            Status = AttendanceStatus.Present
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, Guid.NewGuid(), "HR");

        Func<Task> act = () => service.ApproveAsync(leave.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Đơn nghỉ trùng ngày*");
    }

    [Fact]
    public async Task Create_WhenAnnualLeaveExceedsQuota_ShouldThrow()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployee(db, employeeId);
        SeedLeavePolicy(db, 0, 5);
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveType = 0,
            FromDate = new DateOnly(2026, 6, 1),
            ToDate = new DateOnly(2026, 6, 3),
            TotalDays = 3,
            Reason = "Used annual leave",
            Status = LeaveStatus.Approved
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, employeeId);

        Func<Task> act = () => service.CreateAsync(new CreateLeaveRequest
        {
            LeaveType = 0,
            FromDate = new DateOnly(2026, 6, 8),
            ToDate = new DateOnly(2026, 6, 11),
            Reason = "Too much leave"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Vượt hạn mức*");
    }

    [Fact]
    public async Task Create_WhenUnpaidLeaveHasNoQuota_ShouldSucceed()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployee(db, employeeId);
        SeedLeavePolicy(db, 2, null);
        await db.SaveChangesAsync();
        var service = CreateService(db, employeeId);

        var result = await service.CreateAsync(new CreateLeaveRequest
        {
            LeaveType = 2,
            FromDate = new DateOnly(2026, 6, 1),
            ToDate = new DateOnly(2026, 6, 30),
            Reason = "Unpaid leave"
        });

        result.TotalDays.Should().Be(22);
        result.LeaveType.Should().Be(2);
    }

    [Fact]
    public async Task Approve_WhenPendingLeaveWouldExceedQuota_ShouldThrow()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployee(db, employeeId);
        SeedLeavePolicy(db, 0, 5);
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveType = 0,
            FromDate = new DateOnly(2026, 6, 1),
            ToDate = new DateOnly(2026, 6, 3),
            TotalDays = 3,
            Reason = "Approved",
            Status = LeaveStatus.Approved
        });
        var pending = new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveType = 0,
            FromDate = new DateOnly(2026, 6, 8),
            ToDate = new DateOnly(2026, 6, 10),
            TotalDays = 3,
            Reason = "Pending",
            Status = LeaveStatus.Pending
        };
        db.LeaveRequests.Add(pending);
        await db.SaveChangesAsync();
        var service = CreateService(db, Guid.NewGuid(), "HR");

        Func<Task> act = () => service.ApproveAsync(pending.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Vượt hạn mức*");
    }

    [Fact]
    public async Task GetMyBalance_ShouldReturnEntitledUsedAndRemainingDays()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployee(db, employeeId);
        SeedLeavePolicy(db, 0, 12);
        SeedLeavePolicy(db, 1, 30);
        SeedLeavePolicy(db, 2, null);
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveType = 0,
            FromDate = new DateOnly(2026, 6, 1),
            ToDate = new DateOnly(2026, 6, 3),
            TotalDays = 3,
            Reason = "Approved annual",
            Status = LeaveStatus.Approved
        });
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveType = 2,
            FromDate = new DateOnly(2026, 6, 8),
            ToDate = new DateOnly(2026, 6, 9),
            TotalDays = 2,
            Reason = "Approved unpaid",
            Status = LeaveStatus.Approved
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, employeeId);

        var result = await service.GetMyBalanceAsync(2026);

        result.Should().HaveCount(3);
        result.Single(x => x.LeaveType == 0).Should().BeEquivalentTo(
            new LeaveBalanceDto(0, "Loại 0", 12, 3, 9));
        result.Single(x => x.LeaveType == 1).Should().BeEquivalentTo(
            new LeaveBalanceDto(1, "Loại 1", 30, 0, 30));
        result.Single(x => x.LeaveType == 2).Should().BeEquivalentTo(
            new LeaveBalanceDto(2, "Loại 2", null, 2, null));
    }

    private static AttendanceDbContext CreateDbContext()
    {
        return new AttendanceDbContext(new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private static void SeedEmployee(AttendanceDbContext db, Guid employeeId, Guid? departmentId = null)
    {
        db.EmployeeReferences.Add(new EmployeeReference
        {
            EmployeeId = employeeId,
            EmployeeCode = "EMP",
            FullName = "Employee",
            DepartmentId = departmentId ?? Guid.NewGuid(),
            IsActive = true,
            WorkingStatus = "Active"
        });
    }

    private static void SeedLeavePolicy(AttendanceDbContext db, int leaveType, decimal? quota)
    {
        db.LeavePolicies.Add(new LeavePolicy
        {
            LeaveType = leaveType,
            Name = $"Loại {leaveType}",
            IsPaid = leaveType != 2,
            AnnualQuotaDays = quota,
            Description = $"{leaveType} policy",
            IsActive = true
        });
    }

    private static LeaveRequestService CreateService(AttendanceDbContext db, Guid employeeId, string role = "Employee")
    {
        return new LeaveRequestService(
            db,
            new FakeCurrentUser { EmployeeId = employeeId, Role = role },
            new FakeClock { UtcNow = DateTime.UtcNow });
    }
}
