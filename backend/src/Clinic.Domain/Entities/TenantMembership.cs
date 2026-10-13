namespace Clinic.Domain.Entities;
public sealed class TenantMembership {
 public Guid Id { get; set; } = Guid.NewGuid();
 public Guid TenantId { get; set; }
 public Guid UserId { get; set; }
 public bool IsActive { get; set; } = true;
 public bool AllBranches { get; set; }
}
public sealed class BranchAccess {
 public Guid Id { get; set; } = Guid.NewGuid();
 public Guid TenantId { get; set; }
 public Guid MembershipId { get; set; }
 public Guid BranchId { get; set; }
}
