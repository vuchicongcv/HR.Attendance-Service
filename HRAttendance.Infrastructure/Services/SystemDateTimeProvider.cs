using HRAttendance.Application.Interfaces;

namespace HRAttendance.Infrastructure.Services;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
