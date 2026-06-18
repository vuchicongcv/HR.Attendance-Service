namespace HRAttendance.Application.DTOs;

public class UpsertShiftRequest
{
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal StandardHours { get; set; }
    public bool IsActive { get; set; } = true;
}

public record ShiftDto(
    Guid Id,
    string ShiftCode,
    string ShiftName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal StandardHours,
    bool IsActive);
