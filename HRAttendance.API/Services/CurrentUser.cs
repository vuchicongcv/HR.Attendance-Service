using System.Security.Claims;
using HRAttendance.Application.Interfaces;

namespace HRAttendance.API.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? EmployeeId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue("employee_id");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string Role => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public bool IsInRole(params string[] roles)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user != null && roles.Any(user.IsInRole);
    }
}
