namespace HRAttendance.Domain.Entities;

public class Shift
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ShiftCode { get; set; } = string.Empty;
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal StandardHours { get; set; }
    public bool IsActive { get; set; } = true;
}
