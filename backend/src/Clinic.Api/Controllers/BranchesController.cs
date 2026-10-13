using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Api.Security;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Clinic.Api.Controllers;

[ApiController, Route("master-data/branches"), Authorize]
public sealed class BranchesController(AuthorizationDbContext db, VerifiedTenant scope, SuperAdminAccess admin) : ControllerBase
{
    [HttpGet, BusinessApi("branches.catalog.list", "Danh sách chi nhánh", true)]
    public async Task<IActionResult> List([FromQuery] Guid? tenantId, [FromQuery] string? search,
        [FromQuery] string? code, [FromQuery] string? name, [FromQuery] string? address,
        [FromQuery] bool? isActive, [FromQuery] int page = 1, [FromQuery] int pageSize = 200, CancellationToken ct = default)
    {
        if (new[] { search, code, name, address }.Any(x => x?.Trim().Length > 100)) return BadRequest(new { message = "Điều kiện lọc tối đa 100 ký tự." });
        var target = await ResolveTenant(tenantId, ct);
        if (target is null) return Forbid();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = (admin.IsSuperAdmin ? db.Branches.IgnoreQueryFilters() : db.Branches).AsNoTracking().Where(x => x.TenantId == target);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%") || EF.Functions.ILike(x.Code, $"%{term}%") ||
                (x.Address != null && EF.Functions.ILike(x.Address, $"%{term}%")));
        }
        if (!string.IsNullOrWhiteSpace(code)) { var term = code.Trim(); query = query.Where(x => EF.Functions.ILike(x.Code, $"%{term}%")); }
        if (!string.IsNullOrWhiteSpace(name)) { var term = name.Trim(); query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%")); }
        if (!string.IsNullOrWhiteSpace(address)) { var term = address.Trim(); query = query.Where(x => x.Address != null && EF.Functions.ILike(x.Address, $"%{term}%")); }
        if (isActive is not null) query = query.Where(x => x.IsActive == isActive);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.TenantId, x.Code, x.Name, x.Address, x.TimeZone, x.IsActive, x.CreatedAtUtc }).ToListAsync(ct);
        return Ok(new { page, pageSize, total, items });
    }

    [HttpPost, BusinessApi("branches.catalog.create", "Tạo chi nhánh", true)]
    public async Task<IActionResult> Create(BranchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Tên chi nhánh là bắt buộc." });
        var target = await ResolveTenant(request.TenantId, ct);
        if (target is null) return Forbid();
        scope.Set(target.Value, Guid.Empty, admin.IsSuperAdmin);
        var branch = new Branch
        {
            TenantId = target.Value,
            Code = GenerateCode(),
            Name = request.Name.Trim(),
            Address = Clean(request.Address),
            TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Asia/Ho_Chi_Minh" : request.TimeZone.Trim()
        };
        db.Branches.Add(branch);
        Audit(target.Value, "branch.created", branch.Id, "Tạo chi nhánh");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new { message = "Mã chi nhánh bị trùng. Vui lòng thử lại." }); }
        return Created($"/master-data/branches/{branch.Id}", branch);
    }

    [HttpPut("{id:guid}"), BusinessApi("branches.catalog.update", "Cập nhật chi nhánh", true)]
    public async Task<IActionResult> Update(Guid id, BranchRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Tên chi nhánh là bắt buộc." });
        var target = await ResolveTenant(request.TenantId, ct);
        if (target is null) return Forbid();
        scope.Set(target.Value, Guid.Empty, admin.IsSuperAdmin);
        var branch = await db.Branches.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == target, ct);
        if (branch is null) return NotFound();
        branch.Name = request.Name.Trim();
        branch.Address = Clean(request.Address);
        branch.TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Asia/Ho_Chi_Minh" : request.TimeZone.Trim();
        Audit(target.Value, "branch.updated", branch.Id, "Cập nhật chi nhánh");
        await db.SaveChangesAsync(ct);
        return Ok(new { branch.Id, branch.TenantId, branch.Code, branch.Name, branch.Address, branch.TimeZone, branch.IsActive, branch.CreatedAtUtc });
    }

    [HttpPut("{id:guid}/status"), BusinessApi("branches.catalog.status", "Cập nhật trạng thái chi nhánh", true)]
    public async Task<IActionResult> SetStatus(Guid id, BranchStatusRequest request, CancellationToken ct)
    {
        var originalTenant = scope.TenantId;
        var originalBranch = scope.BranchId;
        var target = await ResolveTenant(request.TenantId, ct);
        if (target is null) return Forbid();
        scope.Set(target.Value, Guid.Empty, admin.IsSuperAdmin);
        var branch = await db.Branches.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == target, ct);
        if (branch is null) return NotFound();
        if (branch.IsActive && !request.IsActive && originalTenant == target && originalBranch == id)
            return Conflict(new { message = "Không thể ngừng chi nhánh đang dùng trong phiên làm việc. Hãy chuyển sang chi nhánh khác trước." });
        if (branch.IsActive && !request.IsActive && await db.Branches.IgnoreQueryFilters().CountAsync(x => x.TenantId == target && x.IsActive, ct) <= 1)
            return Conflict(new { message = "Tenant phải còn ít nhất một chi nhánh hoạt động." });
        branch.IsActive = request.IsActive;
        Audit(target.Value, request.IsActive ? "branch.activated" : "branch.deactivated", branch.Id,
            request.IsActive ? "Kích hoạt chi nhánh" : "Ngừng hoạt động chi nhánh");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<Guid?> ResolveTenant(Guid? requested, CancellationToken ct)
    {
        if (!admin.IsSuperAdmin)
        {
            if (scope.TenantId is not Guid current || (requested is not null && requested != current)) return null;
            return current;
        }
        var target = requested ?? scope.TenantId;
        return target is Guid tenant && await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Id == tenant && x.IsActive, ct) ? target : null;
    }

    private void Audit(Guid tenantId, string action, Guid resourceId, string description) =>
        db.AuditEvents.Add(new AuditEvent { TenantId = tenantId, ActorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Action = action, ResourceId = resourceId, Reason = description });

    private static string GenerateCode() => $"CN-{Guid.NewGuid():N}"[..11].ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record BranchRequest([Required, StringLength(200, MinimumLength = 1)] string Name,
        [StringLength(500)] string? Address, [StringLength(100)] string? TimeZone = null, Guid? TenantId = null);
    public sealed record BranchStatusRequest(bool IsActive, Guid? TenantId = null);
}
