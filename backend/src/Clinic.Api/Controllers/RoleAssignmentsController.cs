using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Clinic.Api.Security;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Clinic.Api.Controllers;
[ControlPlane, ApiController, Route("organization/role-assignments")]
[Authorize(Policy = "tenant:roles.manage"), Authorize(Policy = "tenant:members.manage")]
public sealed class RoleAssignmentsController(AuthorizationDbContext db, VerifiedTenant scope) : ControllerBase {
 private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 [HttpGet]
 public async Task<IActionResult> List(CancellationToken ct) => Ok(await (
  from a in db.RoleAssignments.AsNoTracking() join m in db.Memberships on a.MembershipId equals m.Id
  join u in db.Users on m.UserId equals u.Id join r in db.BusinessRoles on a.RoleId equals r.Id
  select new { a.Id, a.MembershipId, userName = u.UserName, roleName = r.Name, a.RoleId, a.BranchId, a.IsActive }).ToListAsync(ct));
 [HttpPost]
 public async Task<IActionResult> Grant(GrantRequest request, CancellationToken ct) {
  var member = await db.Memberships.SingleOrDefaultAsync(m => m.Id == request.MembershipId && m.IsActive, ct);
  if (member is null || !await db.BusinessRoles.AnyAsync(r => r.Id == request.RoleId && r.IsActive, ct)) return NotFound();
  if (member.UserId == ActorId) return Conflict(new { message = "Không tự thay đổi quyền của chính mình." });
  if (request.BranchId is Guid branch && (!await db.Branches.AnyAsync(b => b.Id == branch && b.IsActive, ct) ||
    (!member.AllBranches && !await db.BranchAccesses.AnyAsync(a => a.MembershipId == member.Id && a.BranchId == branch, ct)))) return BadRequest(new { message = "Nhân viên chưa được cấp quyền vào cơ sở này." });
  var assignment = await db.RoleAssignments.SingleOrDefaultAsync(a => a.MembershipId == member.Id && a.RoleId == request.RoleId && a.BranchId == request.BranchId, ct);
  if (assignment?.IsActive == true) return Conflict(new { message = "Vai trò này đã được cấp." });
  if (assignment is null) { assignment = new RoleAssignment { TenantId = scope.TenantId!.Value, MembershipId = member.Id, RoleId = request.RoleId, BranchId = request.BranchId }; db.RoleAssignments.Add(assignment); }
  else assignment.IsActive = true;
  db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId!.Value, ActorId = ActorId, Action = "role_assignment.granted", ResourceId = assignment.Id, Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Thêm nhân viên vào nhóm" : (string.IsNullOrWhiteSpace(request.Reason) ? "Cập nhật cấu hình phân quyền" : request.Reason.Trim()) });
  try { await db.SaveChangesAsync(ct); }
  catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" }) { return Conflict(new { message = "Quyền đã được thay đổi. Vui lòng tải lại." }); }
  return Ok(new { assignment.Id });
 }
 [HttpPost("{id:guid}/revoke")]
 public async Task<IActionResult> Revoke(Guid id, ReasonRequest request, CancellationToken ct) {
  var assignment = await db.RoleAssignments.SingleOrDefaultAsync(a => a.Id == id, ct);
  if (assignment is null) return NotFound();
  if (await db.Memberships.AnyAsync(m => m.Id == assignment.MembershipId && m.UserId == ActorId, ct)) return Conflict(new { message = "Không tự thu hồi quyền của chính mình." });
  if (!assignment.IsActive) return NoContent();
  assignment.IsActive = false;
  db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId!.Value, ActorId = ActorId, Action = "role_assignment.revoked", ResourceId = id, Reason = (string.IsNullOrWhiteSpace(request.Reason) ? "Cập nhật cấu hình phân quyền" : request.Reason.Trim()) });
  await db.SaveChangesAsync(ct); return NoContent();
 }
 public sealed record GrantRequest(Guid MembershipId, Guid RoleId, Guid? BranchId, [StringLength(500)] string? Reason = null);
 public sealed record ReasonRequest([StringLength(500)] string? Reason = null);
}
