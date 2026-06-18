using HRAttendance.Domain.Enums;

namespace HRAttendance.Application.DTOs;

// Kiosk chấm công bằng mã nhân viên
public class KioskCheckRequest
{
    public string EmployeeCode { get; set; } = string.Empty;
}

// Admin khóa/mở kiosk
public class KioskToggleRequest
{
    public bool Enabled { get; set; }
}

public record KioskCheckResultDto(
    string FullName,
    string EmployeeCode,
    string Action,            // "check-in" | "check-out"
    AttendanceRecordDto Record);

public record AttendanceRecordDto(
    Guid Id,
    Guid EmployeeId,
    DateOnly WorkDate,
    Guid? ShiftId,
    DateTime? CheckInTime,
    DateTime? CheckOutTime,
    decimal WorkedHours,
    decimal OvertimeHours,
    int LateMinutes,
    int EarlyLeaveMinutes,
    AttendanceStatus Status,
    string? Note);

public class ManualAttendanceRequest
{
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public Guid? ShiftId { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Note { get; set; }
}

public record MonthlyAttendanceSummaryDto(
    Guid Id,
    Guid EmployeeId,
    int Month,
    int Year,
    decimal StandardWorkdays,
    decimal ActualWorkdays,
    decimal TotalWorkedHours,
    decimal OvertimeHours,
    decimal PaidLeaveDays,
    decimal UnpaidLeaveDays,
    decimal AbsentDays,
    bool IsClosed,
    DateTime? ClosedAt);
