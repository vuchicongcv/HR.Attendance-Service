using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HRAttendance.Infrastructure.Persistence;

public class AttendanceDbContextFactory : IDesignTimeDbContextFactory<AttendanceDbContext>
{
    public AttendanceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=AttendanceDB;Username=postgres;Password=postgres")
            .Options;
        return new AttendanceDbContext(options);
    }
}
