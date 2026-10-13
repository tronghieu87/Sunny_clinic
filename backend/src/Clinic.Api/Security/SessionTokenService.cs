using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Persistence;
using Microsoft.IdentityModel.Tokens;

namespace Clinic.Api.Security;

public sealed class SessionTokenService(IConfiguration configuration)
{
    private const string SessionClaim = "sid";
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "Sunheart.Clinic";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "Sunheart.Clinic.Api";
    private readonly string _signingKey = configuration["Jwt:SigningKey"] is { Length: >= 32 } key
        ? key
        : throw new InvalidOperationException("JWT signing key is missing or too short. Configure Jwt:SigningKey using a secret store.");
    public int AccessTokenMinutes => Math.Clamp(configuration.GetValue("Jwt:AccessTokenMinutes", 10), 5, 30);
    public int RefreshTokenDays => Math.Clamp(configuration.GetValue("Jwt:RefreshTokenDays", 30), 1, 90);
    public string RefreshCookiePath => configuration["Jwt:RefreshCookiePath"] ?? "/api/auth";

    public DateTime AccessExpiresAtUtc => DateTime.UtcNow.AddMinutes(AccessTokenMinutes);
    public DateTime RefreshExpiresAtUtc => DateTime.UtcNow.AddDays(RefreshTokenDays);

    public string CreateAccessToken(ClinicUser user, Guid sessionId, Guid? tenantId, Guid? branchId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(SessionClaim, sessionId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (tenantId is Guid tenant && tenant != Guid.Empty) claims.Add(new Claim("clinic_tenant", tenant.ToString()));
        if (branchId is Guid branch && branch != Guid.Empty) claims.Add(new Claim("clinic_branch", branch.ToString()));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey)), SecurityAlgorithms.HmacSha256);
        var expires = AccessExpiresAtUtc;
        var token = new JwtSecurityToken(_issuer, _audience, claims, notBefore: DateTime.UtcNow, expires: expires, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string Value, RefreshToken Record) CreateRefreshToken(Guid sessionId, DateTime expiresAtUtc)
    {
        var id = Guid.NewGuid();
        var secret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
        var value = id.ToString("N") + "." + secret;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        return (value, new RefreshToken { Id = id, SessionId = sessionId, TokenHash = hash, ExpiresAtUtc = expiresAtUtc });
    }

    public bool TryParseRefreshToken(string? value, out Guid tokenId, out string secret)
    {
        tokenId = Guid.Empty;
        secret = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var separator = value.IndexOf('.');
        return separator == 32 && Guid.TryParseExact(value[..separator], "N", out tokenId) &&
               value.Length is > 33 and <= 256 && (secret = value[(separator + 1)..]).Length >= 32;
    }

    public bool VerifyRefreshToken(string secret, string hash)
    {
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        byte[] expected;
        try { expected = Convert.FromHexString(hash); }
        catch (FormatException) { return false; }
        return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static bool TryGetSessionId(ClaimsPrincipal principal, out Guid sessionId) =>
        Guid.TryParse(principal.FindFirstValue(SessionClaim), out sessionId);

    public void SetRefreshCookie(HttpResponse response, string value, DateTime expiresAtUtc, bool secure)
    {
        response.Cookies.Append("Clinic.Refresh", value, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath,
            Expires = expiresAtUtc,
            IsEssential = true
        });
    }

    public void ClearRefreshCookie(HttpResponse response, bool secure) =>
        response.Cookies.Delete("Clinic.Refresh", new CookieOptions
        {
            HttpOnly = true, Secure = secure, SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath, IsEssential = true
        });
}
