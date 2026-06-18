using HRAttendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRAttendance.Infrastructure.Migrations;

// Loại nghỉ động: thêm 2 cột Name + IsPaid vào LeavePolicies.
// (Viết tay theo đúng convention inline-attribute của dự án — không dùng Designer.)
[DbContext(typeof(AttendanceDbContext))]
[Migration("20260617080000_AddDynamicLeaveTypes")]
public partial class AddDynamicLeaveTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "LeavePolicies",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "IsPaid",
            table: "LeavePolicies",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Name", table: "LeavePolicies");
        migrationBuilder.DropColumn(name: "IsPaid", table: "LeavePolicies");
    }
}
