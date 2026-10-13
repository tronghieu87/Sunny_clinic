namespace Clinic.Domain.Entities;
public sealed class MenuItem {
 public Guid Id { get; set; } = Guid.NewGuid(); public Guid TenantId { get; set; }
 public Guid? ParentId { get; set; } public string Code { get; set; } = "";
 public string Name { get; set; } = ""; public string? Path { get; set; }
 public string Kind { get; set; } = "function";
 public string? BaseApi { get; set; }
 public string? Description { get; set; }
 public int SortOrder { get; set; } public bool IsActive { get; set; } = true;
}
public sealed class MenuOperation { public Guid TenantId { get; set; } public Guid MenuId { get; set; } public string PermissionCode { get; set; } = ""; }
public sealed class RoleMenuOperation { public Guid TenantId { get; set; } public Guid RoleId { get; set; } public Guid MenuId { get; set; } public string PermissionCode { get; set; } = ""; }
public sealed class MenuOperationEndpoint { public Guid TenantId { get; set; } public Guid MenuId { get; set; } public string PermissionCode { get; set; } = ""; public string EndpointCode { get; set; } = ""; }
