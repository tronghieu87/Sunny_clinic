namespace Clinic.Domain.Entities;

/// <summary>A global standard dictionary or a tenant-owned configurable dictionary.</summary>
public sealed class CategoryGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public bool IsSystem { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<CategoryItem> Items { get; set; } = new List<CategoryItem>();
}
