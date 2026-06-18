using System;
using HRAttendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRAttendance.Infrastructure.Migrations;

[DbContext(typeof(AttendanceDbContext))]
[Migration("20260616090000_AddLeavePolicies")]
public partial class AddLeavePolicies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LeavePolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                LeaveType = table.Column<int>(type: "integer", nullable: false),
                AnnualQuotaDays = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_LeavePolicies", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_LeavePolicies_LeaveType",
            table: "LeavePolicies",
            column: "LeaveType",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("LeavePolicies");
    }
}
