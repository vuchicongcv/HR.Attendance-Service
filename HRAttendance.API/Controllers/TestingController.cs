using HRAttendance.Application.Interfaces;
using HRPayroll.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HRAttendance.API.Controllers;

// Endpoint demo/giả lập — CHỈ tồn tại khi build Debug, không có ở production (Release).
#if DEBUG
[ApiController]
[Route("api/testing")]
public class TestingController : ControllerBase
{
    private readonly IAttendanceDbContext _context;
    private readonly IEventPublisher _eventPublisher;

    public TestingController(IAttendanceDbContext context, IEventPublisher eventPublisher)
    {
        _context = context;
        _eventPublisher = eventPublisher;
    }

    [HttpPost("trigger-end-of-month-demo")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> TriggerEndOfMonthDemo([FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
    {
        // Kiểm tra xem trong DB có bao nhiêu nhân viên tổng cộng (bỏ qua điều kiện trạng thái để debug)
        var totalInDb = await _context.EmployeeReferences.CountAsync(cancellationToken);

        // 1. Lấy tất cả nhân viên đang làm việc từ database đồng bộ của N2 (Bỏ filter trạng thái để đảm bảo không bị sót)
        var employees = await _context.EmployeeReferences
            .ToListAsync(cancellationToken);

        var random = new Random();
        int triggeredCount = 0;

        foreach (var emp in employees)
        {
            // 2. Mocking số liệu giả lập cho từng nhân viên để Demo
            
            // Random ngày công thực tế từ 18 đến 22 (bước nhảy 0.5)
            // random.Next(36, 45) / 2m => sinh ra số từ 18.0 đến 22.0
            decimal actualWorkdays = random.Next(36, 45) / 2m; 
            
            // Random giờ tăng ca từ 0 đến 10 giờ
            decimal overtimeHours = random.Next(0, 11); 
            
            // Nếu ngày công thực tế < 22, có thể họ có nghỉ phép có lương (tối đa phần thiếu)
            decimal paidLeaveDays = 0;
            if (actualWorkdays < 22m)
            {
                decimal missingDays = 22m - actualWorkdays;
                paidLeaveDays = random.Next(0, (int)(missingDays * 2) + 1) / 2m;
            }
            
            // Ngày nghỉ không lương = phần còn lại của công chuẩn 22
            decimal unpaidLeaveDays = 22m - actualWorkdays - paidLeaveDays;
            if (unpaidLeaveDays < 0) unpaidLeaveDays = 0;

            var eomEvent = new AttendanceMonthlyClosedEvent
            {
                EmployeeId = emp.EmployeeId,
                Month = month,
                Year = year,
                StandardWorkdays = 22m, // Cố định công chuẩn 22
                ActualWorkdays = actualWorkdays,
                OvertimeHours = overtimeHours,
                PaidLeaveDays = paidLeaveDays,
                UnpaidLeaveDays = unpaidLeaveDays
            };

            // 3. Publish Event sang RabbitMQ (Service Lương N3 sẽ hứng được cái này)
            await _eventPublisher.PublishAsync(eomEvent, cancellationToken);
            triggeredCount++;
        }

        return Ok(new
        {
            Message = "Đã trigger chốt công giả lập thành công.",
            TotalInDb = totalInDb,
            TotalEmployeesTriggered = triggeredCount,
            Month = month,
            Year = year,
            Note = "Dữ liệu chấm công đã được random tự động để phục vụ cho Demo đồ án."
        });
    }
}
#endif
