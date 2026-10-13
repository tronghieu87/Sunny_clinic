using System.Text.Json;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Security;

internal static class AdministrativeUnitImporter
{
    private const string ResourceName = "Clinic.Api.Data.administrative-units.json";

    public static async Task ImportAsync(IServiceProvider services)
    {
        await using var stream = typeof(AdministrativeUnitImporter).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Administrative-unit source data is missing from the application bundle.");
        var source = await JsonSerializer.DeserializeAsync<ImportDocument>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Administrative-unit source data could not be read.");
        Validate(source);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDataDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var current = await db.AdministrativeUnitCatalogs.SingleOrDefaultAsync(x => x.Version == source.CatalogVersion);
        if (current is not null)
        {
            var count = await db.AdministrativeUnits.CountAsync(x => x.CatalogId == current.Id);
            if (count != source.Provinces.Count + source.Communes.Count)
                throw new InvalidOperationException("The existing catalog version has an unexpected row count; no changes were made.");
            Console.WriteLine($"Administrative catalog already imported: {source.Provinces.Count} provinces/cities and {source.Communes.Count} communes.");
            return;
        }

        var catalog = new AdministrativeUnitCatalog { Version = source.CatalogVersion, SourceFileName = source.SourceFileName };
        db.AdministrativeUnitCatalogs.Add(catalog);
        var parents = source.Provinces.ToDictionary(x => x.Code, x => new AdministrativeUnit
        {
            CatalogId = catalog.Id, Code = x.Code, Name = x.Name, UnitType = TypeOfProvince(x.Name), Level = 1,
            SortOrder = int.Parse(x.Code), LegacyProvinceCode = x.LegacyProvinceCode
        });
        db.AdministrativeUnits.AddRange(parents.Values);
        db.AdministrativeUnits.AddRange(source.Communes.Select(x => new AdministrativeUnit
        {
            CatalogId = catalog.Id, ParentId = parents[x.ProvinceCode].Id, Code = x.Code, Name = x.Name,
            UnitType = x.Name.StartsWith("Đặc khu ", StringComparison.OrdinalIgnoreCase) ? "Đặc khu" : x.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0], Level = 2, SortOrder = x.SortOrder,
            LegacyProvinceCode = x.LegacyProvinceCode, LegacyDistrictCode = x.LegacyDistrictCode,
            LegacyDistrictName = x.LegacyDistrictName, SourceRowNumber = x.Row, ReviewStatus = x.ReviewStatus
        }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine($"Imported administrative catalog {source.CatalogVersion}: {source.Provinces.Count} provinces/cities and {source.Communes.Count} communes.");
    }

    private static void Validate(ImportDocument source)
    {
        if (string.IsNullOrWhiteSpace(source.CatalogVersion) || string.IsNullOrWhiteSpace(source.SourceFileName) ||
            source.Provinces.Count != 34 || source.Communes.Count != 3321 ||
            source.Provinces.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != source.Provinces.Count ||
            source.Communes.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != source.Communes.Count ||
            source.Communes.Any(x => !source.Provinces.Any(p => p.Code == x.ProvinceCode) || x.SortOrder < 1 ||
                string.IsNullOrWhiteSpace(x.Code) || string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.LegacyDistrictCode)))
            throw new InvalidOperationException("Administrative source validation failed (expected 34 provinces/cities, 3,321 communes, unique codes and complete parent/source fields).");
    }

    private static string TypeOfProvince(string name) => name.StartsWith("Thành phố", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Tp ", StringComparison.OrdinalIgnoreCase) ? "Thành phố" : "Tỉnh";

    private sealed record ImportDocument(string SourceFileName, string CatalogVersion, List<ProvinceRow> Provinces, List<CommuneRow> Communes);
    private sealed record ProvinceRow(string Code, string Name, string LegacyProvinceCode);
    private sealed record CommuneRow(int Row, string ProvinceCode, string ProvinceName, string LegacyProvinceCode, string LegacyDistrictCode,
        string LegacyDistrictName, int SortOrder, string Code, string Name, string? ReviewStatus);
}
