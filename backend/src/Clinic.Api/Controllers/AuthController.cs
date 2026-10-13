using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Clinic.Api.Security;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Controllers;

[ControlPlane, ApiController, Route("auth")]
public sealed class AuthController(
    AuthorizationDbContext db,
    ClinicDataDbContext clinicData,
    UserManager<ClinicUser> users,
    SignInManager<ClinicUser> signIn,
    IAntiforgery antiforgery,
    AccessResolver access,
    VerifiedTenant scope,
    SuperAdminAccess admin,
    SessionTokenService tokens,
    IWebHostEnvironment environment) : ControllerBase
{
    private const string LoginError = "Thông tin đăng nhập không hợp lệ hoặc tài khoản chưa được phép truy cập.";
    private const string RefreshCookieName = "Clinic.Refresh";
    private static readonly string DummyHash = new PasswordHasher<ClinicUser>().HashPassword(new ClinicUser(), Guid.NewGuid().ToString());

    [HttpGet("csrf"), AllowAnonymous]
    public IActionResult Csrf()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(request.UserName);
        if (user is null || !user.IsActive)
        {
            _ = users.PasswordHasher.VerifyHashedPassword(new ClinicUser(), DummyHash, request.Password);
            return Unauthorized(new { message = LoginError });
        }

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized(new { message = LoginError });

        var now = DateTime.UtcNow;
        var session = new UserSession
        {
            UserId = user.Id,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(tokens.RefreshTokenDays),
            CreatedFromIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString() is { Length: > 500 } agent ? agent[..500] : Request.Headers.UserAgent.ToString()
        };
        var (refreshValue, refreshRecord) = tokens.CreateRefreshToken(session.Id, session.ExpiresAtUtc);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.UserSessions.Add(session);
        db.RefreshTokens.Add(refreshRecord);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var accessToken = tokens.CreateAccessToken(user, session.Id, null, null);
        Response.Headers.CacheControl = "no-store";
        if (request.ClientType == "mobile")
            return Ok(new TokenResponse(accessToken, tokens.AccessExpiresAtUtc, refreshValue, session.ExpiresAtUtc, "Bearer"));

        tokens.SetRefreshCookie(Response, refreshValue, session.ExpiresAtUtc, Request.IsHttps || !environment.IsDevelopment());
        return Ok(new TokenResponse(accessToken, tokens.AccessExpiresAtUtc, null, session.ExpiresAtUtc, "Bearer"));
    }

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var supplied = request.RefreshToken ?? Request.Cookies[RefreshCookieName];
        if (!tokens.TryParseRefreshToken(supplied, out var tokenId, out var secret)) return Unauthorized();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var tokenSeed = await db.RefreshTokens.AsNoTracking().Where(t => t.Id == tokenId)
            .Select(t => new { t.SessionId, t.TokenHash }).SingleOrDefaultAsync(ct);
        if (tokenSeed is null || !tokens.VerifyRefreshToken(secret, tokenSeed.TokenHash)) return Unauthorized();

        // Serialize concurrent refresh attempts on the owning session row before checking replay state.
        var now = DateTime.UtcNow;
        var sessionLocked = await db.UserSessions.Where(s => s.Id == tokenSeed.SessionId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.LastRefreshedAtUtc, now), ct);
        if (sessionLocked == 0) return Unauthorized();

        // Read rotation state after acquiring the row lock so simultaneous refreshes cannot both succeed.
        var token = await db.RefreshTokens.SingleAsync(t => t.Id == tokenId, ct);

        if (token.RevokedAtUtc is not null)
        {
            await db.UserSessions.Where(s => s.Id == tokenSeed.SessionId).ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAtUtc, now), ct);
            await db.RefreshTokens.Where(t => t.SessionId == tokenSeed.SessionId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAtUtc, now), ct);
            await transaction.CommitAsync(ct);
            ClearRefreshCookie();
            return Unauthorized(new { message = "Phiên đã bị thu hồi. Vui lòng đăng nhập lại." });
        }

        if (token.ExpiresAtUtc <= now) return Unauthorized();
        var session = await db.UserSessions.AsNoTracking().SingleAsync(s => s.Id == token.SessionId, ct);
        var user = await users.FindByIdAsync(session.UserId.ToString());
        if (user is null || !user.IsActive) return Unauthorized();

        var (newRefreshValue, newRefreshRecord) = tokens.CreateRefreshToken(session.Id, session.ExpiresAtUtc);
        token.RevokedAtUtc = now;
        token.ReplacedByTokenId = newRefreshRecord.Id;
        db.RefreshTokens.Add(newRefreshRecord);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        Response.Headers.CacheControl = "no-store";
        var accessToken = tokens.CreateAccessToken(user, session.Id, session.TenantId, session.BranchId);
        if (request.RefreshToken is not null)
            return Ok(new TokenResponse(accessToken, tokens.AccessExpiresAtUtc, newRefreshValue, session.ExpiresAtUtc, "Bearer"));

        tokens.SetRefreshCookie(Response, newRefreshValue, session.ExpiresAtUtc, Request.IsHttps || !environment.IsDevelopment());
        return Ok(new TokenResponse(accessToken, tokens.AccessExpiresAtUtc, null, session.ExpiresAtUtc, "Bearer"));
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (SessionTokenService.TryGetSessionId(User, out var sessionId)) await RevokeSession(sessionId, ct);
        ClearRefreshCookie();
        return NoContent();
    }

    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(PasswordResetRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        if (user is null || !user.IsActive) return BadRequest(new { message = "Liên kết không hợp lệ hoặc đã hết hạn. Yêu cầu gửi liên kết mới." });
        var result = await users.ResetPasswordAsync(user, request.Token, request.Password);
        if (!result.Succeeded) return BadRequest(new { message = string.Join(" ", result.Errors.Select(error => error.Description)) });
        await RevokeUserSessions(user.Id);
        return NoContent();
    }

    [HttpGet("session"), Authorize]
    public async Task<IActionResult> Session()
    {
        var user = await users.GetUserAsync(User);
        if (user is null || !user.IsActive) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        object? employee = null;
        if (scope.TenantId is { } tenantId && tenantId != Guid.Empty)
        {
            var membershipId = await db.Memberships.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.UserId == user.Id && item.TenantId == tenantId && item.IsActive)
                .Select(item => (Guid?)item.Id).SingleOrDefaultAsync();
            if (membershipId is { } id) employee = await clinicData.Employees.AsNoTracking()
                .Where(item => item.MembershipId == id && item.IsActive)
                .Select(item => new { item.Id, item.EmployeeCode, item.FullName }).SingleOrDefaultAsync();
        }
        return Ok(new { userName = user.UserName, email = user.Email, phoneNumber = user.PhoneNumber, tenantId = scope.TenantId, branchId = scope.BranchId, isSuperAdmin = admin.IsSuperAdmin, employee });
    }

    [HttpGet("permissions"), Authorize]
    public async Task<IActionResult> Permissions([FromServices] PermissionService permissions, CancellationToken ct) =>
        Ok(await permissions.Get(Guid.Parse(users.GetUserId(User)!), false, ct));

    [HttpGet("capabilities"), Authorize]
    public async Task<IActionResult> Capabilities([FromServices] PermissionService permissions, CancellationToken ct)
    {
        var userId = Guid.Parse(users.GetUserId(User)!);
        return Ok(new { isSuperAdmin = admin.IsSuperAdmin, effective = await permissions.Get(userId, false, ct), tenantWide = await permissions.Get(userId, true, ct) });
    }

    [HttpGet("workplaces"), Authorize]
    public async Task<IActionResult> Workplaces(CancellationToken ct)
    {
        var userId = Guid.Parse(users.GetUserId(User)!);
        if (admin.IsSuperAdmin) return Ok(await (from t in db.Tenants.IgnoreQueryFilters().AsNoTracking() join b in db.Branches.IgnoreQueryFilters() on t.Id equals b.TenantId where t.IsActive && b.IsActive orderby t.Name, b.Name select new { tenantId = t.Id, tenantName = t.Name, branchId = b.Id, branchName = b.Name }).ToListAsync(ct));
        var result = await (from m in db.Memberships.IgnoreQueryFilters().AsNoTracking()
            join t in db.Tenants.IgnoreQueryFilters() on m.TenantId equals t.Id
            join b in db.Branches.IgnoreQueryFilters() on t.Id equals b.TenantId
            where m.UserId == userId && m.IsActive && t.IsActive && b.IsActive &&
                (m.AllBranches || db.BranchAccesses.IgnoreQueryFilters().Any(a => a.TenantId == m.TenantId && a.MembershipId == m.Id && a.BranchId == b.Id))
            orderby t.Name, b.Name
            select new { tenantId = t.Id, tenantName = t.Name, branchId = b.Id, branchName = b.Name }).ToListAsync(ct);
        return Ok(result);
    }

    [HttpPost("workplace"), Authorize]
    public async Task<IActionResult> Select(WorkplaceRequest request, CancellationToken ct)
    {
        var user = await users.GetUserAsync(User);
        if (user is null || !SessionTokenService.TryGetSessionId(User, out var sessionId) ||
            !await access.CanAccess(user.Id, request.TenantId, request.BranchId, ct)) return Forbid();
        var updated = await db.UserSessions.Where(s => s.Id == sessionId && s.UserId == user.Id && s.RevokedAtUtc == null && s.ExpiresAtUtc > DateTime.UtcNow)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.TenantId, request.TenantId).SetProperty(s => s.BranchId, request.BranchId), ct);
        if (updated != 1) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { accessToken = tokens.CreateAccessToken(user, sessionId, request.TenantId, request.BranchId), expiresAtUtc = tokens.AccessExpiresAtUtc });
    }

    private async Task RevokeSession(Guid sessionId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await db.UserSessions.Where(s => s.Id == sessionId && s.RevokedAtUtc == null).ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAtUtc, now), ct);
        await db.RefreshTokens.Where(t => t.SessionId == sessionId && t.RevokedAtUtc == null).ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAtUtc, now), ct);
    }

    private async Task RevokeUserSessions(Guid userId)
    {
        var now = DateTime.UtcNow;
        await db.UserSessions.Where(s => s.UserId == userId && s.RevokedAtUtc == null).ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAtUtc, now));
        await db.RefreshTokens.Where(t => db.UserSessions.IgnoreQueryFilters().Any(s => s.UserId == userId && s.Id == t.SessionId) && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAtUtc, now));
    }

    private void ClearRefreshCookie() => tokens.ClearRefreshCookie(Response, Request.IsHttps || !environment.IsDevelopment());

    public sealed record LoginRequest([Required, StringLength(256)] string UserName, [Required, StringLength(1024)] string Password, string ClientType = "browser");
    public sealed record RefreshRequest(string? RefreshToken = null);
    public sealed record TokenResponse(string AccessToken, DateTime AccessExpiresAtUtc, string? RefreshToken, DateTime SessionExpiresAtUtc, string TokenType);
    public sealed record WorkplaceRequest(Guid TenantId, Guid BranchId);
    public sealed record PasswordResetRequest([Required] Guid UserId, [Required, StringLength(4096)] string Token, [Required, StringLength(128, MinimumLength = 8)] string Password);
}
