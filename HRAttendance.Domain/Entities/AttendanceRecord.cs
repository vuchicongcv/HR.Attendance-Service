using HRAttendance.Domain.Enums;

namespace HRAttendance.Domain.Entities;

public class AttendanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public Guid? ShiftId { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public decimal WorkedHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Note { get; set; }

    public Shift? Shift { get; set; }
}
