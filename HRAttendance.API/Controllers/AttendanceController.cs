using HRAttendance.Application.DTOs;
using HRAttendance.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRAttendance.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceService _service;

    public AttendanceController(AttendanceService service)
    {
        _service = service;
    }

    [HttpPost("check-in")]
    [Authorize(Roles = "Employee,Manager,HR,Admin")]
    public async Task<IActionResult> CheckIn(CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.CheckInAsync(cancellationToken));
    }

    [HttpPost("check-out")]
    [Authorize(Roles = "Employee,Manager,HR,Admin")]
    public async Task<IActionResult> CheckOut(CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.CheckOutAsync(cancellationToken));
    }

    // Kiosk CÔNG KHAI: chấm công bằng mã nhân viên (KHÔNG cần đăng nhập — cho NV không có tài khoản).
    [HttpPost("kiosk/check-in")]
    [AllowAnonymous]
    public async Task<IActionResult> KioskCheckIn([FromBody] KioskCheckRequest request, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.KioskCheckInAsync(request.EmployeeCode, cancellationToken));
    }

    [HttpPost("kiosk/check-out")]
    [AllowAnonymous]
    public async Task<IActionResult> KioskCheckOut([FromBody] KioskCheckRequest request, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.KioskCheckOutAsync(request.EmployeeCode, cancellationToken));
    }

    // Trạng thái kiosk (công khai — để trang kiosk biết đang mở hay đang khóa).
    [HttpGet("kiosk/status")]
    [AllowAnonymous]
    public IActionResult KioskStatus() => Ok(new { isEnabled = _service.GetKioskEnabled() });

    // Bật/tắt kiosk là cấu hình hệ thống → chỉ Admin.
    [HttpPost("kiosk/toggle")]
    [Authorize(Roles = "Admin")]
    public IActionResult KioskToggle([FromBody] KioskToggleRequest request)
    {
        _service.SetKioskEnabled(request.Enabled);
        return Ok(new { isEnabled = request.Enabled });
    }

    [HttpGet("me")]
    [Authorize(Roles = "Employee,Manager,HR,Admin")]
    public async Task<IActionResult> GetMine([FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.GetMineAsync(month, year, cancellationToken));
    }

    [HttpGet]
    [Authorize(Roles = "Admin,HR,Manager")]
    public async Task<IActionResult> Get([FromQuery] Guid? employeeId, [FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.GetAsync(employeeId, month, year, cancellationToken));
    }

    [HttpGet("by-department/{departmentId:guid}")]
    [Authorize(Roles = "Admin,HR,Manager")]
    public async Task<IActionResult> GetByDepartment(Guid departmentId, [FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.GetByDepartmentAsync(departmentId, month, year, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> UpsertManual([FromBody] ManualAttendanceRequest request, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.UpsertManualAsync(request, cancellationToken));
    }

    [HttpPost("close")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Close([FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.CloseMonthAsync(month, year, cancellationToken));
    }

    [HttpGet("summary")]
    [Authorize(Roles = "Admin,HR,Manager")]
    public async Task<IActionResult> GetSummary([FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.GetSummaryAsync(month, year, cancellationToken));
    }

    private async Task<IActionResult> HandleAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}
