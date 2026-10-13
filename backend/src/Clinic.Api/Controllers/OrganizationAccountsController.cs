using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Api.Security;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Clinic.Api.Controllers;

[ControlPlane, ApiController, Route("organization/accounts"), Authorize(Policy = "tenant:members.manage")]
public sealed class OrganizationAccountsController(AuthorizationDbContext db, ClinicDataDbContext clinic, UserManager<ClinicUser> users, VerifiedTenant scope, SuperAdminAccess admin, SmtpEmailSender emailSender, ILogger<OrganizationAccountsController> logger) : ControllerBase {
 private Guid Tenant => scope.TenantId!.Value;
 private Guid ActorId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

 [HttpGet]
 public async Task<IActionResult> List(CancellationToken ct) {
  var accounts = await (
  from membership in db.Memberships.AsNoTracking()
  join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
  orderby user.UserName
   select new { id = membership.Id, userName = user.UserName, displayName = user.DisplayName, email = user.Email, membership.AllBranches, isActive = membership.IsActive && user.IsActive, user.IsSuperAdmin }
  ).ToListAsync(ct);
  var linked = await clinic.Employees.AsNoTracking().Where(employee => employee.MembershipId != null)
   .Select(employee => new { employee.MembershipId, employee.Id, employee.FullName, employee.EmployeeCode }).ToListAsync(ct);
  return Ok(accounts.Select(account => new { account.id, account.userName, account.displayName, account.email, account.AllBranches, account.isActive, account.IsSuperAdmin,
   employeeId = linked.FirstOrDefault(employee => employee.MembershipId == account.id)?.Id,
   employeeName = linked.FirstOrDefault(employee => employee.MembershipId == account.id)?.FullName }));
 }

 [HttpGet("{id:guid}")]
 public async Task<IActionResult> Get(Guid id, CancellationToken ct) {
  var account = await (from membership in db.Memberships.AsNoTracking()
   join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
   where membership.Id == id
  select new { id = membership.Id, userName = user.UserName, displayName = user.DisplayName, email = user.Email, membership.AllBranches, isActive = membership.IsActive && user.IsActive, user.IsSuperAdmin })
   .SingleOrDefaultAsync(ct);
  return account is null ? NotFound() : Ok(account);
 }

 [HttpPost]
 public async Task<IActionResult> Create(CreateAccountRequest request, CancellationToken ct) {
  await using var transaction = await db.Database.BeginTransactionAsync(ct);
  var user = new ClinicUser { Id = Guid.NewGuid(), UserName = request.UserName.Trim(), DisplayName = request.DisplayName.Trim(), Email = request.Email.Trim(), LockoutEnabled = true };
  var result = await users.CreateAsync(user, request.Password);
  if (!result.Succeeded) {
   var errors = result.Errors.Select(error => error.Description).ToArray();
  return result.Errors.Any(error => error.Code is "DuplicateUserName" or "DuplicateEmail")
   ? Conflict(new { message = result.Errors.Any(error => error.Code == "DuplicateEmail") ? "Email này đã được dùng cho tài khoản khác." : "Tên tài khoản đã được sử dụng." })
    : BadRequest(new { message = string.Join(" ", errors) });
  }

  var membership = new TenantMembership { TenantId = Tenant, UserId = user.Id, AllBranches = request.AllBranches };
  db.Memberships.Add(membership);
  db.AuditEvents.Add(new AuditEvent { TenantId = Tenant, ActorId = ActorId, Action = "account.created", ResourceId = membership.Id, Reason = request.Reason?.Trim() is { Length: > 0 } reason ? reason : "Tạo tài khoản trong đơn vị" });
  await db.SaveChangesAsync(ct);
  await transaction.CommitAsync(ct);
  return CreatedAtAction(nameof(Get), new { id = membership.Id }, new { id = membership.Id, userName = user.UserName });
 }

 [HttpGet("employee-options")]
 public async Task<IActionResult> EmployeeOptions([FromQuery] string? search, CancellationToken ct) {
  if (search?.Trim().Length > 100) return BadRequest(new { message = "Từ khóa tối đa 100 ký tự." });
  var query = clinic.Employees.AsNoTracking().Where(employee => employee.IsActive);
  if (!string.IsNullOrWhiteSpace(search)) {
   var term = search.Trim();
   query = query.Where(employee => EF.Functions.ILike(employee.FullName, $"%{term}%") || employee.EmployeeCode.Contains(term));
  }
  return Ok(await query.OrderBy(employee => employee.FullName).Take(200)
   .Select(employee => new { employee.Id, employee.EmployeeCode, employee.FullName, employee.MembershipId }).ToListAsync(ct));
 }

 [HttpPut("{id:guid}/employee")]
 public async Task<IActionResult> SetEmployee(Guid id, SetEmployeeRequest request, CancellationToken ct) {
  if (!await db.Memberships.AsNoTracking().AnyAsync(member => member.Id == id && member.TenantId == Tenant, ct)) return NotFound();
  await using var tx = await clinic.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  var old = await clinic.Employees.SingleOrDefaultAsync(employee => employee.MembershipId == id, ct);
  Employee? selected = null;
  if (request.EmployeeId is Guid employeeId) {
   selected = await clinic.Employees.SingleOrDefaultAsync(employee => employee.Id == employeeId && employee.TenantId == Tenant, ct);
   if (selected is null || !selected.IsActive) return BadRequest(new { message = "Nhân viên không hoạt động hoặc không thuộc tenant hiện tại." });
   if (selected.MembershipId is Guid linked && linked != id) return Conflict(new { message = "Nhân viên đã liên kết với tài khoản khác." });
  }
  if (old?.Id == selected?.Id) return NoContent();
  if (old is not null) { old.MembershipId = null; old.UpdatedAtUtc = DateTime.UtcNow; await clinic.SaveChangesAsync(ct); }
  if (selected is not null) { selected.MembershipId = id; selected.UpdatedAtUtc = DateTime.UtcNow; }
  clinic.AuditEvents.Add(new ClinicDataAuditEvent { TenantId = Tenant, ActorId = ActorId,
   Action = selected is null ? "employee.account_unlinked" : "employee.account_linked",
   EntityType = "employee", ResourceId = selected?.Id ?? old!.Id });
  try { await clinic.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
  catch (DbUpdateException) { return Conflict(new { message = "Liên kết nhân viên vừa thay đổi. Tải lại danh sách và thử lại." }); }
  return NoContent();
 }

 [HttpPut("{id:guid}/email")]
 public async Task<IActionResult> UpdateEmail(Guid id, UpdateEmailRequest request, CancellationToken ct) {
  var membership = await db.Memberships.SingleOrDefaultAsync(member => member.Id == id, ct);
  if (membership is null) return NotFound();
  var user = await users.FindByIdAsync(membership.UserId.ToString());
  if (user is null) return NotFound();
  var email = request.Email.Trim();
  if (string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) return NoContent();

  await using var transaction = await db.Database.BeginTransactionAsync(ct);
  var result = await users.SetEmailAsync(user, email);
  if (!result.Succeeded) {
   var errors = result.Errors.Select(error => error.Description).ToArray();
   return result.Errors.Any(error => error.Code == "DuplicateEmail")
    ? Conflict(new { message = "Email này đã được dùng cho tài khoản khác." })
    : BadRequest(new { message = string.Join(" ", errors) });
  }

  db.AuditEvents.Add(new AuditEvent { TenantId = Tenant, ActorId = ActorId, Action = "account.email_updated", ResourceId = membership.Id, Reason = "Cập nhật email tài khoản" });
  await db.SaveChangesAsync(ct);
  await transaction.CommitAsync(ct);
  return NoContent();
 }

 [HttpPut("{id:guid}/display-name")]
 public async Task<IActionResult> UpdateDisplayName(Guid id, UpdateDisplayNameRequest request, CancellationToken ct) {
  var membership = await db.Memberships.SingleOrDefaultAsync(member => member.Id == id, ct);
  if (membership is null) return NotFound();
  var user = await users.FindByIdAsync(membership.UserId.ToString());
  if (user is null) return NotFound();
  var displayName = request.DisplayName.Trim();
  if (string.Equals(user.DisplayName, displayName, StringComparison.Ordinal)) return NoContent();
  await using var transaction = await db.Database.BeginTransactionAsync(ct);
  user.DisplayName = displayName;
  var result = await users.UpdateAsync(user);
  if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(error => error.Description)) });
  db.AuditEvents.Add(new AuditEvent { TenantId = Tenant, ActorId = ActorId, Action = "account.display_name_updated", ResourceId = membership.Id, Reason = "Cập nhật tên tài khoản" });
  await db.SaveChangesAsync(ct);
  await transaction.CommitAsync(ct);
  return NoContent();
 }

 [HttpPost("{id:guid}/password-reset-email")]
 public async Task<IActionResult> SendPasswordResetEmail(Guid id, CancellationToken ct) {
  if (!emailSender.IsConfigured) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Chưa cấu hình dịch vụ gửi email." });
  var membership = await db.Memberships.SingleOrDefaultAsync(member => member.Id == id, ct);
  if (membership is null) return NotFound();
  var user = await users.FindByIdAsync(membership.UserId.ToString());
  if (user?.Email is not { Length: > 0 } email) return Conflict(new { message = "Tài khoản chưa có email nhận thư." });

  var token = await users.GeneratePasswordResetTokenAsync(user);
  try { await emailSender.SendPasswordResetLinkAsync(email, emailSender.CreateResetLink(user.Id, token), ct); }
  catch (Exception error) when (error is not OperationCanceledException) {
    var errorType = error.GetType().Name;
    var inner = error.InnerException;
    var detail = inner?.Message.Replace('\r', ' ').Replace('\n', ' ');
    if (detail is { Length: > 240 }) detail = detail[..240];
    var innerType = inner?.GetType().Name ?? "none";
    logger.LogError("Password reset email failed at SMTP transport. ErrorType={ErrorType}; InnerErrorType={InnerErrorType}; InnerError={InnerError}", errorType, innerType, detail ?? "none");
    return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = $"Gửi SMTP thất bại ({errorType}; {innerType}): {detail ?? "không có chi tiết"}" });
  }

  db.AuditEvents.Add(new AuditEvent { TenantId = Tenant, ActorId = ActorId, Action = "account.password_reset_email_sent", ResourceId = membership.Id, Reason = "Gửi liên kết đặt lại mật khẩu đến địa chỉ đã đăng ký" });
  await db.SaveChangesAsync(ct);
  return NoContent();
 }

 [HttpPost("{id:guid}/password")]
 public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken ct) {
  var membership = await db.Memberships.SingleOrDefaultAsync(member => member.Id == id, ct);
  if (membership is null) return NotFound();
  var user = await users.FindByIdAsync(membership.UserId.ToString());
  if (user is null) return NotFound();

  await using var transaction = await db.Database.BeginTransactionAsync(ct);
  var token = await users.GeneratePasswordResetTokenAsync(user);
  var result = await users.ResetPasswordAsync(user, token, request.Password);
  if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(error => error.Description)) });

  var revokedAt = DateTime.UtcNow;
  await db.UserSessions.IgnoreQueryFilters().Where(session => session.UserId == user.Id && session.RevokedAtUtc == null)
   .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, revokedAt), ct);
  await db.RefreshTokens.IgnoreQueryFilters().Where(refresh => db.UserSessions.IgnoreQueryFilters()
   .Any(session => session.Id == refresh.SessionId && session.UserId == user.Id) && refresh.RevokedAtUtc == null)
   .ExecuteUpdateAsync(setters => setters.SetProperty(refresh => refresh.RevokedAtUtc, revokedAt), ct);

  db.AuditEvents.Add(new AuditEvent { TenantId = Tenant, ActorId = ActorId, Action = "account.password_reset", ResourceId = membership.Id, Reason = request.Reason?.Trim() is { Length: > 0 } reason ? reason : "Đặt lại mật khẩu" });
  await db.SaveChangesAsync(ct);
  await transaction.CommitAsync(ct);
  return NoContent();
 }

 [HttpPut("{id:guid}/status")]
 public async Task<IActionResult> SetStatus(Guid id, AccountStatusRequest request, CancellationToken ct) {
  var membership = await db.Memberships.SingleOrDefaultAsync(member => member.Id == id, ct);
  if (membership is null) return NotFound();
  if (membership.UserId == ActorId && !request.IsActive) return Conflict(new { message = "Không thể ngừng tài khoản của chính mình." });
  if (membership.IsActive == request.IsActive) return NoContent();

  membership.IsActive = request.IsActive;
  db.AuditEvents.Add(new AuditEvent {
   TenantId = Tenant,
   ActorId = ActorId,
   Action = request.IsActive ? "account.activated" : "account.deactivated",
   ResourceId = membership.Id,
   Reason = request.Reason?.Trim() is { Length: > 0 } reason ? reason : (request.IsActive ? "Kích hoạt tại đơn vị" : "Ngừng tại đơn vị")
  });
  await db.SaveChangesAsync(ct);
  return NoContent();
 }

 [HttpPut("{id:guid}/super-admin")]
 public async Task<IActionResult> SetSuperAdmin(Guid id, SuperAdminStatusRequest request, CancellationToken ct) {
  if (!admin.IsSuperAdmin) return Forbid();
  // Serialize all promotions and revocations so two administrators cannot remove the final active administrator.
  await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  await db.Database.ExecuteSqlRawAsync("DO $$ BEGIN PERFORM pg_advisory_xact_lock(6120412); END $$;", ct);
  var membership = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(member => member.Id == id && member.TenantId == Tenant, ct);
  if (membership is null) return NotFound();
  var user = await db.Users.SingleOrDefaultAsync(item => item.Id == membership.UserId, ct);
  if (user is null) return NotFound();
  if (user.IsSuperAdmin == request.IsSuperAdmin) return NoContent();
  if (!request.IsSuperAdmin && user.Id == ActorId) return Conflict(new { message = "Không thể thu hồi Super Admin của chính mình." });
  if (request.IsSuperAdmin && (!user.IsActive || !membership.IsActive)) return Conflict(new { message = "Chỉ cấp Super Admin cho tài khoản đang hoạt động tại đơn vị." });
  if (!request.IsSuperAdmin && await db.Users.CountAsync(item => item.IsSuperAdmin && item.IsActive, ct) <= 1)
   return Conflict(new { message = "Hệ thống phải còn ít nhất một Super Admin đang hoạt động." });
  user.IsSuperAdmin = request.IsSuperAdmin;
  db.AuditEvents.Add(new AuditEvent { TenantId = Tenant, ActorId = ActorId, ResourceId = user.Id,
   Action = request.IsSuperAdmin ? "account.super_admin_granted" : "account.super_admin_revoked",
   Reason = request.IsSuperAdmin ? "Cấp quyền Super Admin toàn hệ thống" : "Thu hồi quyền Super Admin toàn hệ thống" });
  await db.SaveChangesAsync(ct);
  await transaction.CommitAsync(ct);
  return NoContent();
 }

 /// <summary>Payload for creating an account in the current tenant.</summary>
 public sealed record CreateAccountRequest {
  [Required, StringLength(256, MinimumLength = 3)]
  public required string UserName { get; init; }

  [Required, StringLength(200, MinimumLength = 2)]
  public required string DisplayName { get; init; }

    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

  [Required, StringLength(128, MinimumLength = 8)]
  public required string Password { get; init; }

  public bool AllBranches { get; init; }

  [StringLength(500)]
  public string? Reason { get; init; }
 }
 public sealed record SetEmployeeRequest(Guid? EmployeeId);

 /// <summary>Payload for changing an account's display name.</summary>
 public sealed record UpdateDisplayNameRequest {
  [Required, StringLength(200, MinimumLength = 2)]
  public required string DisplayName { get; init; }
 }

 /// <summary>Payload for setting a new account password.</summary>
 public sealed record ResetPasswordRequest {
  [Required, StringLength(128, MinimumLength = 8)]
  public required string Password { get; init; }

  [StringLength(500)]
  public string? Reason { get; init; }
 }

 /// <summary>Payload for changing the email address of a login account.</summary>
 public sealed record UpdateEmailRequest {
  [Required, EmailAddress, StringLength(256)]
  public required string Email { get; init; }
 }

 /// <summary>Payload for activating or deactivating a tenant membership.</summary>
 public sealed record AccountStatusRequest {
  public bool IsActive { get; init; }

  [StringLength(500)]
  public string? Reason { get; init; }
 }
 public sealed record SuperAdminStatusRequest(bool IsSuperAdmin);
}
