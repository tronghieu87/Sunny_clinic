namespace Clinic.Domain.Entities;

/// <summary>A versioned, system-wide administrative reference catalog imported from a supplied source file.</summary>
public sealed class AdministrativeUnitCatalog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Version { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<AdministrativeUnit> Units { get; set; } = new List<AdministrativeUnit>();
}
