using System.ComponentModel.DataAnnotations;
using Clinic.Api.Security;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Clinic.Api.Controllers;

[ApiController, Route("master-data/categories"), Authorize]
public sealed class CategoriesController(ClinicDataDbContext db, VerifiedTenant scope) : ControllerBase
{
    [HttpGet]
    [BusinessApi("categories.list", "Danh sách nhóm danh mục")]
    public async Task<IActionResult> ListGroups([FromQuery] bool activeOnly, CancellationToken ct)
    {
        var query = db.CategoryGroups.AsNoTracking();
        if (activeOnly) query = query.Where(x => x.IsActive);
        var rows = await query
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.TenantId, x.IsSystem, x.Code, x.Name, x.Description, x.SortOrder, x.IsActive })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("{groupId:guid}/items")]
    [BusinessApi("categories.items.list", "Danh sách giá trị danh mục")]
    public async Task<IActionResult> ListItems(Guid groupId, [FromQuery] bool includeInactive, CancellationToken ct)
    {
        if (!await db.CategoryGroups.AnyAsync(x => x.Id == groupId, ct)) return NotFound();
        var query = db.CategoryItems.AsNoTracking().Where(x => x.CategoryGroupId == groupId);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        var rows = await query
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.CategoryGroupId, x.Code, x.Name, x.SortOrder, x.IsActive })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("{groupId:guid}")]
    [BusinessApi("categories.detail", "Chi tiết nhóm danh mục")]
    public async Task<IActionResult> GetGroup(Guid groupId, CancellationToken ct)
    {
        var group = await db.CategoryGroups.AsNoTracking().SingleOrDefaultAsync(x => x.Id == groupId, ct);
        return group is null ? NotFound() : Ok(ToResponse(group));
    }

    [HttpGet("{groupId:guid}/items/{itemId:guid}")]
    [BusinessApi("categories.items.detail", "Chi tiết giá trị danh mục")]
    public async Task<IActionResult> GetItem(Guid groupId, Guid itemId, CancellationToken ct)
    {
        var item = await db.CategoryItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == itemId && x.CategoryGroupId == groupId, ct);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    [BusinessApi("categories.create", "Tạo nhóm danh mục")]
    public async Task<IActionResult> CreateGroup(SaveGroupRequest request, CancellationToken ct)
    {
        if (!ValidName(request.Name, request.SortOrder) || (request.Description?.Length ?? 0) > 500)
            return BadRequest(new { message = "Nhập tên và thứ tự hợp lệ." });
        var tenantId = request.IsGlobal ? null : RequireTenant();
        if (tenantId is null && !scope.IsSuperAdmin) return Forbid();
        var group = new CategoryGroup { TenantId = tenantId, IsSystem = request.IsGlobal, Code = GenerateCode("CAT"), Name = request.Name.Trim(), Description = CleanDescription(request.Description), SortOrder = request.SortOrder };
        db.CategoryGroups.Add(group);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new { message = "Không tạo được mã nhóm danh mục tự động. Vui lòng thử lại." }); }
        return Created($"/master-data/categories/{group.Id}", ToResponse(group));
    }

    [HttpPut("{groupId:guid}")]
    [BusinessApi("categories.update", "Cập nhật nhóm danh mục")]
    public async Task<IActionResult> UpdateGroup(Guid groupId, SaveGroupRequest request, CancellationToken ct)
    {
        if (!ValidName(request.Name, request.SortOrder) || (request.Description?.Length ?? 0) > 500)
            return BadRequest(new { message = "Nhập tên và thứ tự hợp lệ." });
        var group = await db.CategoryGroups.SingleOrDefaultAsync(x => x.Id == groupId, ct);
        if (group is null) return NotFound();
        if (!CanEdit(group)) return Forbid();
        group.Name = request.Name.Trim(); group.Description = CleanDescription(request.Description);
        group.SortOrder = request.SortOrder; group.IsActive = request.IsActive; group.UpdatedAtUtc = DateTime.UtcNow;
        if (!request.IsActive)
        {
            var items = await db.CategoryItems.Where(x => x.CategoryGroupId == groupId && x.IsActive).ToListAsync(ct);
            foreach (var item in items) { item.IsActive = false; item.UpdatedAtUtc = DateTime.UtcNow; }
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new { message = "Mã nhóm danh mục đã tồn tại." }); }
        return NoContent();
    }

    [HttpPost("{groupId:guid}/items")]
    [BusinessApi("categories.items.create", "Thêm giá trị danh mục")]
    public async Task<IActionResult> CreateItem(Guid groupId, SaveItemRequest request, CancellationToken ct)
    {
        if (!ValidName(request.Name, request.SortOrder)) return BadRequest(new { message = "Nhập tên và thứ tự hợp lệ." });
        var group = await db.CategoryGroups.SingleOrDefaultAsync(x => x.Id == groupId && x.IsActive, ct);
        if (group is null) return NotFound();
        if (!CanEdit(group)) return Forbid();
        var item = new CategoryItem { TenantId = group.TenantId, CategoryGroupId = groupId, Code = GenerateCode("VAL"), Name = request.Name.Trim(), SortOrder = request.SortOrder };
        db.CategoryItems.Add(item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new { message = "Không tạo được mã giá trị tự động. Vui lòng thử lại." }); }
        return Created($"/master-data/categories/{groupId}/items/{item.Id}", ToResponse(item));
    }

    [HttpPut("{groupId:guid}/items/{itemId:guid}")]
    [BusinessApi("categories.items.update", "Cập nhật giá trị danh mục")]
    public async Task<IActionResult> UpdateItem(Guid groupId, Guid itemId, SaveItemRequest request, CancellationToken ct)
    {
        if (!ValidName(request.Name, request.SortOrder)) return BadRequest(new { message = "Nhập tên và thứ tự hợp lệ." });
        var item = await db.CategoryItems.SingleOrDefaultAsync(x => x.Id == itemId && x.CategoryGroupId == groupId, ct);
        if (item is null) return NotFound();
        var group = await db.CategoryGroups.SingleOrDefaultAsync(x => x.Id == groupId, ct);
        if (group is null || !CanEdit(group)) return Forbid();
        if (request.IsActive && !await db.CategoryGroups.AnyAsync(x => x.Id == groupId && x.IsActive, ct))
            return Conflict(new { message = "Không thể kích hoạt giá trị khi nhóm danh mục đang ngừng hoạt động." });
        item.Name = request.Name.Trim(); item.SortOrder = request.SortOrder;
        item.IsActive = request.IsActive; item.UpdatedAtUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new { message = "Mã giá trị đã tồn tại trong nhóm danh mục." }); }
        return NoContent();
    }

    private Guid? RequireTenant() => scope.TenantId is { } id && id != Guid.Empty ? id : null;
    private bool CanEdit(CategoryGroup group) => group.TenantId is null ? scope.IsSuperAdmin : group.TenantId == scope.TenantId;
    private static string GenerateCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)].ToUpperInvariant();
    private static string? CleanDescription(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool ValidName(string name, int sortOrder) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 120 && sortOrder >= 0;
    private static object ToResponse(CategoryGroup x) => new { x.Id, x.TenantId, x.IsSystem, x.Code, x.Name, x.Description, x.SortOrder, x.IsActive };
    private static object ToResponse(CategoryItem x) => new { x.Id, x.CategoryGroupId, x.Code, x.Name, x.SortOrder, x.IsActive };

    public sealed record SaveGroupRequest([Required, StringLength(120)] string Name, [StringLength(500)] string? Description, int SortOrder = 0, bool IsActive = true, bool IsGlobal = false);
    public sealed record SaveItemRequest([Required, StringLength(120)] string Name, int SortOrder = 0, bool IsActive = true);
}
