namespace Clinic.Domain.Entities;

/// <summary>A versioned global ICD-10 reference catalog imported from a supplied workbook.</summary>
public sealed class Icd10Catalog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Version { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceSheet { get; set; } = string.Empty;
    public string? DocumentTitle { get; set; }
    public string? DocumentNote { get; set; }
    public int SourceRecordCount { get; set; }
    public int DuplicateRowsConsolidated { get; set; }
    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Icd10Code> Codes { get; set; } = new List<Icd10Code>();
}
