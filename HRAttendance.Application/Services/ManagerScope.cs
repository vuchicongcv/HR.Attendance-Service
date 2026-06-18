using HRAttendance.Domain.Entities;

namespace HRAttendance.Application.Services;

public static class ManagerScope
{
    // Tập phòng ban một manager quản lý = phòng quản lý TRỰC TIẾP + toàn bộ phòng con (đệ quy theo ParentDepartmentId).
    // Nhờ vậy manager khối (vd Khối Kỹ thuật) thấy được cả các team con (Backend/Frontend/QA).
    public static HashSet<Guid> ResolveDepartmentIds(IReadOnlyCollection<DepartmentReference> all, Guid managerId)
    {
        var childrenByParent = all
            .Where(d => d.ParentDepartmentId.HasValue)
            .GroupBy(d => d.ParentDepartmentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.DepartmentId).ToList());

        var result = new HashSet<Guid>();
        var stack = new Stack<Guid>(all.Where(d => d.ManagerEmployeeId == managerId).Select(d => d.DepartmentId));
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!result.Add(id)) continue; // đã thăm
            if (childrenByParent.TryGetValue(id, out var kids))
                foreach (var k in kids) stack.Push(k);
        }
        return result;
    }
}
