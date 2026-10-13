using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Api.Security;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Controllers;

[ApiController, Route("master-data/specialties"), Authorize]
public sealed class SpecialtiesController(ClinicDataDbContext db, VerifiedTenant scope) : ControllerBase
{
    [HttpGet, BusinessApi("specialties.list", "Danh sách chuyên khoa")]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? code, [FromQuery] string? name, [FromQuery] string? description, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, CancellationToken ct = default)
    {
        if (new[] { search, code, name, description }.Any(x => x?.Trim().Length > 100)) return BadRequest(new { message = "Từ khóa tối đa 100 ký tự." });
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.Specialties.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%") || EF.Functions.ILike(x.Code, $"%{term}%")); }
        if (!string.IsNullOrWhiteSpace(code)) { var term = code.Trim(); query = query.Where(x => EF.Functions.ILike(x.Code, $"%{term}%")); }
        if (!string.IsNullOrWhiteSpace(name)) { var term = name.Trim(); query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%")); }
        if (!string.IsNullOrWhiteSpace(description)) { var term = description.Trim(); query = query.Where(x => x.Description != null && EF.Functions.ILike(x.Description, $"%{term}%")); }
        if (isActive is not null) query = query.Where(x => x.IsActive == isActive);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpPost, BusinessApi("specialties.create", "Tạo chuyên khoa")]
    public async Task<IActionResult> Create(SpecialtyRequest request, CancellationToken ct)
    {
        var tenantId = scope.TenantId;
        if (tenantId is null) return Forbid();
        var entity = new Specialty { TenantId = tenantId.Value, Code = GenerateCode("CK"), Name = request.Name.Trim(), Description = Clean(request.Description), SortOrder = request.SortOrder };
        db.Specialties.Add(entity); Audit("specialty.created", entity.Id);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict(new { message = "Mã chuyên khoa đã tồn tại trong đơn vị." }); }
        return Created($"/master-data/specialties/{entity.Id}", entity);
    }

    [HttpPut("{id:guid}"), BusinessApi("specialties.update", "Cập nhật chuyên khoa")]
    public async Task<IActionResult> Update(Guid id, SpecialtyRequest request, CancellationToken ct)
    {
        var entity = await db.Specialties.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();
        entity.Name = request.Name.Trim(); entity.Description = Clean(request.Description); entity.SortOrder = request.SortOrder; entity.UpdatedAtUtc = DateTime.UtcNow;
        Audit("specialty.updated", id);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict(new { message = "Mã chuyên khoa đã tồn tại trong đơn vị." }); }
        return Ok(entity);
    }

    [HttpPut("{id:guid}/status"), BusinessApi("specialties.status", "Cập nhật trạng thái chuyên khoa")]
    public async Task<IActionResult> SetStatus(Guid id, StatusRequest request, CancellationToken ct)
    {
        var entity = await db.Specialties.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();
        if (!request.IsActive && await db.ClinicRooms.AnyAsync(x => x.SpecialtyId == id && x.IsActive, ct)) return Conflict(new { message = "Chuyên khoa đang được gắn với phòng/bàn khám đang hoạt động." });
        entity.IsActive = request.IsActive; entity.UpdatedAtUtc = DateTime.UtcNow; Audit(request.IsActive ? "specialty.activated" : "specialty.deactivated", id);
        await db.SaveChangesAsync(ct); return NoContent();
    }

    private void Audit(string action, Guid id) => db.AuditEvents.Add(new ClinicDataAuditEvent { TenantId = scope.TenantId!.Value, ActorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = action, EntityType = "specialty", ResourceId = id });
    private static string GenerateCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)].ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public sealed record SpecialtyRequest([Required, StringLength(120, MinimumLength = 1)] string Name, [StringLength(500)] string? Description, [Range(0, int.MaxValue)] int SortOrder = 0);
    public sealed record StatusRequest(bool IsActive);
}

[ApiController, Route("master-data/clinic-rooms"), Authorize]
public sealed class ClinicRoomsController(ClinicDataDbContext db, AuthorizationDbContext authorizationDb, VerifiedTenant scope) : ControllerBase
{
    [HttpGet("lookups"), BusinessApi("clinic-rooms.lookups", "Danh mục chuyên khoa phục vụ cấu hình phòng khám")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        if (!await HasCurrentBranch(ct)) return Forbid();
        return Ok(await db.Specialties.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(ct));
    }

    [HttpGet, BusinessApi("clinic-rooms.list", "Danh sách phòng khám và bàn khám")]
    public async Task<IActionResult> List([FromQuery] string? code, [FromQuery] string? name, [FromQuery] string? roomType, [FromQuery] string? specialty, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, CancellationToken ct = default)
    {
        if (!await HasCurrentBranch(ct)) return Forbid();
        if (new[] { code, name, roomType, specialty }.Any(x => x?.Trim().Length > 100)) return BadRequest(new { message = "Điều kiện tìm kiếm tối đa 100 ký tự." });
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.ClinicRooms.AsNoTracking().Where(x => x.BranchId == scope.BranchId);
        if (!string.IsNullOrWhiteSpace(code)) { var term = code.Trim(); query = query.Where(x => EF.Functions.ILike(x.Code, $"%{term}%")); }
        if (!string.IsNullOrWhiteSpace(name)) { var term = name.Trim(); query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%")); }
        if (!string.IsNullOrWhiteSpace(roomType)) query = query.Where(x => x.RoomType.Contains(roomType.Trim()));
        if (!string.IsNullOrWhiteSpace(specialty)) query = query.Where(x => db.Specialties.Any(s => s.Id == x.SpecialtyId && EF.Functions.ILike(s.Name, $"%{specialty.Trim()}%")));
        if (isActive is not null) query = query.Where(x => x.IsActive == isActive);
        var total = await query.CountAsync(ct);
        var items = await (from room in query join joinedSpecialty in db.Specialties.AsNoTracking() on room.SpecialtyId equals (Guid?)joinedSpecialty.Id into joined from joinedSpecialty in joined.DefaultIfEmpty()
            orderby room.Name select new { room.Id, room.BranchId, room.Code, room.Name, room.RoomType, room.SpecialtyId, specialtyName = joinedSpecialty == null ? null : joinedSpecialty.Name, room.Description, room.IsActive })
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpPost, BusinessApi("clinic-rooms.create", "Tạo phòng khám hoặc bàn khám")]
    public async Task<IActionResult> Create(RoomRequest request, CancellationToken ct)
    {
        if (!await HasCurrentBranch(ct)) return Forbid();
        if (!Types.Contains(request.RoomType)) return BadRequest(new { message = "Loại phòng/bàn không hợp lệ." });
        if (!await ValidSpecialty(request.SpecialtyId, ct)) return BadRequest(new { message = "Chuyên khoa không hoạt động hoặc không thuộc đơn vị hiện tại." });
        var entity = new ClinicRoom { TenantId = scope.TenantId!.Value, BranchId = scope.BranchId!.Value, SpecialtyId = request.SpecialtyId, Code = GenerateCode("PK"), Name = request.Name.Trim(), RoomType = request.RoomType, Description = Clean(request.Description) };
        db.ClinicRooms.Add(entity); Audit("clinic-room.created", entity.Id);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict(new { message = "Mã phòng/bàn đã tồn tại tại cơ sở." }); }
        return Created($"/master-data/clinic-rooms/{entity.Id}", entity);
    }

    [HttpPut("{id:guid}"), BusinessApi("clinic-rooms.update", "Cập nhật phòng khám hoặc bàn khám")]
    public async Task<IActionResult> Update(Guid id, RoomRequest request, CancellationToken ct)
    {
        if (!await HasCurrentBranch(ct)) return Forbid();
        if (!Types.Contains(request.RoomType)) return BadRequest(new { message = "Loại phòng/bàn không hợp lệ." });
        var entity = await db.ClinicRooms.SingleOrDefaultAsync(x => x.Id == id && x.BranchId == scope.BranchId, ct);
        if (entity is null) return NotFound();
        if (!await ValidSpecialty(request.SpecialtyId, ct)) return BadRequest(new { message = "Chuyên khoa không hoạt động hoặc không thuộc đơn vị hiện tại." });
        entity.SpecialtyId = request.SpecialtyId; entity.Name = request.Name.Trim(); entity.RoomType = request.RoomType; entity.Description = Clean(request.Description); entity.UpdatedAtUtc = DateTime.UtcNow;
        Audit("clinic-room.updated", id);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict(new { message = "Mã phòng/bàn đã tồn tại tại cơ sở." }); }
        return Ok(entity);
    }

    [HttpPut("{id:guid}/status"), BusinessApi("clinic-rooms.status", "Cập nhật trạng thái phòng khám hoặc bàn khám")]
    public async Task<IActionResult> SetStatus(Guid id, StatusRequest request, CancellationToken ct)
    {
        if (!await HasCurrentBranch(ct)) return Forbid();
        var entity = await db.ClinicRooms.SingleOrDefaultAsync(x => x.Id == id && x.BranchId == scope.BranchId, ct);
        if (entity is null) return NotFound();
        entity.IsActive = request.IsActive; entity.UpdatedAtUtc = DateTime.UtcNow; Audit(request.IsActive ? "clinic-room.activated" : "clinic-room.deactivated", id);
        await db.SaveChangesAsync(ct); return NoContent();
    }

    private async Task<bool> HasCurrentBranch(CancellationToken ct) => scope.TenantId is { } tenant && scope.BranchId is { } branch && await authorizationDb.Branches.IgnoreQueryFilters().AsNoTracking().AnyAsync(x => x.Id == branch && x.TenantId == tenant && x.IsActive, ct);
    private Task<bool> ValidSpecialty(Guid? id, CancellationToken ct) => id is null ? Task.FromResult(true) : db.Specialties.AnyAsync(x => x.Id == id && x.IsActive, ct);
    private void Audit(string action, Guid id) => db.AuditEvents.Add(new ClinicDataAuditEvent { TenantId = scope.TenantId!.Value, ActorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = action, EntityType = "clinic_room", ResourceId = id });
    private static readonly string[] Types = ["EXAMINATION", "PROCEDURE", "DIAGNOSTIC", "OTHER"];
    private static string GenerateCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)].ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public sealed record RoomRequest([Required, StringLength(120, MinimumLength = 1)] string Name, [Required] string RoomType, Guid? SpecialtyId, [StringLength(500)] string? Description);
    public sealed record StatusRequest(bool IsActive);
}
