namespace Clinic.Domain.Entities;

// Software-wide definitions. Only role grants carry a tenant boundary.
public sealed class PlatformOperation
{
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; }
}
public sealed class PlatformMenu
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Path { get; set; }
    public string Kind { get; set; } = "function";
    public string? BaseApi { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class PlatformMenuOperation
{
    public Guid MenuId { get; set; }
    public string OperationCode { get; set; } = "";
}
public sealed class PlatformMenuEndpoint
{
    public Guid MenuId { get; set; }
    public string OperationCode { get; set; } = "";
    public string EndpointCode { get; set; } = "";
}
public sealed class TenantRoleMenuGrant
{
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public Guid MenuId { get; set; }
    public string OperationCode { get; set; } = "";
}
