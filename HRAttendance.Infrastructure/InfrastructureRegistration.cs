using HRAttendance.Application.Interfaces;
using HRAttendance.Infrastructure.Persistence;
using HRAttendance.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRAttendance.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AttendanceDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        services.AddScoped<IAttendanceDbContext>(provider => provider.GetRequiredService<AttendanceDbContext>());
        services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        return services;
    }
}
