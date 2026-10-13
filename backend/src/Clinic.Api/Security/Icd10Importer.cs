using System.IO.Compression;
using System.Text.Json;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Security;

internal static class Icd10Importer
{
    private const string ResourceName = "Clinic.Api.Data.icd10-codes.json.gz";

    public static async Task ImportAsync(IServiceProvider services)
    {
        await using var resource = typeof(Icd10Importer).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("ICD-10 source data is missing from the application bundle.");
        await using var decompressed = new GZipStream(resource, CompressionMode.Decompress);
        var source = await JsonSerializer.DeserializeAsync<ImportDocument>(decompressed, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("ICD-10 source data could not be read.");
        Validate(source);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDataDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var current = await db.Icd10Catalogs.SingleOrDefaultAsync(x => x.Version == source.CatalogVersion);
        if (current is not null)
        {
            var count = await db.Icd10Codes.CountAsync(x => x.CatalogId == current.Id);
            if (count != source.Codes.Count)
                throw new InvalidOperationException("The existing ICD-10 catalog version has an unexpected row count; no changes were made.");
            Console.WriteLine($"ICD-10 catalog already imported: {count:N0} codes.");
            return;
        }

        var catalog = new Icd10Catalog
        {
            Version = source.CatalogVersion, SourceFileName = source.SourceFileName, SourceSheet = source.SourceSheet,
            DocumentTitle = source.DocumentTitle, DocumentNote = source.DocumentNote,
            SourceRecordCount = source.SourceRecordCount, DuplicateRowsConsolidated = source.DuplicateRowsConsolidated
        };
        db.Icd10Catalogs.Add(catalog);
        db.Icd10Codes.AddRange(source.Codes.Select(x => new Icd10Code
        {
            CatalogId = catalog.Id, Code = x.Code, NormalizedCode = x.NormalizedCode,
            ChapterNumber = x.ChapterNumber, ChapterCode = x.ChapterCode, ChapterNameEn = x.ChapterNameEn, ChapterName = x.ChapterName,
            MainGroupCode = x.MainGroupCode, MainGroupNameEn = x.MainGroupNameEn, MainGroupName = x.MainGroupName,
            SubGroup1Code = x.SubGroup1Code, SubGroup1NameEn = x.SubGroup1NameEn, SubGroup1Name = x.SubGroup1Name,
            SubGroup2Code = x.SubGroup2Code, SubGroup2NameEn = x.SubGroup2NameEn, SubGroup2Name = x.SubGroup2Name,
            CategoryCode = x.CategoryCode, CategoryNameEn = x.CategoryNameEn, CategoryName = x.CategoryName,
            DiseaseNameEn = x.DiseaseNameEn, DiseaseName = x.DiseaseName, Note = x.Note,
            SourceUpdatedAt = x.SourceUpdatedAt, SourceRows = x.SourceRows
        }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine($"Imported ICD-10 catalog {source.CatalogVersion}: {source.Codes.Count:N0} codes from {source.SourceRecordCount:N0} source rows.");
    }

    private static void Validate(ImportDocument source)
    {
        if (string.IsNullOrWhiteSpace(source.CatalogVersion) || string.IsNullOrWhiteSpace(source.SourceFileName) ||
            source.SourceSheet != "ICD10" || source.SourceRecordCount != 12219 || source.DuplicateRowsConsolidated != 1 ||
            source.Codes.Count != 12218 || source.Codes.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != source.Codes.Count ||
            source.Codes.Select(x => x.ChapterCode).Distinct(StringComparer.Ordinal).Count() != 22 ||
            source.Codes.Any(x => string.IsNullOrWhiteSpace(x.Code) || string.IsNullOrWhiteSpace(x.NormalizedCode) ||
                                  string.IsNullOrWhiteSpace(x.DiseaseName) || x.SourceRows.Length == 0))
            throw new InvalidOperationException("ICD-10 source validation failed (expected 22 chapters and 12,218 unique disease codes from 12,219 source rows).");
    }

    private sealed record ImportDocument(string SourceFileName, string CatalogVersion, string DocumentTitle, string DocumentNote,
        string SourceSheet, int SourceRecordCount, int DuplicateRowsConsolidated, List<CodeRow> Codes);
    private sealed record CodeRow(string Code, string NormalizedCode, string? ChapterNumber, string? ChapterCode, string? ChapterNameEn, string? ChapterName,
        string? MainGroupCode, string? MainGroupNameEn, string? MainGroupName, string? SubGroup1Code, string? SubGroup1NameEn, string? SubGroup1Name,
        string? SubGroup2Code, string? SubGroup2NameEn, string? SubGroup2Name, string? CategoryCode, string? CategoryNameEn, string? CategoryName,
        string? DiseaseNameEn, string DiseaseName, string? Note, string? SourceUpdatedAt, int[] SourceRows);
}
