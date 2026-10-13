using Clinic.Api.Security;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Controllers;

[ApiController, Route("master-data/administrative-units"), Authorize]
public sealed class AdministrativeUnitsController(ClinicDataDbContext db) : ControllerBase
{
    [HttpGet("catalog")]
    [BusinessApi("administrative-units.catalog", "Thông tin bộ dữ liệu địa giới hành chính")]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        var catalog = await db.AdministrativeUnitCatalogs.AsNoTracking()
            .OrderByDescending(x => x.ImportedAtUtc).FirstOrDefaultAsync(ct);
        if (catalog is null) return NotFound(new { message = "Chưa có dữ liệu địa giới hành chính." });
        var counts = await db.AdministrativeUnits.AsNoTracking().Where(x => x.CatalogId == catalog.Id)
            .GroupBy(x => x.Level).Select(x => new { Level = x.Key, Count = x.Count() }).ToListAsync(ct);
        return Ok(new { catalog.Version, catalog.SourceFileName, catalog.ImportedAtUtc,
            provinceCount = counts.SingleOrDefault(x => x.Level == 1)?.Count ?? 0,
            communeCount = counts.SingleOrDefault(x => x.Level == 2)?.Count ?? 0 });
    }

    [HttpGet("provinces")]
    [BusinessApi("administrative-units.provinces.list", "Danh sách tỉnh/thành")]
    public async Task<IActionResult> Provinces(CancellationToken ct)
    {
        var catalogId = await CurrentCatalogId(ct);
        if (catalogId is null) return NotFound(new { message = "Chưa có dữ liệu địa giới hành chính." });
        var rows = await db.AdministrativeUnits.AsNoTracking().Where(x => x.CatalogId == catalogId && x.Level == 1)
            .OrderBy(x => x.SortOrder).Select(x => new { x.Code, x.Name, x.UnitType, x.LegacyProvinceCode,
                communeCount = db.AdministrativeUnits.Count(child => child.CatalogId == catalogId && child.ParentId == x.Id) }).ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("provinces/{provinceCode}/communes")]
    [BusinessApi("administrative-units.communes.list", "Tra cứu phường/xã theo tỉnh/thành")]
    public async Task<IActionResult> Communes(string provinceCode, [FromQuery] string? search, [FromQuery] string? code,
        [FromQuery] string? name, [FromQuery] string? legacyDistrictName, [FromQuery] string? unitType,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var catalogId = await CurrentCatalogId(ct);
        if (catalogId is null) return NotFound(new { message = "Chưa có dữ liệu địa giới hành chính." });
        var parent = await db.AdministrativeUnits.AsNoTracking().SingleOrDefaultAsync(x => x.CatalogId == catalogId && x.Level == 1 && x.Code == provinceCode, ct);
        if (parent is null) return NotFound(new { message = "Không tìm thấy tỉnh/thành." });
        var query = db.AdministrativeUnits.AsNoTracking().Where(x => x.CatalogId == catalogId && x.ParentId == parent.Id);
        if (new[] { search, code, name, legacyDistrictName, unitType }.Any(x => x?.Trim().Length > 100))
            return BadRequest(new { message = "Điều kiện tìm kiếm tối đa 100 ký tự." });
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{EscapeLike(term)}%") || x.Code.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(code)) query = query.Where(x => EF.Functions.ILike(x.Code, $"%{EscapeLike(code.Trim())}%"));
        if (!string.IsNullOrWhiteSpace(name)) query = query.Where(x => EF.Functions.ILike(x.Name, $"%{EscapeLike(name.Trim())}%"));
        if (!string.IsNullOrWhiteSpace(legacyDistrictName)) query = query.Where(x => x.LegacyDistrictName != null && EF.Functions.ILike(x.LegacyDistrictName, $"%{EscapeLike(legacyDistrictName.Trim())}%"));
        if (!string.IsNullOrWhiteSpace(unitType)) query = query.Where(x => EF.Functions.ILike(x.UnitType, $"%{EscapeLike(unitType.Trim())}%"));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Code, x.Name, x.UnitType, x.LegacyProvinceCode, x.LegacyDistrictCode, x.LegacyDistrictName, x.SourceRowNumber, x.ReviewStatus })
            .ToListAsync(ct);
        return Ok(new { province = new { parent.Code, parent.Name }, page, pageSize, total, items = rows });
    }

    private async Task<Guid?> CurrentCatalogId(CancellationToken ct) => await db.AdministrativeUnitCatalogs.AsNoTracking()
        .OrderByDescending(x => x.ImportedAtUtc).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
