using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Clinic.Api.Security;
public sealed class AccessResolver(AuthorizationDbContext db) {
 public async Task<bool> CanAccess(Guid userId, Guid tenantId, Guid branchId, CancellationToken ct) {
  // These narrow queries intentionally bypass tenant filters to verify membership before establishing scope.
  var membership = await db.Memberships.IgnoreQueryFilters().AsNoTracking()
   .SingleOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId && m.IsActive, ct);
  var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
  if (user is null || (!user.IsSuperAdmin && membership is null)) return false;
  if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId && t.IsActive, ct)) return false;
  if (!await db.Branches.IgnoreQueryFilters().AnyAsync(b => b.Id == branchId && b.TenantId == tenantId && b.IsActive, ct)) return false;
  return user.IsSuperAdmin || membership!.AllBranches || await db.BranchAccesses.IgnoreQueryFilters()
   .AnyAsync(a => a.TenantId == tenantId && a.MembershipId == membership.Id && a.BranchId == branchId, ct);
 }
}
