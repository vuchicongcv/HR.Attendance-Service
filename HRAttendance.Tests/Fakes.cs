using HRAttendance.Application.Interfaces;

namespace HRAttendance.Tests;

internal class FakeCurrentUser : ICurrentUser
{
    public Guid? EmployeeId { get; set; }
    public string Role { get; set; } = "Employee";

    public bool IsInRole(params string[] roles)
    {
        return roles.Contains(Role);
    }
}

internal class FakeClock : IDateTimeProvider
{
    public DateTime UtcNow { get; set; }
}

internal class CapturingPublisher : IEventPublisher
{
    public List<object> Published { get; } = new();

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        Published.Add(message);
        return Task.CompletedTask;
    }
}
