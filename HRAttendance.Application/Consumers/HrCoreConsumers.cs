using HRCore.Domain.Events;
using HRAttendance.Application.Interfaces;
using HRAttendance.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRAttendance.Application.Consumers;

public class EmployeeCreatedEventConsumer : IConsumer<EmployeeCreatedEvent>
{
    private readonly IAttendanceDbContext _context;
    private readonly ILogger<EmployeeCreatedEventConsumer> _logger;

    public EmployeeCreatedEventConsumer(IAttendanceDbContext context, ILogger<EmployeeCreatedEventConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EmployeeCreatedEvent> context)
    {
        await EmployeeReferenceSync.UpsertEmployeeAsync(_context, context.Message.EmployeeId, context.Message.EmployeeCode,
            context.Message.FullName, context.Message.DepartmentId, context.Message.WorkingStatus?.ToString(), true,
            context.CancellationToken);
        _logger.LogInformation("Synced EmployeeCreatedEvent for {EmployeeId}.", context.Message.EmployeeId);
    }
}

public class EmployeeUpdatedEventConsumer : IConsumer<EmployeeUpdatedEvent>
{
    private readonly IAttendanceDbContext _context;
    private readonly ILogger<EmployeeUpdatedEventConsumer> _logger;

    public EmployeeUpdatedEventConsumer(IAttendanceDbContext context, ILogger<EmployeeUpdatedEventConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EmployeeUpdatedEvent> context)
    {
        await EmployeeReferenceSync.UpsertEmployeeAsync(_context, context.Message.EmployeeId, context.Message.EmployeeCode,
            context.Message.FullName, context.Message.DepartmentId, context.Message.WorkingStatus?.ToString(), true,
            context.CancellationToken);
        _logger.LogInformation("Synced EmployeeUpdatedEvent for {EmployeeId}.", context.Message.EmployeeId);
    }
}

public class EmployeeResignedEventConsumer : IConsumer<EmployeeResignedEvent>
{
    private readonly IAttendanceDbContext _context;
    private readonly ILogger<EmployeeResignedEventConsumer> _logger;

    public EmployeeResignedEventConsumer(IAttendanceDbContext context, ILogger<EmployeeResignedEventConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EmployeeResignedEvent> context)
    {
        await EmployeeReferenceSync.UpsertEmployeeAsync(_context, context.Message.EmployeeId, context.Message.EmployeeCode,
            context.Message.FullName, context.Message.DepartmentId, "Resigned", false, context.CancellationToken);
        _logger.LogInformation("Synced EmployeeResignedEvent for {EmployeeId}.", context.Message.EmployeeId);
    }
}

public class DepartmentUpdatedEventConsumer : IConsumer<DepartmentUpdatedEvent>
{
    private readonly IAttendanceDbContext _context;
    private readonly ILogger<DepartmentUpdatedEventConsumer> _logger;

    public DepartmentUpdatedEventConsumer(IAttendanceDbContext context, ILogger<DepartmentUpdatedEventConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DepartmentUpdatedEvent> context)
    {
        var message = context.Message;
        var department = await _context.DepartmentReferences
            .FirstOrDefaultAsync(x => x.DepartmentId == message.DepartmentId, context.CancellationToken);
        if (department == null)
        {
            department = new DepartmentReference { DepartmentId = message.DepartmentId };
            _context.DepartmentReferences.Add(department);
        }

        department.DepartmentName = message.DepartmentName;
        department.ParentDepartmentId = message.ParentDepartmentId;
        department.ManagerEmployeeId = message.ManagerEmployeeId;
        department.IsActive = message.IsActive;
        await _context.SaveChangesAsync(context.CancellationToken);
        _logger.LogInformation("Synced DepartmentUpdatedEvent for {DepartmentId}.", message.DepartmentId);
    }
}

internal static class EmployeeReferenceSync
{
    public static async Task UpsertEmployeeAsync(
        IAttendanceDbContext db,
        Guid employeeId,
        string employeeCode,
        string fullName,
        Guid? departmentId,
        string? workingStatus,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var employee = await db.EmployeeReferences
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId, cancellationToken);
        if (employee == null)
        {
            employee = new EmployeeReference { EmployeeId = employeeId };
            db.EmployeeReferences.Add(employee);
        }

        employee.EmployeeCode = employeeCode;
        employee.FullName = fullName;
        employee.DepartmentId = departmentId;
        employee.WorkingStatus = workingStatus ?? employee.WorkingStatus;
        employee.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}
