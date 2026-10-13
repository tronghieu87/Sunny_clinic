using Clinic.Api.Security;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Clinic.Api.Controllers;
[ApiController, Route("organization")]
public sealed class OrganizationController(AuthorizationDbContext db) : ControllerBase {
 [HttpGet, Authorize, BusinessApi("organization.details", "Thông tin đơn vị")]
 public async Task<IActionResult> Get(CancellationToken ct) => Ok(await db.Tenants.AsNoTracking().Select(t => new { t.Id, t.Code, t.Name }).SingleAsync(ct));
 [HttpGet("branches"), Authorize, BusinessApi("organization.branches", "Danh sách cơ sở", true)]
 public async Task<IActionResult> Branches(CancellationToken ct) => Ok(await db.Branches.AsNoTracking().OrderBy(b => b.Name).Select(b => new { b.Id, b.Code, b.Name, b.IsActive }).ToListAsync(ct));
 [ControlPlane, HttpGet("members"), Authorize(Policy = "tenant:members.manage")]
 public async Task<IActionResult> Members(CancellationToken ct) => Ok(await (from m in db.Memberships.AsNoTracking()
  join u in db.Users on m.UserId equals u.Id select new { m.Id, userName = u.UserName, m.IsActive, m.AllBranches }).ToListAsync(ct));
 [ControlPlane, HttpGet("roles"), Authorize(Policy = "tenant:roles.manage")]
 public async Task<IActionResult> Roles(CancellationToken ct) => Ok(await db.BusinessRoles.AsNoTracking().Select(r => new { r.Id, r.Code, r.Name, r.IsActive }).ToListAsync(ct));
}
