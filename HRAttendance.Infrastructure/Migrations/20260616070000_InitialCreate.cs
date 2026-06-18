using System;
using HRAttendance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRAttendance.Infrastructure.Migrations;

[DbContext(typeof(AttendanceDbContext))]
[Migration("20260616070000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DepartmentReferences",
            columns: table => new
            {
                DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                DepartmentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ManagerEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DepartmentReferences", x => x.DepartmentId));

        migrationBuilder.CreateTable(
            name: "EmployeeReferences",
            columns: table => new
            {
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                DepartmentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                WorkingStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_EmployeeReferences", x => x.EmployeeId));

        migrationBuilder.CreateTable(
            name: "LeaveRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                LeaveType = table.Column<int>(type: "integer", nullable: false),
                FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                TotalDays = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                ApproverEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_LeaveRequests", x => x.Id));

        migrationBuilder.CreateTable(
            name: "MonthlyAttendanceSummaries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                Month = table.Column<int>(type: "integer", nullable: false),
                Year = table.Column<int>(type: "integer", nullable: false),
                StandardWorkdays = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                ActualWorkdays = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                TotalWorkedHours = table.Column<decimal>(type: "numeric(7,2)", nullable: false),
                OvertimeHours = table.Column<decimal>(type: "numeric(7,2)", nullable: false),
                PaidLeaveDays = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                UnpaidLeaveDays = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                AbsentDays = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_MonthlyAttendanceSummaries", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Shifts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ShiftCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ShiftName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                StandardHours = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Shifts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AttendanceRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                CheckInTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CheckOutTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                WorkedHours = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                OvertimeHours = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                LateMinutes = table.Column<int>(type: "integer", nullable: false),
                EarlyLeaveMinutes = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                table.ForeignKey(
                    name: "FK_AttendanceRecords_Shifts_ShiftId",
                    column: x => x.ShiftId,
                    principalTable: "Shifts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex("IX_AttendanceRecords_ShiftId", "AttendanceRecords", "ShiftId");
        migrationBuilder.CreateIndex("IX_AttendanceRecords_EmployeeId_WorkDate", "AttendanceRecords", new[] { "EmployeeId", "WorkDate" }, unique: true);
        migrationBuilder.CreateIndex("IX_MonthlyAttendanceSummaries_EmployeeId_Month_Year", "MonthlyAttendanceSummaries", new[] { "EmployeeId", "Month", "Year" }, unique: true);
        migrationBuilder.CreateIndex("IX_Shifts_ShiftCode", "Shifts", "ShiftCode", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("AttendanceRecords");
        migrationBuilder.DropTable("DepartmentReferences");
        migrationBuilder.DropTable("EmployeeReferences");
        migrationBuilder.DropTable("LeaveRequests");
        migrationBuilder.DropTable("MonthlyAttendanceSummaries");
        migrationBuilder.DropTable("Shifts");
    }
}
