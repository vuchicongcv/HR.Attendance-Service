namespace HRAttendance.Domain.Entities;

public class MonthlyAttendanceSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal StandardWorkdays { get; set; }
    public decimal ActualWorkdays { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal PaidLeaveDays { get; set; }
    public decimal UnpaidLeaveDays { get; set; }
    public decimal AbsentDays { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
}
