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
[ControlPlane, ApiController, Route("organization/menus"), Authorize(Policy = "tenant:roles.manage")]
public sealed class MenusController(AuthorizationDbContext db, VerifiedTenant scope, SuperAdminAccess admin) : ControllerBase {
 Guid Tenant => scope.TenantId!.Value;
 void Audit(string action, Guid id, string? reason) => db.AuditEvents.Add(new AuditEvent { TenantId = Tenant, ActorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = action, ResourceId = id, Reason = string.IsNullOrWhiteSpace(reason) ? action : reason.Trim() });
 [HttpGet]
 public async Task<IActionResult> List(CancellationToken ct) {
  var items = await db.PlatformMenus.AsNoTracking().OrderBy(m => m.SortOrder).ThenBy(m => m.Name).ToListAsync(ct);
  var ops = await db.PlatformMenuOperations.AsNoTracking().ToListAsync(ct);
  return Ok(items.Select(m => new { m.Id, m.Code, m.Name, m.ParentId, m.Path, m.Kind, m.BaseApi, m.Description, m.SortOrder, m.IsActive, operations = ops.Where(o => o.MenuId == m.Id).Select(o => o.OperationCode) }));
 }
 [HttpPut("{id:guid}")]
 public async Task<IActionResult> Save(Guid id, SaveRequest request, CancellationToken ct) {
  if (!admin.IsSuperAdmin) return Forbid();
  if (id == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || !System.Text.RegularExpressions.Regex.IsMatch(request.Code, "^[a-zA-Z][a-zA-Z0-9_.-]{0,99}$")) return BadRequest();
  var path = string.IsNullOrWhiteSpace(request.Path) ? null : request.Path.Trim();
  if (request.Kind is not ("menu" or "function") || request.SortOrder < 0) return BadRequest(new { message = "Loại chức năng hoặc thứ tự không hợp lệ." });
  var baseApi = string.IsNullOrWhiteSpace(request.BaseApi) ? null : request.BaseApi.Trim();
  if (baseApi is not null && (!baseApi.StartsWith('/') || baseApi.StartsWith("//") || baseApi.Any(char.IsWhiteSpace))) return BadRequest(new { message = "API gốc phải là đường dẫn nội bộ bắt đầu bằng /." });
  if (path is not null && (!System.Text.RegularExpressions.Regex.IsMatch(path, "^/[a-zA-Z0-9/_-]*$") || path.StartsWith("//") || path == "/permissions" || path.StartsWith("/permissions/"))) return BadRequest(new { message = "Path phải là đường dẫn nội bộ; không được ghi đè trang quản trị phân quyền." });
  await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  var items = await db.PlatformMenus.ToListAsync(ct); var item = items.SingleOrDefault(m => m.Id == id);
  if (request.ParentId is Guid parent) {
   var seen = new HashSet<Guid> { id }; Guid? cursor = parent;
   while (cursor is Guid current) { var ancestor = items.SingleOrDefault(m => m.Id == current); if (!seen.Add(current) || ancestor is null) return BadRequest(new { message = "Menu cha không hợp lệ hoặc tạo vòng lặp." }); cursor = ancestor.ParentId; }
  }
  var codes = request.OperationCodes.Distinct().ToArray();
  if (await db.PlatformOperations.CountAsync(p => codes.Contains(p.Code) && p.IsActive && !p.IsSystem, ct) != codes.Length) return BadRequest(new { message = "Chọn thao tác đang hoạt động trong khung hệ thống." });
  var old = await db.PlatformMenuOperations.Where(o => o.MenuId == id).ToListAsync(ct); var removed = old.Where(o => !codes.Contains(o.OperationCode)).Select(o => o.OperationCode).ToArray();
  if (await db.TenantRoleMenuGrants.IgnoreQueryFilters().AnyAsync(o => o.MenuId == id && removed.Contains(o.OperationCode), ct) || await db.PlatformMenuEndpoints.AnyAsync(o => o.MenuId == id && removed.Contains(o.OperationCode), ct)) return Conflict(new { message = "Thao tác đang được gán nhóm hoặc API. Gỡ các liên kết trước khi bỏ thao tác." });
  if (item is null) { item = new PlatformMenu { Id = id }; db.PlatformMenus.Add(item); }
  item.Code = request.Code; item.Name = request.Name.Trim(); item.ParentId = request.ParentId; item.Path = path; item.SortOrder = request.SortOrder; item.IsActive = request.IsActive;
  item.Kind = request.Kind; item.BaseApi = baseApi; item.Description = request.Description?.Trim();
  db.PlatformMenuOperations.RemoveRange(old.Where(o => removed.Contains(o.OperationCode)));
  foreach (var code in codes.Where(c => !old.Any(o => o.OperationCode == c))) db.PlatformMenuOperations.Add(new PlatformMenuOperation { MenuId = id, OperationCode = code });
  Audit("menu.configured", id, string.IsNullOrWhiteSpace(request.Reason) ? "Lưu chức năng" : request.Reason); return await Commit(tx, ct);
 }
 [HttpDelete("{id:guid}")]
 public async Task<IActionResult> Delete(Guid id, ReasonRequest request, CancellationToken ct) {
  if (!admin.IsSuperAdmin) return Forbid();
  var item = await db.PlatformMenus.SingleOrDefaultAsync(m => m.Id == id, ct); if (item is null) return NotFound();
  item.IsActive = false; Audit("menu.deactivated", id, request.Reason); await db.SaveChangesAsync(ct); return NoContent();
 }
 [HttpGet("roles/{id:guid}")]
 public async Task<IActionResult> Grants(Guid id, CancellationToken ct) {
  if (!await db.BusinessRoles.AnyAsync(r => r.Id == id, ct)) return NotFound();
  return Ok(await db.TenantRoleMenuGrants.AsNoTracking().Where(g => g.RoleId == id).Select(g => new { g.MenuId, PermissionCode = g.OperationCode }).ToListAsync(ct));
 }
 [HttpPut("roles/{id:guid}")]
 public async Task<IActionResult> Grants(Guid id, GrantRequest request, CancellationToken ct) {
  if (!await db.BusinessRoles.AnyAsync(r => r.Id == id && r.IsActive, ct)) return NotFound();
  var user = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
  if (await (from a in db.RoleAssignments join m in db.Memberships on a.MembershipId equals m.Id where a.RoleId == id && a.IsActive && m.UserId == user select a.Id).AnyAsync(ct)) return Conflict(new { message = "Không tự sửa nhóm đang được cấp cho chính mình." });
  var desired = request.Grants.Distinct().ToArray();
  var valid = await (from o in db.PlatformMenuOperations join m in db.PlatformMenus on o.MenuId equals m.Id join p in db.PlatformOperations on o.OperationCode equals p.Code where m.IsActive && p.IsActive select new Grant(o.MenuId, o.OperationCode)).ToListAsync(ct);
  if (desired.Any(g => !valid.Contains(g))) return BadRequest(new { message = "Chức năng hoặc thao tác không hợp lệ." });
  var old = await db.TenantRoleMenuGrants.Where(g => g.RoleId == id).ToListAsync(ct);
  db.TenantRoleMenuGrants.RemoveRange(old.Where(g => !desired.Contains(new Grant(g.MenuId, g.OperationCode))));
  foreach (var g in desired.Where(g => !old.Any(o => o.MenuId == g.MenuId && o.OperationCode == g.PermissionCode))) db.TenantRoleMenuGrants.Add(new TenantRoleMenuGrant { TenantId = Tenant, RoleId = id, MenuId = g.MenuId, OperationCode = g.PermissionCode });
  Audit("role.menu_grants_changed", id, request.Reason); await db.SaveChangesAsync(ct); return NoContent();
 }
 [HttpGet("{id:guid}/endpoint-config")]
 public async Task<IActionResult> EndpointConfig(Guid id, CancellationToken ct) {
  if (!await db.PlatformMenus.AnyAsync(m => m.Id == id, ct)) return NotFound();
  var operations = await db.PlatformMenuOperations.Where(o => o.MenuId == id).Select(o => o.OperationCode).ToListAsync(ct);
  var mappings = await db.PlatformMenuEndpoints.AsNoTracking().Where(o => o.MenuId == id).ToListAsync(ct);
  return Ok(operations.Select(code => new { permissionCode = code, endpointCodes = mappings.Where(m => m.OperationCode == code).Select(m => m.EndpointCode).ToArray() }));
 }
 [HttpPut("{id:guid}/endpoint-config")]
 public async Task<IActionResult> EndpointConfig(Guid id, EndpointConfigRequest request, CancellationToken ct) {
  if (!admin.IsSuperAdmin) return Forbid();
  await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  if (!await db.PlatformMenus.AnyAsync(m => m.Id == id, ct)) return NotFound();
  var operations = await db.PlatformMenuOperations.Where(o => o.MenuId == id).Select(o => o.OperationCode).ToListAsync(ct);
  if (request.Operations.Select(o => o.PermissionCode).Distinct().Count() != request.Operations.Length || !operations.Order().SequenceEqual(request.Operations.Select(o => o.PermissionCode).Order())) return Conflict(new { message = "Danh sách thao tác đã thay đổi. Tải lại chức năng trước khi lưu." });
  var codes = request.Operations.SelectMany(o => o.EndpointCodes).Distinct().ToArray();
  if (await db.ApiEndpoints.CountAsync(e => e.IsActive && codes.Contains(e.Code), ct) != codes.Length) return BadRequest(new { message = "Chỉ chọn API nghiệp vụ đã đăng ký." });
  var old = await db.PlatformMenuEndpoints.Where(o => o.MenuId == id).ToListAsync(ct);
  var desired = request.Operations.SelectMany(o => o.EndpointCodes.Distinct().Select(code => (Operation: o.PermissionCode, Endpoint: code))).ToHashSet();
  db.PlatformMenuEndpoints.RemoveRange(old.Where(o => !desired.Contains((o.OperationCode, o.EndpointCode))));
  foreach (var pair in desired.Where(p => !old.Any(o => o.OperationCode == p.Operation && o.EndpointCode == p.Endpoint))) db.PlatformMenuEndpoints.Add(new PlatformMenuEndpoint { MenuId = id, OperationCode = pair.Operation, EndpointCode = pair.Endpoint });
  Audit("menu.operation_apis_changed", id, request.Reason);
  return await Commit(tx, ct);
 }
 [HttpGet("{id:guid}/operations/{code}/endpoints")]
 public async Task<IActionResult> Endpoints(Guid id, string code, CancellationToken ct) {
  if (!await db.PlatformMenuOperations.AnyAsync(m => m.MenuId == id && m.OperationCode == code, ct)) return NotFound();
  return Ok(await db.PlatformMenuEndpoints.Where(m => m.MenuId == id && m.OperationCode == code).Select(m => m.EndpointCode).ToArrayAsync(ct));
 }
 [HttpPut("{id:guid}/operations/{code}/endpoints")]
 public async Task<IActionResult> Endpoints(Guid id, string code, EndpointRequest request, CancellationToken ct) {
  if (!admin.IsSuperAdmin) return Forbid();
  if (!await db.PlatformMenuOperations.AnyAsync(m => m.MenuId == id && m.OperationCode == code, ct)) return NotFound();
  var codes = request.EndpointCodes.Distinct().ToArray();
  if (await db.ApiEndpoints.CountAsync(e => e.IsActive && codes.Contains(e.Code), ct) != codes.Length) return BadRequest(new { message = "Chỉ chọn API nghiệp vụ đã đăng ký." });
  var old = await db.PlatformMenuEndpoints.Where(m => m.MenuId == id && m.OperationCode == code).ToListAsync(ct);
  db.PlatformMenuEndpoints.RemoveRange(old.Where(m => !codes.Contains(m.EndpointCode)));
  foreach (var endpoint in codes.Where(e => !old.Any(o => o.EndpointCode == e))) db.PlatformMenuEndpoints.Add(new PlatformMenuEndpoint { MenuId = id, OperationCode = code, EndpointCode = endpoint });
  Audit("menu.operation_apis_changed", id, code + ": " + request.Reason); await db.SaveChangesAsync(ct); return NoContent();
 }
 async Task<IActionResult> Commit(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, CancellationToken ct) {
  try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return NoContent(); }
  catch (Exception e) when (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure } || e is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure } }) { return Conflict(new { message = "Mã/path đã tồn tại hoặc dữ liệu vừa thay đổi. Tải lại và thử lại." }); }
 }
 public sealed record SaveRequest([Required, StringLength(100)] string Code, [Required, StringLength(200)] string Name, Guid? ParentId, [StringLength(300)] string? Path, int SortOrder, bool IsActive, [Required, MaxLength(200)] string[] OperationCodes, [StringLength(300)] string? Reason = null, [Required, StringLength(20)] string Kind = "function", [StringLength(300)] string? BaseApi = null, [StringLength(1000)] string? Description = null);
 public sealed record ReasonRequest([StringLength(300)] string? Reason = null);
 public sealed record Grant(Guid MenuId, string PermissionCode);
 public sealed record GrantRequest([Required, MaxLength(500)] Grant[] Grants, [StringLength(300)] string? Reason = null);
 public sealed record EndpointRequest([Required, MaxLength(100)] string[] EndpointCodes, [StringLength(300)] string? Reason = null);
 public sealed record OperationEndpoints([Required, StringLength(100)] string PermissionCode, [Required, MaxLength(100)] string[] EndpointCodes);
 public sealed record EndpointConfigRequest([Required, MaxLength(200)] OperationEndpoints[] Operations, [StringLength(300)] string? Reason = null);
}
