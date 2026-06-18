using HRAttendance.Application.DTOs;
using HRAttendance.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRAttendance.API.Controllers;

[Authorize]
[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly LeaveRequestService _service;

    public LeaveRequestsController(LeaveRequestService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = "Employee,Manager,HR,Admin")]
    public async Task<IActionResult> Create([FromBody] CreateLeaveRequest request, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.CreateAsync(request, cancellationToken));
    }

    [HttpGet("me")]
    [Authorize(Roles = "Employee,Manager,HR,Admin")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.GetMineAsync(cancellationToken));
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Manager,HR,Admin")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.GetPendingAsync(cancellationToken));
    }

    [HttpGet("balance")]
    [Authorize(Roles = "Employee,Manager,HR,Admin")]
    public async Task<IActionResult> GetMyBalance([FromQuery] int year, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.GetMyBalanceAsync(year, cancellationToken));
    }

    [HttpGet("balance/{employeeId:guid}")]
    [Authorize(Roles = "Manager,HR,Admin")]
    public async Task<IActionResult> GetBalance(Guid employeeId, [FromQuery] int year, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.GetBalanceAsync(employeeId, year, cancellationToken));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Manager,HR,Admin")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.ApproveAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Manager,HR,Admin")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectLeaveRequest request, CancellationToken cancellationToken)
    {
        return await HandleAsync(() => _service.RejectAsync(id, request.Reason, cancellationToken));
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
