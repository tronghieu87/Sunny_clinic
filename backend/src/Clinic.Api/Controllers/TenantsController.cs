using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Api.Security;
using Clinic.Application;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Clinic.Api.Controllers;
[ControlPlane, ApiController, Route("platform/tenants"), Authorize]
public sealed class TenantsController(AuthorizationDbContext db, SuperAdminAccess admin, IServiceScopeFactory factory) : ControllerBase {
 [HttpGet]
 public async Task<IActionResult> List(CancellationToken ct) => !admin.IsSuperAdmin ? Forbid() : Ok(await db.Tenants.IgnoreQueryFilters().AsNoTracking().OrderBy(t => t.Name).Select(t => new { t.Id, t.Code, t.Name, t.IsActive }).ToListAsync(ct));
 [HttpPost]
 public async Task<IActionResult> Create(CreateRequest request, CancellationToken ct) {
  if (!admin.IsSuperAdmin) return Forbid();
  if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest();
  using var provisioning = factory.CreateScope(); var tenant = new Tenant { Code = GenerateCode(), Name = request.Name.Trim() };
  var verified = provisioning.ServiceProvider.GetRequiredService<VerifiedTenant>();
  verified.Set(tenant.Id, Guid.Empty, true);
  var target = provisioning.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
  target.Tenants.Add(tenant);
  foreach (var p in PermissionCatalog.All) target.Permissions.Add(new Permission { TenantId = tenant.Id, Code = p.Key, Description = p.Value, IsSystem = true });
  target.PermissionEndpoints.AddRange(new PermissionEndpoint { TenantId = tenant.Id, PermissionCode = "organization.read", EndpointCode = "organization.details" }, new PermissionEndpoint { TenantId = tenant.Id, PermissionCode = "branches.read", EndpointCode = "organization.branches" });
  target.AuditEvents.Add(new AuditEvent { TenantId = tenant.Id, ActorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = "tenant.created", ResourceId = tenant.Id, Reason = "Tạo tenant" });
  try { await target.SaveChangesAsync(ct); }
  catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return Conflict(new { message = "Mã tenant đã tồn tại." }); }
  await EmployeePositionSetup.SyncTenant(provisioning.ServiceProvider.GetRequiredService<ClinicDataDbContext>(), verified, tenant.Id, ct);
  return Created("/platform/tenants", new { tenant.Id, tenant.Code, tenant.Name, tenant.IsActive });
 }
 private static string GenerateCode() => $"TEN-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
 public sealed record CreateRequest([Required, StringLength(200)] string Name, [StringLength(300)] string? Reason = null);
}
