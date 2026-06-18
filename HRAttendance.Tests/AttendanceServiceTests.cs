using FluentAssertions;
using HRAttendance.Application.DTOs;
using HRAttendance.Application.Services;
using HRAttendance.Domain.Entities;
using HRAttendance.Domain.Enums;
using HRAttendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRAttendance.Tests;

public class AttendanceServiceTests
{
    [Fact]
    public async Task CheckIn_WhenAlreadyCheckedIn_ShouldThrow()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployeeAndShift(db, employeeId);
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = employeeId,
            WorkDate = new DateOnly(2026, 6, 16),
            CheckInTime = new DateTime(2026, 6, 16, 1, 0, 0, DateTimeKind.Utc),
            Status = AttendanceStatus.Present
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, employeeId, new DateTime(2026, 6, 16, 2, 0, 0, DateTimeKind.Utc));

        Func<Task> act = () => service.CheckInAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Nhân viên đã check-in*");
    }

    [Fact]
    public async Task CheckOut_WhenNoCheckIn_ShouldThrow()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployeeAndShift(db, employeeId);
        await db.SaveChangesAsync();
        var service = CreateService(db, employeeId, new DateTime(2026, 6, 16, 10, 0, 0, DateTimeKind.Utc));

        Func<Task> act = () => service.CheckOutAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Chưa có bản ghi check-in*");
    }

    [Fact]
    public async Task CheckOut_ForDayShift_ShouldCalculateLateWorkedAndOvertime()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var shift = SeedEmployeeAndShift(db, employeeId);
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = employeeId,
            WorkDate = new DateOnly(2026, 6, 16),
            ShiftId = shift.Id,
            CheckInTime = new DateTime(2026, 6, 16, 1, 30, 0, DateTimeKind.Utc),
            LateMinutes = 30,
            Status = AttendanceStatus.Present
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, employeeId, new DateTime(2026, 6, 16, 11, 0, 0, DateTimeKind.Utc));

        var result = await service.CheckOutAsync();

        result.WorkedHours.Should().Be(8.5m);
        result.OvertimeHours.Should().Be(0.5m);
    }

    [Fact]
    public async Task CheckOut_ForNightShift_ShouldCalculateAcrossMidnight()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployeeAndShift(db, employeeId);
        var night = new Shift
        {
            ShiftCode = "NIGHT",
            ShiftName = "Night Shift",
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(6, 0),
            StandardHours = 8,
            IsActive = true
        };
        db.Shifts.Add(night);
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = employeeId,
            WorkDate = new DateOnly(2026, 6, 16),
            ShiftId = night.Id,
            CheckInTime = new DateTime(2026, 6, 16, 15, 0, 0, DateTimeKind.Utc),
            Status = AttendanceStatus.Present
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, employeeId, new DateTime(2026, 6, 16, 23, 30, 0, DateTimeKind.Utc));

        var result = await service.CheckOutAsync();

        result.WorkedHours.Should().Be(7.5m);
        result.EarlyLeaveMinutes.Should().Be(0);
    }

    [Fact]
    public async Task CheckOut_ForNightShiftWhenLeavingEarly_ShouldCalculateEarlyLeave()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployeeAndShift(db, employeeId);
        var night = new Shift
        {
            ShiftCode = "NIGHT",
            ShiftName = "Night Shift",
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(6, 0),
            StandardHours = 8,
            IsActive = true
        };
        db.Shifts.Add(night);
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = employeeId,
            WorkDate = new DateOnly(2026, 6, 16),
            ShiftId = night.Id,
            CheckInTime = new DateTime(2026, 6, 16, 15, 0, 0, DateTimeKind.Utc),
            Status = AttendanceStatus.Present
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, employeeId, new DateTime(2026, 6, 16, 22, 30, 0, DateTimeKind.Utc));

        var result = await service.CheckOutAsync();

        result.WorkedHours.Should().Be(6.5m);
        result.EarlyLeaveMinutes.Should().Be(30);
    }

    [Fact]
    public async Task CheckIn_WhenEmployeeInactive_ShouldThrow()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        SeedEmployeeAndShift(db, employeeId, isActive: false);
        await db.SaveChangesAsync();
        var service = CreateService(db, employeeId, new DateTime(2026, 6, 16, 1, 0, 0, DateTimeKind.Utc));

        Func<Task> act = () => service.CheckInAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Nhân viên đã nghỉ việc*");
    }

    [Fact]
    public async Task CloseMonth_ShouldCreateSummaryAndPublishEvent()
    {
        var employeeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var shift = SeedEmployeeAndShift(db, employeeId);
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = employeeId,
            WorkDate = new DateOnly(2026, 6, 16),
            ShiftId = shift.Id,
            WorkedHours = 9,
            OvertimeHours = 1,
            Status = AttendanceStatus.Present
        });
        db.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveType = 0,
            FromDate = new DateOnly(2026, 6, 17),
            ToDate = new DateOnly(2026, 6, 17),
            TotalDays = 1,
            Reason = "Annual",
            Status = LeaveStatus.Approved
        });
        await db.SaveChangesAsync();
        var publisher = new CapturingPublisher();
        var service = CreateService(db, employeeId, DateTime.UtcNow, "HR", publisher);

        var summaries = await service.CloseMonthAsync(6, 2026);

        summaries.Should().ContainSingle();
        summaries[0].ActualWorkdays.Should().Be(1);
        summaries[0].PaidLeaveDays.Should().Be(1);
        summaries[0].OvertimeHours.Should().Be(1);
        publisher.Published.Should().ContainSingle();
    }

    private static AttendanceDbContext CreateDbContext()
    {
        return new AttendanceDbContext(new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private static Shift SeedEmployeeAndShift(AttendanceDbContext db, Guid employeeId, bool isActive = true)
    {
        db.EmployeeReferences.Add(new EmployeeReference
        {
            EmployeeId = employeeId,
            EmployeeCode = "EMP",
            FullName = "Test Employee",
            DepartmentId = Guid.NewGuid(),
            IsActive = isActive,
            WorkingStatus = isActive ? "Active" : "Resigned"
        });
        var shift = new Shift
        {
            ShiftCode = "DAY",
            ShiftName = "Day Shift",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            StandardHours = 8,
            IsActive = true
        };
        db.Shifts.Add(shift);
        return shift;
    }

    private static AttendanceService CreateService(
        AttendanceDbContext db,
        Guid employeeId,
        DateTime utcNow,
        string role = "Employee",
        CapturingPublisher? publisher = null)
    {
        return new AttendanceService(
            db,
            new FakeCurrentUser { EmployeeId = employeeId, Role = role },
            new FakeClock { UtcNow = utcNow },
            publisher ?? new CapturingPublisher());
    }
}
