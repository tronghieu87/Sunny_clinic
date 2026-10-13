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

[ApiController, Route("master-data/employees"), Authorize]
public sealed class EmployeesController(ClinicDataDbContext db, VerifiedTenant scope) : ControllerBase
{
    [HttpGet("lookups")]
    [BusinessApi("employees.lookups", "Danh mục phục vụ hồ sơ nhân viên")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        var groups = await db.CategoryGroups.AsNoTracking().Where(group => group.IsActive &&
                ((group.TenantId == null && group.IsSystem &&
                (group.Code == "GENDER" || group.Code == "OCCUPATION" || group.Code == "IDENTITY_DOCUMENT")) ||
                (group.TenantId == scope.TenantId && group.Code == EmployeePositionSetup.GroupCode)))
            .OrderBy(group => group.Code)
            .Select(group => new { group.Code, group.Name, items = group.Items.Where(item => item.IsActive)
                .OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
                .Select(item => new { item.Id, item.Code, item.Name }).ToList() })
            .ToListAsync(ct);
        return Ok(groups);
    }

    [HttpGet]
    [BusinessApi("employees.list", "Danh sách nhân viên")]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? code, [FromQuery] string? name,
        [FromQuery] string? phone, [FromQuery] string? dateOfBirth, [FromQuery] string? occupation, [FromQuery] string? position, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        if (new[] { search, code, name, phone, dateOfBirth, occupation, position }.Any(value => value?.Trim().Length > 120))
            return BadRequest(new { message = "Điều kiện tìm kiếm tối đa 120 ký tự." });

        var query = db.Employees.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(employee => employee.EmployeeCode.Contains(term) || EF.Functions.ILike(employee.FullName, $"%{EscapeLike(term)}%") || (employee.PhoneNumber != null && employee.PhoneNumber.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(code)) query = query.Where(employee => EF.Functions.ILike(employee.EmployeeCode, $"%{EscapeLike(code.Trim())}%"));
        if (!string.IsNullOrWhiteSpace(name)) query = query.Where(employee => EF.Functions.ILike(employee.FullName, $"%{EscapeLike(name.Trim())}%"));
        if (!string.IsNullOrWhiteSpace(phone)) query = query.Where(employee => employee.PhoneNumber != null && employee.PhoneNumber.Contains(phone.Trim()));
        if (!string.IsNullOrWhiteSpace(dateOfBirth))
        {
            if (!DateOnly.TryParse(dateOfBirth, System.Globalization.CultureInfo.InvariantCulture, out var birthDate))
                return BadRequest(new { message = "Lọc ngày sinh theo định dạng YYYY-MM-DD." });
            query = query.Where(employee => employee.DateOfBirth == birthDate);
        }
        if (!string.IsNullOrWhiteSpace(occupation)) query = query.Where(employee => employee.OccupationCategoryId != null && db.CategoryItems.Any(item => item.Id == employee.OccupationCategoryId && EF.Functions.ILike(item.Name, $"%{EscapeLike(occupation.Trim())}%")));
        if (!string.IsNullOrWhiteSpace(position)) query = query.Where(employee =>
            (employee.Position != null && EF.Functions.ILike(employee.Position, $"%{EscapeLike(position.Trim())}%")) ||
            (employee.PositionCategoryId != null && db.CategoryItems.Any(item => item.Id == employee.PositionCategoryId && EF.Functions.ILike(item.Name, $"%{EscapeLike(position.Trim())}%"))));
        if (isActive is not null) query = query.Where(employee => employee.IsActive == isActive);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(employee => employee.EmployeeCode).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(employee => new {
                employee.Id, employee.EmployeeCode, employee.FullName, employee.PositionCategoryId,
                position = db.CategoryItems.Where(item => item.Id == employee.PositionCategoryId).Select(item => item.Name).FirstOrDefault() ?? employee.Position,
                employee.DateOfBirth,
                employee.PhoneNumber, employee.Email, employee.IsActive, employee.StartDate,
                employee.GenderCategoryId, gender = db.CategoryItems.Where(item => item.Id == employee.GenderCategoryId).Select(item => item.Name).FirstOrDefault(),
                employee.OccupationCategoryId, occupation = db.CategoryItems.Where(item => item.Id == employee.OccupationCategoryId).Select(item => item.Name).FirstOrDefault(),
                employee.MembershipId
            }).ToListAsync(ct);
        return Ok(new { page, pageSize, total, items = rows });
    }

    [HttpGet("{id:guid}")]
    [BusinessApi("employees.detail", "Chi tiết nhân viên")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (employee is null) return NotFound();
        var position = await db.CategoryItems.AsNoTracking().Where(item => item.Id == employee.PositionCategoryId).Select(item => item.Name).FirstOrDefaultAsync(ct);
        return Ok(ToResponse(employee, includeIdentityDocument: true, position));
    }

    [HttpPost]
    [BusinessApi("employees.create", "Tạo hồ sơ nhân viên")]
    public async Task<IActionResult> Create(SaveEmployeeRequest request, CancellationToken ct)
    {
        var invalid = await Validate(request, null, ct);
        if (invalid is not null) return BadRequest(new { message = invalid });
        var tenantId = RequireTenant();
        if (tenantId is null) return Forbid();

        var employee = new Employee {
            TenantId = tenantId.Value, EmployeeCode = await NextEmployeeCode(tenantId.Value, ct), FullName = request.FullName.Trim(), PositionCategoryId = request.PositionCategoryId,
            GenderCategoryId = request.GenderCategoryId, DateOfBirth = request.DateOfBirth, PhoneNumber = Clean(request.PhoneNumber),
            Email = Clean(request.Email)?.ToLowerInvariant(), IdentityDocumentTypeId = request.IdentityDocumentTypeId,
            IdentityDocumentNumber = Clean(request.IdentityDocumentNumber), OccupationCategoryId = request.OccupationCategoryId,
            StartDate = request.StartDate, IsActive = request.IsActive
        };
        db.Employees.Add(employee);
        AddAudit("employee.created", employee.Id);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new { message = "Mã nhân viên hoặc tài khoản liên kết đã được sử dụng trong đơn vị." }); }
        var position = await db.CategoryItems.AsNoTracking().Where(item => item.Id == employee.PositionCategoryId).Select(item => item.Name).FirstOrDefaultAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = employee.Id }, ToResponse(employee, includeIdentityDocument: false, position));
    }

    [HttpPut("{id:guid}")]
    [BusinessApi("employees.update", "Cập nhật hồ sơ nhân viên")]
    public async Task<IActionResult> Update(Guid id, SaveEmployeeRequest request, CancellationToken ct)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (employee is null) return NotFound();
        var invalid = await Validate(request, employee, ct);
        if (invalid is not null) return BadRequest(new { message = invalid });

        employee.FullName = request.FullName.Trim();
        employee.PositionCategoryId = request.PositionCategoryId;
        employee.Position = null;
        employee.GenderCategoryId = request.GenderCategoryId; employee.DateOfBirth = request.DateOfBirth;
        employee.PhoneNumber = Clean(request.PhoneNumber); employee.Email = Clean(request.Email)?.ToLowerInvariant();
        employee.IdentityDocumentTypeId = request.IdentityDocumentTypeId; employee.IdentityDocumentNumber = Clean(request.IdentityDocumentNumber);
        employee.OccupationCategoryId = request.OccupationCategoryId;
        employee.StartDate = request.StartDate; employee.UpdatedAtUtc = DateTime.UtcNow;
        AddAudit("employee.updated", employee.Id);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new { message = "Mã nhân viên hoặc tài khoản liên kết đã được sử dụng trong đơn vị." }); }
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [BusinessApi("employees.status", "Cập nhật trạng thái nhân viên")]
    public async Task<IActionResult> SetStatus(Guid id, EmployeeStatusRequest request, CancellationToken ct)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (employee is null) return NotFound();
        if (employee.IsActive == request.IsActive) return NoContent();
        employee.IsActive = request.IsActive; employee.UpdatedAtUtc = DateTime.UtcNow;
        AddAudit(request.IsActive ? "employee.activated" : "employee.deactivated", employee.Id);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<string?> Validate(SaveEmployeeRequest request, Employee? existing, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 200)
            return "Họ tên không hợp lệ.";
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow) || request.StartDate > DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)))
            return "Ngày sinh hoặc ngày bắt đầu không hợp lệ.";
        if (request.Email is { Length: > 254 } || request.PhoneNumber is { Length: > 30 } || request.IdentityDocumentNumber is { Length: > 50 })
            return "Email, số điện thoại hoặc giấy tờ vượt quá độ dài cho phép.";
        if ((request.IdentityDocumentTypeId is null) != string.IsNullOrWhiteSpace(request.IdentityDocumentNumber))
            return "Khi nhập số giấy tờ, cần chọn loại giấy tờ và ngược lại.";

        var tenant = RequireTenant();
        if (tenant is null) return "Không xác định được đơn vị hiện tại.";
        if (!await CategoryValueValid(request.GenderCategoryId, "GENDER", existing?.GenderCategoryId, ct) ||
            !await CategoryValueValid(request.IdentityDocumentTypeId, "IDENTITY_DOCUMENT", existing?.IdentityDocumentTypeId, ct) ||
            !await CategoryValueValid(request.OccupationCategoryId, "OCCUPATION", existing?.OccupationCategoryId, ct))
            return "Giới tính, loại giấy tờ hoặc nghề nghiệp không thuộc danh mục chuẩn hợp lệ.";
        var currentPositionId = existing?.PositionCategoryId;
        if (request.PositionCategoryId is Guid positionId &&
            !await db.CategoryItems.AnyAsync(item => item.Id == positionId && item.TenantId == tenant &&
                item.CategoryGroup != null && item.CategoryGroup.TenantId == tenant &&
                item.CategoryGroup.Code == EmployeePositionSetup.GroupCode &&
                ((item.IsActive && item.CategoryGroup.IsActive) || item.Id == currentPositionId), ct))
            return "Chức vụ không thuộc danh mục của đơn vị hiện tại.";
        return null;
    }

    private async Task<bool> CategoryValueValid(Guid? id, string groupCode, Guid? currentId, CancellationToken ct)
    {
        if (id is null) return true;
        return await db.CategoryItems.AnyAsync(item => item.Id == id && item.TenantId == null && item.CategoryGroup != null && item.CategoryGroup.IsSystem && item.CategoryGroup.Code == groupCode && (item.IsActive || item.Id == currentId), ct);
    }

    private Guid? RequireTenant() => scope.TenantId is { } id && id != Guid.Empty ? id : null;
    private void AddAudit(string action, Guid resourceId) => db.AuditEvents.Add(new ClinicDataAuditEvent {
        TenantId = RequireTenant()!.Value,
        ActorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        Action = action, EntityType = "employee", ResourceId = resourceId
    });
    private async Task<string> NextEmployeeCode(Guid tenantId, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                INSERT INTO employee_code_sequences ("TenantId", "LastValue")
                VALUES (@tenantId, COALESCE((
                    SELECT MAX(substring("EmployeeCode" from 4)::bigint)
                    FROM employees
                    WHERE "TenantId" = @tenantId AND "EmployeeCode" ~ '^NV-[0-9]+$'
                ), 0) + 1)
                ON CONFLICT ("TenantId") DO UPDATE
                SET "LastValue" = employee_code_sequences."LastValue" + 1
                RETURNING "LastValue"
                """;
            command.Parameters.Add(new NpgsqlParameter<Guid>("tenantId", tenantId));
            var result = await command.ExecuteScalarAsync(ct);
            var nextValue = Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
            return $"NV-{nextValue:D6}";
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static object ToResponse(Employee employee, bool includeIdentityDocument, string? position) => new {
        employee.Id, employee.EmployeeCode, employee.FullName, employee.PositionCategoryId, position = position ?? employee.Position,
        employee.GenderCategoryId, employee.DateOfBirth,
        employee.PhoneNumber, employee.Email, employee.IdentityDocumentTypeId,
        identityDocumentNumber = includeIdentityDocument ? employee.IdentityDocumentNumber : null,
        employee.OccupationCategoryId, employee.MembershipId, employee.StartDate, employee.IsActive,
        employee.CreatedAtUtc, employee.UpdatedAtUtc
    };

    public sealed record SaveEmployeeRequest(
        [Required, StringLength(200)] string FullName,
        Guid? PositionCategoryId,
        Guid? GenderCategoryId,
        DateOnly? DateOfBirth,
        [StringLength(30)] string? PhoneNumber,
        [EmailAddress, StringLength(254)] string? Email,
        Guid? IdentityDocumentTypeId,
        [StringLength(50)] string? IdentityDocumentNumber,
        Guid? OccupationCategoryId,
        DateOnly? StartDate,
        bool IsActive = true);


    public sealed record EmployeeStatusRequest(bool IsActive);

    private static string EscapeLike(string value)
    {
        var escape = ((char)92).ToString();
        return value.Replace(escape, escape + escape, StringComparison.Ordinal)
            .Replace("%", escape + "%", StringComparison.Ordinal)
            .Replace("_", escape + "_", StringComparison.Ordinal);
    }


}
