using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Api.Security;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Clinic.Api.Controllers;
[ControlPlane, ApiController, Route("organization/roles"), Authorize(Policy = "tenant:roles.manage")]
public sealed class RolesController(AuthorizationDbContext db, VerifiedTenant scope) : ControllerBase {
 [HttpPut("{id:guid}")]
 public async Task<IActionResult> Update(Guid id, UpdateRequest request, CancellationToken ct) {
  if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest();
  await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  var role = await db.BusinessRoles.SingleOrDefaultAsync(r => r.Id == id, ct); if (role is null) return NotFound();
  var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
  if (await (from a in db.RoleAssignments join m in db.Memberships on a.MembershipId equals m.Id where a.RoleId == id && a.IsActive && m.IsActive select a.Id).AnyAsync(ct) && !request.IsActive) return Conflict(new { message = "Thu hồi các nhân viên khỏi nhóm trước khi ngừng nhóm." });
  role.Name = request.Name.Trim(); role.IsActive = request.IsActive;
  db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId!.Value, ActorId = actor, ResourceId = id, Action = "role.updated", Reason = (string.IsNullOrWhiteSpace(request.Reason) ? "Cập nhật cấu hình phân quyền" : request.Reason.Trim()) });
  try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return NoContent(); }
  catch (Exception e) when (e is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } || e is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } }) { return Conflict(new { message = "Nhóm vừa thay đổi. Tải lại và thử lại." }); }
 }
 [HttpDelete("{id:guid}")]
 public async Task<IActionResult> Delete(Guid id, DeleteRequest request, CancellationToken ct) {
  var role = await db.BusinessRoles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
  return role is null ? NotFound() : await Update(id, new UpdateRequest(role.Name, false, request.Reason), ct);
 }
 public sealed record UpdateRequest([Required, StringLength(200)] string Name, bool IsActive, [StringLength(300)] string? Reason = null);
 public sealed record DeleteRequest([StringLength(300)] string? Reason = null);
 [HttpPost]
 public async Task<IActionResult> Create(CreateRequest request, CancellationToken ct) {
  var code = request.Code.Trim().ToUpperInvariant();
  if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z][A-Z0-9_-]{0,49}$") || string.IsNullOrWhiteSpace(request.Name) ) return BadRequest(new { message = "Nhập mã và tên nhóm hợp lệ." });
  var role = new BusinessRole { TenantId = scope.TenantId!.Value, Code = code, Name = request.Name.Trim() };
  db.BusinessRoles.Add(role);
  db.AuditEvents.Add(new AuditEvent { TenantId = role.TenantId, ActorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = "role.created", ResourceId = role.Id, Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Tạo nhóm người dùng" : (string.IsNullOrWhiteSpace(request.Reason) ? "Cập nhật cấu hình phân quyền" : request.Reason.Trim()) });
  try { await db.SaveChangesAsync(ct); }
  catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return Conflict(new { message = "Mã vai trò đã tồn tại trong đơn vị." }); }
  return Created("/organization/permissions/roles/" + role.Id, new { role.Id, role.Code, role.Name, role.IsActive });
 }
 public sealed record CreateRequest([Required, StringLength(50)] string Code, [Required, StringLength(200)] string Name, [StringLength(300)] string? Reason = null);
}
