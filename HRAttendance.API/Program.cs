using System.Text;
using HRAttendance.API.Services;
using HRAttendance.Application.Consumers;
using HRAttendance.Application.Interfaces;
using HRAttendance.Application.Services;
using HRAttendance.Domain.Entities;
using HRAttendance.Infrastructure;
using HRAttendance.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dán JWT do HR Core cấp.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { scheme, Array.Empty<string>() } });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<LeaveRequestService>();
builder.Services.AddScoped<LeavePolicyService>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.")))
        };
    });

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<EmployeeCreatedEventConsumer>();
    x.AddConsumer<EmployeeUpdatedEventConsumer>();
    x.AddConsumer<EmployeeResignedEventConsumer>();
    x.AddConsumer<DepartmentUpdatedEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqUrl = builder.Configuration["RABBITMQ_URL"];
        if (!string.IsNullOrWhiteSpace(rabbitMqUrl))
        {
            cfg.Host(new Uri(rabbitMqUrl), h => h.Heartbeat(TimeSpan.FromSeconds(10)));
        }
        else
        {
            var host = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
            cfg.Host(host, "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
                h.Heartbeat(TimeSpan.FromSeconds(10));
            });
        }

        cfg.ReceiveEndpoint("attendance-event-queue", e =>
        {
            e.ConfigureConsumer<EmployeeCreatedEventConsumer>(context);
            e.ConfigureConsumer<EmployeeUpdatedEventConsumer>(context);
            e.ConfigureConsumer<EmployeeResignedEventConsumer>(context);
            e.ConfigureConsumer<DepartmentUpdatedEventConsumer>(context);
        });
    });
});

builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "postgres", tags: new[] { "ready" });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AttendanceDbContext>();
    db.Database.Migrate();
    SeedDefaultShifts(db);
    SeedDefaultLeavePolicies(db);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.Run();

static void SeedDefaultShifts(AttendanceDbContext db)
{
    if (!db.Shifts.Any(x => x.ShiftCode == "DAY"))
    {
        db.Shifts.Add(new Shift
        {
            ShiftCode = "DAY",
            ShiftName = "Day Shift",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            StandardHours = 8,
            IsActive = true
        });
    }

    if (!db.Shifts.Any(x => x.ShiftCode == "NIGHT"))
    {
        db.Shifts.Add(new Shift
        {
            ShiftCode = "NIGHT",
            ShiftName = "Night Shift",
            StartTime = new TimeOnly(22, 0),
            EndTime = new TimeOnly(6, 0),
            StandardHours = 8,
            IsActive = true
        });
    }

    db.SaveChanges();
}

static void SeedDefaultLeavePolicies(AttendanceDbContext db)
{
    // 3 loại nghỉ mặc định (mã 0/1/2). Loại nghỉ giờ là động — Admin/HR có thể thêm loại mới.
    var defaults = new[]
    {
        (Code: 0, Name: "Phép năm",   IsPaid: true,  Quota: (decimal?)12, Desc: "Nghỉ phép năm có lương"),
        (Code: 1, Name: "Nghỉ ốm",    IsPaid: true,  Quota: (decimal?)30, Desc: "Nghỉ ốm có lương"),
        (Code: 2, Name: "Không lương", IsPaid: false, Quota: (decimal?)null, Desc: "Nghỉ không lương")
    };

    if (!db.LeavePolicies.Any())
    {
        foreach (var d in defaults)
            db.LeavePolicies.Add(new LeavePolicy
            {
                LeaveType = d.Code, Name = d.Name, IsPaid = d.IsPaid,
                AnnualQuotaDays = d.Quota, Description = d.Desc, IsActive = true
            });
        db.SaveChanges();
        return;
    }

    // Nâng cấp dữ liệu cũ: điền Name/IsPaid cho 3 loại mặc định nếu còn trống (sau khi thêm cột mới).
    var changed = false;
    foreach (var d in defaults)
    {
        var p = db.LeavePolicies.FirstOrDefault(x => x.LeaveType == d.Code);
        if (p != null && string.IsNullOrWhiteSpace(p.Name))
        {
            p.Name = d.Name;
            p.IsPaid = d.IsPaid;
            changed = true;
        }
    }
    if (changed) db.SaveChanges();
}
