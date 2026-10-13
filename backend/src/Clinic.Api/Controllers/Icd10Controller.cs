using Clinic.Api.Security;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Controllers;

[ApiController, Route("master-data/icd10"), Authorize]
public sealed class Icd10Controller(ClinicDataDbContext db) : ControllerBase
{
    [HttpGet("catalog")]
    [BusinessApi("icd10.catalog", "Thông tin bộ danh mục ICD-10")]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        var catalog = await db.Icd10Catalogs.AsNoTracking().OrderByDescending(x => x.ImportedAtUtc).FirstOrDefaultAsync(ct);
        if (catalog is null) return NotFound(new { message = "Chưa có dữ liệu ICD-10." });
        var codeCount = await db.Icd10Codes.CountAsync(x => x.CatalogId == catalog.Id, ct);
        var chapterCount = await db.Icd10Codes.Where(x => x.CatalogId == catalog.Id).Select(x => x.ChapterCode).Distinct().CountAsync(ct);
        return Ok(new { catalog.Version, catalog.SourceFileName, catalog.SourceSheet, catalog.DocumentTitle,
            catalog.DocumentNote, catalog.SourceRecordCount, catalog.DuplicateRowsConsolidated,
            codeCount, chapterCount, catalog.ImportedAtUtc });
    }

    [HttpGet("chapters")]
    [BusinessApi("icd10.chapters.list", "Danh sách chương ICD-10")]
    public async Task<IActionResult> Chapters(CancellationToken ct)
    {
        var catalogId = await CurrentCatalogId(ct);
        if (catalogId is null) return NotFound(new { message = "Chưa có dữ liệu ICD-10." });
        var rows = await db.Icd10Codes.AsNoTracking().Where(x => x.CatalogId == catalogId)
            .GroupBy(x => new { x.ChapterNumber, x.ChapterCode, x.ChapterName })
            .Select(group => new { group.Key.ChapterNumber, group.Key.ChapterCode, group.Key.ChapterName, codeCount = group.Count() })
            .ToListAsync(ct);
        return Ok(rows.OrderBy(x => RomanOrder(x.ChapterNumber)).ToList());
    }

    [HttpGet("codes")]
    [BusinessApi("icd10.codes.search", "Tra cứu mã bệnh ICD-10")]
    public async Task<IActionResult> Search([FromQuery] string? search, [FromQuery] string? chapterCode,
        [FromQuery] string? code, [FromQuery] string? diseaseName, [FromQuery] string? categoryCode,
        [FromQuery] string? mainGroupName, [FromQuery] int page = 1, [FromQuery] int pageSize = 200, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        if (new[] { search, code, diseaseName, categoryCode, mainGroupName }.Any(x => x?.Trim().Length > 100))
            return BadRequest(new { message = "Điều kiện tìm kiếm tối đa 100 ký tự." });
        if (chapterCode?.Trim().Length > 20) return BadRequest(new { message = "Mã chương không hợp lệ." });
        var catalogId = await CurrentCatalogId(ct);
        if (catalogId is null) return NotFound(new { message = "Chưa có dữ liệu ICD-10." });
        var query = db.Icd10Codes.AsNoTracking().Where(x => x.CatalogId == catalogId);
        if (!string.IsNullOrWhiteSpace(chapterCode)) query = query.Where(x => x.ChapterCode == chapterCode.Trim());
        if (!string.IsNullOrWhiteSpace(code)) query = query.Where(x => EF.Functions.ILike(x.Code, $"%{EscapeLike(code.Trim())}%") || EF.Functions.ILike(x.NormalizedCode, $"%{EscapeLike(code.Trim().Replace(".", string.Empty, StringComparison.Ordinal))}%"));
        if (!string.IsNullOrWhiteSpace(diseaseName)) query = query.Where(x => EF.Functions.ILike(x.DiseaseName, $"%{EscapeLike(diseaseName.Trim())}%") || (x.DiseaseNameEn != null && EF.Functions.ILike(x.DiseaseNameEn, $"%{EscapeLike(diseaseName.Trim())}%")));
        if (!string.IsNullOrWhiteSpace(categoryCode)) query = query.Where(x => x.CategoryCode != null && EF.Functions.ILike(x.CategoryCode, $"%{EscapeLike(categoryCode.Trim())}%"));
        if (!string.IsNullOrWhiteSpace(mainGroupName)) query = query.Where(x => x.MainGroupName != null && EF.Functions.ILike(x.MainGroupName, $"%{EscapeLike(mainGroupName.Trim())}%"));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = EscapeLike(search.Trim());
            query = query.Where(x => EF.Functions.ILike(x.Code, $"%{term}%") ||
                                     EF.Functions.ILike(x.NormalizedCode, $"%{term.Replace(".", string.Empty, StringComparison.Ordinal)}%") ||
                                     EF.Functions.ILike(x.DiseaseName, $"%{term}%") ||
                                     (x.DiseaseNameEn != null && EF.Functions.ILike(x.DiseaseNameEn, $"%{term}%")));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Code).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Code, x.NormalizedCode, x.DiseaseName, x.DiseaseNameEn, x.CategoryCode, x.CategoryName,
                x.ChapterNumber, x.ChapterCode, x.ChapterName, x.MainGroupCode, x.MainGroupName,
                x.SubGroup1Code, x.SubGroup1Name, x.SubGroup2Code, x.SubGroup2Name, x.Note })
            .ToListAsync(ct);
        return Ok(new { page, pageSize, total, items = rows });
    }

    private async Task<Guid?> CurrentCatalogId(CancellationToken ct) => await db.Icd10Catalogs.AsNoTracking()
        .OrderByDescending(x => x.ImportedAtUtc).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private static int RomanOrder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return int.MaxValue;
        var numerals = new Dictionary<char, int> { ['I'] = 1, ['V'] = 5, ['X'] = 10, ['L'] = 50, ['C'] = 100, ['D'] = 500, ['M'] = 1000 };
        var result = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (!numerals.TryGetValue(char.ToUpperInvariant(value[i]), out var current)) return int.MaxValue;
            var next = i + 1 < value.Length && numerals.TryGetValue(char.ToUpperInvariant(value[i + 1]), out var nextValue) ? nextValue : 0;
            result += current < next ? -current : current;
        }
        return result;
    }
}
