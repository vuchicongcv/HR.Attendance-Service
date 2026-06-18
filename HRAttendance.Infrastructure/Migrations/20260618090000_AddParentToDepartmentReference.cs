using HRAttendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRAttendance.Infrastructure.Migrations;

// Thêm ParentDepartmentId vào bản sao phòng ban — để Attendance tính được cây con
// (manager khối thấy chấm công của toàn bộ team con). Đồng bộ từ DepartmentUpdatedEvent.
// (Viết tay theo đúng convention inline-attribute của dự án — không dùng Designer.)
[DbContext(typeof(AttendanceDbContext))]
[Migration("20260618090000_AddParentToDepartmentReference")]
public partial class AddParentToDepartmentReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ParentDepartmentId",
            table: "DepartmentReferences",
            type: "uuid",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ParentDepartmentId",
            table: "DepartmentReferences");
    }
}
