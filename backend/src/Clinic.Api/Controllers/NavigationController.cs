using System.Security.Claims;
using Clinic.Api.Security;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Clinic.Api.Controllers;
[ControlPlane, ApiController, Route("auth/navigation"), Authorize]
public sealed class NavigationController(AuthorizationDbContext db, MenuAccess access, SuperAdminAccess admin) : ControllerBase {
 [HttpGet]
 public async Task<IActionResult> List(CancellationToken ct) {
  var active = (await access.ActiveMenus(ct)).ToArray();
  var all = await db.PlatformMenus.AsNoTracking().Where(m => active.Contains(m.Id)).OrderBy(m => m.SortOrder).ThenBy(m => m.Name).ToListAsync(ct);
  if (admin.IsSuperAdmin) return Ok(all.Select(m => new { m.Id, m.ParentId, m.Name, m.Path, m.SortOrder }));
  var roles = access.Roles(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), false);
  var direct = (await (from g in db.TenantRoleMenuGrants join p in db.PlatformOperations on g.OperationCode equals p.Code where roles.Contains(g.RoleId) && p.IsActive select g.MenuId).Distinct().ToListAsync(ct)).ToHashSet();
  var visible = new HashSet<Guid>(direct);
  foreach (var item in all.Where(m => direct.Contains(m.Id))) { var parent = item.ParentId; while (parent is Guid id) { visible.Add(id); parent = all.SingleOrDefault(m => m.Id == id)?.ParentId; } }
  return Ok(all.Where(m => visible.Contains(m.Id)).Select(m => new { m.Id, m.ParentId, m.Name, path = direct.Contains(m.Id) ? m.Path : null, m.SortOrder }));
 }
}
