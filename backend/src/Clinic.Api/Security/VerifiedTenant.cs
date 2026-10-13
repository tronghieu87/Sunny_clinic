using Clinic.Application;
namespace Clinic.Api.Security;
public sealed class VerifiedTenant : ICurrentTenant {
 public Guid? TenantId { get; private set; }
 public Guid? BranchId { get; private set; }
 public bool IsSuperAdmin { get; private set; }
 internal void Set(Guid tenantId, Guid branchId, bool isSuperAdmin = false) { TenantId = tenantId; BranchId = branchId; IsSuperAdmin = isSuperAdmin; }
}
