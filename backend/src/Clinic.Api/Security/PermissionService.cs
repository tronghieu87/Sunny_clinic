using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Clinic.Api.Security;
public sealed class PermissionService(AuthorizationDbContext db, VerifiedTenant scope) {
 public async Task<string[]> Get(Guid userId, bool tenantWide, CancellationToken ct) {
  if (scope.TenantId is null || scope.BranchId is null) return [];
  return await (from a in db.RoleAssignments.AsNoTracking()
   join m in db.Memberships on new { a.TenantId, Id = a.MembershipId } equals new { m.TenantId, m.Id }
   join r in db.BusinessRoles on new { a.TenantId, Id = a.RoleId } equals new { r.TenantId, r.Id }
   join p in db.RolePermissions on new { a.TenantId, Id = a.RoleId } equals new { p.TenantId, Id = p.RoleId }
   join permission in db.Permissions on new { p.TenantId, Code = p.PermissionCode } equals new { permission.TenantId, permission.Code }
   where permission.IsActive && m.UserId == userId && m.IsActive && a.IsActive && r.IsActive &&
     (a.BranchId == null || (!tenantWide && a.BranchId == scope.BranchId))
   select p.PermissionCode).Distinct().ToArrayAsync(ct);
 }
}
