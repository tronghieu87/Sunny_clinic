namespace Clinic.Domain.Entities;

/// <summary>A selectable value in a global or tenant-scoped category group.</summary>
public sealed class CategoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid CategoryGroupId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public CategoryGroup? CategoryGroup { get; set; }
}
