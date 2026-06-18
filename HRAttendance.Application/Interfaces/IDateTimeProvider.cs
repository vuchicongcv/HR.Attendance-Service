namespace HRAttendance.Application.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
