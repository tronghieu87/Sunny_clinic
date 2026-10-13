using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Clinic.Api.Security;
public sealed class MenuAccess(AuthorizationDbContext db, VerifiedTenant scope) {
 public IQueryable<Guid> Roles(Guid userId, bool tenantWide) =>
  from a in db.RoleAssignments join m in db.Memberships on new { a.TenantId, Id = a.MembershipId } equals new { m.TenantId, m.Id }
  join r in db.BusinessRoles on new { a.TenantId, Id = a.RoleId } equals new { r.TenantId, r.Id }
  where m.UserId == userId && m.IsActive && a.IsActive && r.IsActive && (a.BranchId == null || (!tenantWide && a.BranchId == scope.BranchId)) select r.Id;
 public async Task<HashSet<Guid>> ActiveMenus(CancellationToken ct) {
  var menus = await db.PlatformMenus.AsNoTracking().ToDictionaryAsync(m => m.Id, ct);
  bool Valid(Guid id) { var seen = new HashSet<Guid>(); while (true) { if (!seen.Add(id) || !menus.TryGetValue(id, out var m) || !m.IsActive) return false; if (m.ParentId is not Guid parent) return true; id = parent; } }
  return menus.Keys.Where(Valid).ToHashSet();
 }
 public async Task<bool> Allows(Guid userId, string endpoint, bool tenantWide, CancellationToken ct) {
  if (scope.TenantId is null || scope.BranchId is null) return false;
  var roles = Roles(userId, tenantWide); var active = (await ActiveMenus(ct)).ToArray();
  return await (from g in db.TenantRoleMenuGrants join e in db.PlatformMenuEndpoints on new { g.MenuId, g.OperationCode } equals new { e.MenuId, e.OperationCode }
   join p in db.PlatformOperations on g.OperationCode equals p.Code
   where roles.Contains(g.RoleId) && active.Contains(g.MenuId) && p.IsActive && e.EndpointCode == endpoint select g.MenuId).AnyAsync(ct);
 }
}
