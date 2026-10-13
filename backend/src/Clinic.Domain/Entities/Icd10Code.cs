namespace Clinic.Domain.Entities;

/// <summary>A billable or selectable diagnosis code and its source hierarchy.</summary>
public sealed class Icd10Code
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CatalogId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public string? ChapterNumber { get; set; }
    public string? ChapterCode { get; set; }
    public string? ChapterNameEn { get; set; }
    public string? ChapterName { get; set; }
    public string? MainGroupCode { get; set; }
    public string? MainGroupNameEn { get; set; }
    public string? MainGroupName { get; set; }
    public string? SubGroup1Code { get; set; }
    public string? SubGroup1NameEn { get; set; }
    public string? SubGroup1Name { get; set; }
    public string? SubGroup2Code { get; set; }
    public string? SubGroup2NameEn { get; set; }
    public string? SubGroup2Name { get; set; }
    public string? CategoryCode { get; set; }
    public string? CategoryNameEn { get; set; }
    public string? CategoryName { get; set; }
    public string? DiseaseNameEn { get; set; }
    public string DiseaseName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? SourceUpdatedAt { get; set; }
    public int[] SourceRows { get; set; } = [];
    public Icd10Catalog Catalog { get; set; } = null!;
}
