using Microsoft.EntityFrameworkCore;
using Clinic.Domain.Entities;
using Clinic.Application;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
namespace Clinic.Api.Security;
internal static class LocalSetup {
 public static async Task Run(IServiceProvider services, bool superAdmin = false) {
  if (Console.IsInputRedirected) throw new InvalidOperationException("Run local setup in an interactive terminal.");
  Console.Write("Tên đơn vị: "); var name = Console.ReadLine()?.Trim();
  Console.Write("Tên cơ sở: "); var branchName = Console.ReadLine()?.Trim();
  Console.Write("Tên đăng nhập: "); var username = Console.ReadLine()?.Trim();
  if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || string.IsNullOrWhiteSpace(branchName) || branchName.Length > 200 || string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException("Invalid setup information.");
  Console.Write("Mật khẩu (không hiển thị): ");
  var password = new System.Text.StringBuilder();
  while (true) { var key = Console.ReadKey(true); if (key.Key == ConsoleKey.Enter) break; if (key.Key == ConsoleKey.Backspace) { if (password.Length > 0) password.Length--; } else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar); }
  Console.WriteLine();
  using var scope = services.CreateScope();
  var tenantId = Guid.NewGuid(); var branchId = Guid.NewGuid();
  scope.ServiceProvider.GetRequiredService<VerifiedTenant>().Set(tenantId, branchId);
  var db = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
  var manager = scope.ServiceProvider.GetRequiredService<UserManager<ClinicUser>>();
  await using var transaction = await db.Database.BeginTransactionAsync();
  var user = new ClinicUser { Id = Guid.NewGuid(), UserName = username, LockoutEnabled = true, IsSuperAdmin = superAdmin };
  var result = await manager.CreateAsync(user, password.ToString()); password.Clear();
  if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
  db.Tenants.Add(new Tenant { Id = tenantId, Code = "LOCAL-" + tenantId.ToString("N"), Name = name });
  db.Branches.Add(new Branch { Id = branchId, TenantId = tenantId, Code = "MAIN", Name = branchName });
  var membership = new TenantMembership { TenantId = tenantId, UserId = user.Id, AllBranches = true };
  db.Memberships.Add(membership);
  foreach (var item in PermissionCatalog.All) db.Permissions.Add(new Permission { TenantId = tenantId, Code = item.Key, Description = item.Value, IsSystem = true });
  db.PermissionEndpoints.AddRange(new PermissionEndpoint { TenantId = tenantId, PermissionCode = "organization.read", EndpointCode = "organization.details" }, new PermissionEndpoint { TenantId = tenantId, PermissionCode = "branches.read", EndpointCode = "organization.branches" });
  var role = new BusinessRole { TenantId = tenantId, Code = "MANAGER", Name = "Quản lý đơn vị" };
  db.BusinessRoles.Add(role);
  foreach (var code in PermissionCatalog.Management) db.RolePermissions.Add(new RolePermission { TenantId = tenantId, RoleId = role.Id, PermissionCode = code });
  db.RoleAssignments.Add(new RoleAssignment { TenantId = tenantId, MembershipId = membership.Id, RoleId = role.Id });
  if (superAdmin) db.AuditEvents.Add(new AuditEvent { TenantId = tenantId, ActorId = user.Id, Action = "system.super_admin_created", ResourceId = user.Id, Reason = "Explicit interactive local bootstrap" });
  await db.SaveChangesAsync(); await transaction.CommitAsync();
  Console.WriteLine(superAdmin ? "Đã tạo tài khoản super admin và cơ sở ban đầu." : "Đã tạo đơn vị, cơ sở và tài khoản local. Có thể đăng nhập trên giao diện.");
 }
 public static async Task Promote(IServiceProvider services, string username) {
  using var provisioning = services.CreateScope(); var db = provisioning.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
  await using var transaction = await db.Database.BeginTransactionAsync();
  var user = await db.Users.SingleOrDefaultAsync(u => u.UserName == username && u.IsActive) ?? throw new InvalidOperationException("Exact active account not found.");
  var place = await (from m in db.Memberships.IgnoreQueryFilters() join b in db.Branches.IgnoreQueryFilters() on m.TenantId equals b.TenantId join t in db.Tenants.IgnoreQueryFilters() on m.TenantId equals t.Id where m.UserId == user.Id && m.IsActive && b.IsActive && t.IsActive select new { m.TenantId, BranchId = b.Id }).FirstOrDefaultAsync() ?? throw new InvalidOperationException("Active workplace required for audited promotion.");
  provisioning.ServiceProvider.GetRequiredService<VerifiedTenant>().Set(place.TenantId, place.BranchId);
  if (!user.IsSuperAdmin) { user.IsSuperAdmin = true; db.AuditEvents.Add(new AuditEvent { TenantId = place.TenantId, ActorId = Guid.Empty, ResourceId = user.Id, Action = "system.super_admin_promoted", Reason = "Explicit owner-authorized local console promotion" }); await db.SaveChangesAsync(); }
  await transaction.CommitAsync(); Console.WriteLine("Requested account has super-admin access.");
 }

}
