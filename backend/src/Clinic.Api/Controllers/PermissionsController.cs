using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Application;
using Clinic.Api.Security;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Clinic.Api.Controllers;
[ControlPlane, ApiController, Route("organization/permissions"), Authorize(Policy = "tenant:roles.manage")]
public sealed class PermissionsController(AuthorizationDbContext db, VerifiedTenant scope, SuperAdminAccess admin) : ControllerBase {
 [HttpGet("endpoints")]
 public async Task<IActionResult> Endpoints(CancellationToken ct) => Ok(await db.ApiEndpoints.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.Description).ToListAsync(ct));
 [HttpGet]
 public async Task<IActionResult> List(CancellationToken ct) {
  var operations = await db.PlatformOperations.AsNoTracking().OrderBy(p => p.Code).ToListAsync(ct);
  var mappings = await db.PlatformMenuEndpoints.AsNoTracking().ToListAsync(ct);
  return Ok(operations.Select(p => new { p.Code, p.Description, p.IsSystem, p.IsActive, endpoints = mappings.Where(m => m.OperationCode == p.Code).Select(m => m.EndpointCode).Distinct() }));
 }
 [HttpPost("{code}")]
 [HttpPut("{code}")]
 public async Task<IActionResult> Save(string code, SaveRequest request, CancellationToken ct) {
  if (!admin.IsSuperAdmin) return Forbid();
  if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-zA-Z][a-zA-Z0-9_.-]{0,99}$") || string.IsNullOrWhiteSpace(request.Description)) return BadRequest(new { message = "Nhập tên và mã thao tác hợp lệ." });
  if (PermissionCatalog.All.ContainsKey(code)) return Conflict(new { message = "Quyền nền tảng được bảo vệ." });
  var operation = await db.PlatformOperations.SingleOrDefaultAsync(p => p.Code == code, ct);
  if (operation?.IsSystem == true) return Conflict(new { message = "Thao tác nền tảng được bảo vệ." });
  if (Request.Method == "POST" && operation is not null) return Conflict(new { message = "Mã thao tác đã tồn tại." });
  var description = request.Description.Trim();
  if (await db.PlatformOperations.AnyAsync(p => p.Code != code && p.Description.ToLower() == description.ToLower(), ct)) return Conflict(new { message = "Tên thao tác đã được dùng trong hệ thống." });
  if (operation is null) { operation = new PlatformOperation { Code = code }; db.PlatformOperations.Add(operation); }
  operation.Description = description; operation.IsActive = request.IsActive;
  db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId!.Value, ActorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = "platform.operation_configured", ResourceId = Guid.Empty, Reason = code });
  await db.SaveChangesAsync(ct); return NoContent();
 }
 [HttpGet("roles/{id:guid}")]
 public async Task<IActionResult> Role(Guid id, CancellationToken ct) {
  if (!await db.BusinessRoles.AnyAsync(r => r.Id == id, ct)) return NotFound();
  return Ok(await db.RolePermissions.Where(p => p.RoleId == id).Select(p => p.PermissionCode).ToArrayAsync(ct));
 }
 [HttpPut("roles/{id:guid}")]
 public async Task<IActionResult> Role(Guid id, RoleRequest request, CancellationToken ct) {
  var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
  if (!await db.BusinessRoles.AnyAsync(r => r.Id == id && r.IsActive, ct)) return NotFound();
  if (await (from a in db.RoleAssignments join m in db.Memberships on a.MembershipId equals m.Id where a.RoleId == id && a.IsActive && m.UserId == userId select a.Id).AnyAsync(ct)) return Conflict(new { message = "Không tự sửa vai trò đang được cấp cho chính mình." });
  var codes = request.PermissionCodes.Distinct().ToArray();
  if (codes.Length > 200 || await db.Permissions.CountAsync(p => p.IsActive && codes.Contains(p.Code), ct) != codes.Length) return BadRequest();
  var old = await db.RolePermissions.Where(p => p.RoleId == id).ToListAsync(ct);
  db.RolePermissions.RemoveRange(old.Where(p => !codes.Contains(p.PermissionCode)));
  foreach (var code in codes.Where(c => !old.Any(p => p.PermissionCode == c))) db.RolePermissions.Add(new RolePermission { TenantId = scope.TenantId!.Value, RoleId = id, PermissionCode = code });
  db.AuditEvents.Add(new AuditEvent { TenantId = scope.TenantId!.Value, ActorId = userId, Action = "role.permissions_changed", ResourceId = id, Reason = (string.IsNullOrWhiteSpace(request.Reason) ? "Cập nhật cấu hình phân quyền" : request.Reason.Trim()) });
  await db.SaveChangesAsync(ct); return NoContent();
 }
 public sealed record SaveRequest([Required, StringLength(200)] string Description, [Required, MaxLength(100)] string[] EndpointCodes, bool IsActive, [StringLength(300)] string? Reason = null);
 public sealed record RoleRequest([Required, MaxLength(200)] string[] PermissionCodes, [StringLength(300)] string? Reason = null);
}
