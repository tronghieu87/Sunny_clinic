namespace Clinic.Domain.Entities;
public sealed class BusinessRole {
 public Guid Id { get; set; } = Guid.NewGuid(); public Guid TenantId { get; set; }
 public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty;
 public bool IsActive { get; set; } = true;
}
public sealed class Permission { public Guid TenantId { get; set; } public bool IsSystem { get; set; } public bool IsActive { get; set; } = true; public string Code { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; }
public sealed class RolePermission {
 public Guid TenantId { get; set; } public Guid RoleId { get; set; } public string PermissionCode { get; set; } = string.Empty;
}
public sealed class RoleAssignment {
 public Guid Id { get; set; } = Guid.NewGuid(); public Guid TenantId { get; set; }
 public Guid MembershipId { get; set; } public Guid RoleId { get; set; }
 public Guid? BranchId { get; set; } public bool IsActive { get; set; } = true;
}
