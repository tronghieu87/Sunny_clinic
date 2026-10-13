namespace Clinic.Domain.Entities;

/// <summary>A province/city or commune/ward/special zone in a global, versioned catalog.</summary>
public sealed class AdministrativeUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CatalogId { get; set; }
    public Guid? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty;
    public short Level { get; set; }
    public int SortOrder { get; set; }
    public string? LegacyProvinceCode { get; set; }
    public string? LegacyDistrictCode { get; set; }
    public string? LegacyDistrictName { get; set; }
    public int? SourceRowNumber { get; set; }
    public string? ReviewStatus { get; set; }
    public AdministrativeUnitCatalog Catalog { get; set; } = null!;
    public AdministrativeUnit? Parent { get; set; }
    public ICollection<AdministrativeUnit> Children { get; set; } = new List<AdministrativeUnit>();
}
