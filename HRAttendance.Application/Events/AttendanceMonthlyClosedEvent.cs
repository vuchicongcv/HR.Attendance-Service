namespace HRPayroll.Application.Events;

public class AttendanceMonthlyClosedEvent
{
    public Guid EmployeeId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal StandardWorkdays { get; set; }
    public decimal ActualWorkdays { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal PaidLeaveDays { get; set; }
    public decimal UnpaidLeaveDays { get; set; }
}
