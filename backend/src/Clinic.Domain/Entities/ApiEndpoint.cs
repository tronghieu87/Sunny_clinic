namespace Clinic.Domain.Entities;
public sealed class ApiEndpoint {
 public string Code { get; set; } = string.Empty; public string Method { get; set; } = string.Empty;
 public string Route { get; set; } = string.Empty; public string Description { get; set; } = string.Empty;
 public bool TenantWide { get; set; } public bool IsActive { get; set; } = true;
}
public sealed class PermissionEndpoint { public Guid TenantId { get; set; } public string PermissionCode { get; set; } = string.Empty; public string EndpointCode { get; set; } = string.Empty; }
