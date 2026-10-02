using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PIPDC.Application.Auth;
using PIPDC.Application.Email;
using PIPDC.Domain.Auth;
using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;
using PIPDC.Domain.Entities;
using PIPDC.Infrastructure.Data;

namespace PIPDC.Infrastructure.Auth;

public class AuthService(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ITokenService tokenService,
    AppDbContext dbContext,
    IOptions<JwtSettings> jwtOptions,
    IEmailService emailService,
    IEmailQueue emailQueue,
    IOptions<GmailApiSettings> gmailOptions,
    IHostEnvironment hostEnvironment,
    ILogger<AuthService> logger) : IAuthService
{
    private static string HashToken(string raw) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private const int CodeLength = 6;
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    private const int MaxAttempts = 5;
    private static readonly TimeSpan ResendInterval = TimeSpan.FromSeconds(60);
    public async Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
            return Result.Failure(Error.Conflict("EMAIL_EXISTS", "Email already exists."));

        var user = new AppUser
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserName = request.Email,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return Result.Failure(Error.Validation("REGISTRATION_FAILED",
                string.Join("; ", result.Errors.Select(e => e.Description))));

        await userManager.AddToRoleAsync(user, Roles.User);
        await IssueCodeAndEmailAsync(user, VerificationPurpose.EmailConfirmation, ct);
        return Result.Success();
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is not null && await userManager.IsLockedOutAsync(user))
            return Result<AuthResponse>.Failure(Error.Unauthorized("ACCOUNT_LOCKED", "Too many failed attempts. Try again later."));

        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            if (user is not null)
                await userManager.AccessFailedAsync(user);

            return Result<AuthResponse>.Failure(Error.Unauthorized("INVALID_CREDENTIALS", "Invalid email or password."));
        }

        if (!user.EmailConfirmed)
            return Result<AuthResponse>.Failure(Error.Validation("EMAIL_NOT_CONFIRMED",
                "Please verify your email before signing in. We have sent you a verification code."));

        await userManager.ResetAccessFailedCountAsync(user);
        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == HashToken(request.RefreshToken), ct);

        if (storedToken is null)
            return Result<AuthResponse>.Failure(Error.Unauthorized("INVALID_REFRESH_TOKEN", "Invalid refresh token."));

        if (!storedToken.IsActive)
        {
            if (storedToken.Revoked is not null)
                await RevokeAllActiveTokensAsync(storedToken.UserId, ct);

            return Result<AuthResponse>.Failure(Error.Unauthorized("REFRESH_TOKEN_INACTIVE", "Refresh token is no longer active."));
        }

        storedToken.Revoked = DateTime.UtcNow;

        var newRefreshToken = tokenService.CreateRefreshToken();
        var newHash = HashToken(newRefreshToken);
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Token = newHash,
            UserId = storedToken.UserId,
            CreatedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays)
        });
        storedToken.ReplacedByToken = newHash;
        await dbContext.SaveChangesAsync(ct);

        var user = await userManager.FindByIdAsync(storedToken.UserId);
        return await BuildAuthResponseAsync(user!, newRefreshToken, ct);
    }

    public async Task<Result> RevokeAsync(RevokeRequest request, CancellationToken ct)
    {
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == HashToken(request.RefreshToken), ct);

        if (storedToken is null || !storedToken.IsActive)
            return Result.Failure(Error.NotFound("TOKEN_NOT_FOUND", "Token not found or already inactive."));

        storedToken.Revoked = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<CurrentUserDto>> GetMeAsync(string userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Result<CurrentUserDto>.Failure(Error.NotFound("USER_NOT_FOUND", "User not found."));

        var roles = await userManager.GetRolesAsync(user);
        var location = await LoadLocationAsync(user, ct);

        return Result<CurrentUserDto>.Success(new CurrentUserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.FullName,
            roles,
            user.LocationId,
            location?.Name,
            location?.Type.ToString(),
            HasCoordinates(user)));
    }

    public async Task<Result<CurrentUserDto>> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Result<CurrentUserDto>.Failure(Error.NotFound("USER_NOT_FOUND", "User not found."));

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();

        // Batch 5. The area is validated against the real Location hierarchy so a
        // client cannot invent a location id. Coordinates are range-checked but not
        // geocoded: the client is the only party that knows them, and they are used
        // solely to rank public listings for that user.
        if (request.ClearLocation)
        {
            user.LocationId = null;
            user.Latitude = null;
            user.Longitude = null;
        }
        else if (request.LocationId.HasValue)
        {
            var valid = await dbContext.Locations
                .AnyAsync(l => l.Id == request.LocationId.Value, ct);

            if (!valid)
                return Result<CurrentUserDto>.Failure(Error.Validation("LOCATION_NOT_FOUND",
                    "The selected location does not exist."));

            user.LocationId = request.LocationId.Value;
        }

        if (request.Latitude.HasValue || request.Longitude.HasValue)
        {
            if (request.Latitude is < -90 or > 90)
                return Result<CurrentUserDto>.Failure(Error.Validation("LATITUDE_INVALID",
                    "Latitude must be between -90 and 90."));

            if (request.Longitude is < -180 or > 180)
                return Result<CurrentUserDto>.Failure(Error.Validation("LONGITUDE_INVALID",
                    "Longitude must be between -180 and 180."));

            // A half-supplied pair is meaningless for distance, so it is treated as
            // "not shared" rather than silently keeping one half.
            user.Latitude = request.Latitude;
            user.Longitude = request.Longitude;
        }

        await dbContext.SaveChangesAsync(ct);

        var roles = await userManager.GetRolesAsync(user);
        var location = await LoadLocationAsync(user, ct);

        return Result<CurrentUserDto>.Success(new CurrentUserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.FullName,
            roles,
            user.LocationId,
            location?.Name,
            location?.Type.ToString(),
            HasCoordinates(user)));
    }

    private Task<Location?> LoadLocationAsync(AppUser user, CancellationToken ct) =>
        user.LocationId is null
            ? Task.FromResult<Location?>(null)
            : dbContext.Locations.FirstOrDefaultAsync(l => l.Id == user.LocationId.Value, ct);

    private static bool HasCoordinates(AppUser user) =>
        user.Latitude.HasValue && user.Longitude.HasValue;

    public async Task<Result> ForgotPasswordAsync(string email, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return Result.Failure(Error.NotFound("USER_NOT_FOUND",
                "No account is registered with that email."));

        return await IssueCodeAndEmailAsync(user, VerificationPurpose.PasswordReset, ct);
    }

    public async Task<Result> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Result.Failure(Error.NotFound("USER_NOT_FOUND", "User not found."));

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return Result.Failure(Error.Validation("PASSWORD_CHANGE_FAILED",
                string.Join("; ", result.Errors.Select(e => e.Description))));

        emailQueue.QueueEmail(
            logger,
            EmailTemplates.PasswordChangedNotification(user.Email!, user.FullName,
                gmailOptions.Value.FrontendBaseUrl),
            "password-changed");

        return Result.Success();
    }

    public async Task<Result> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result.Failure(Error.Validation("CODE_INVALID", "The verification code is invalid."));

        if (user.EmailConfirmed)
            return Result.Failure(Error.Conflict("ALREADY_VERIFIED", "Your email is already verified."));

        var consume = await ConsumeCodeAsync(user.Id, VerificationPurpose.EmailConfirmation, request.Code, ct);
        if (consume.IsFailure)
            return consume;

        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
        await InvalidateActiveCodesAsync(user.Id, VerificationPurpose.EmailConfirmation, ct);

        return Result.Success();
    }

    public async Task<Result> ResendVerificationEmailAsync(string email, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return Result.Success();

        if (user.EmailConfirmed)
            return Result.Failure(Error.Conflict("ALREADY_VERIFIED", "Your email is already verified."));

        return await IssueCodeAndEmailAsync(user, VerificationPurpose.EmailConfirmation, ct);
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result.Failure(Error.Validation("CODE_INVALID", "The reset code is invalid."));

        var consume = await ConsumeCodeAsync(user.Id, VerificationPurpose.PasswordReset, request.Code, ct);
        if (consume.IsFailure)
            return consume;

        if (await userManager.HasPasswordAsync(user))
        {
            var remove = await userManager.RemovePasswordAsync(user);
            if (!remove.Succeeded)
                return Result.Failure(Error.Validation("PASSWORD_RESET_FAILED",
                    string.Join("; ", remove.Errors.Select(e => e.Description))));
        }

        var add = await userManager.AddPasswordAsync(user, request.NewPassword);
        if (!add.Succeeded)
            return Result.Failure(Error.Validation("PASSWORD_RESET_FAILED",
                string.Join("; ", add.Errors.Select(e => e.Description))));

        await InvalidateActiveCodesAsync(user.Id, VerificationPurpose.PasswordReset, ct);
        return Result.Success();
    }

    /// <summary>
    /// Roles this endpoint may grant. Deliberately excludes Admin: elevation must
    /// not be reachable through the same route that onboards agents.
    /// </summary>
    private static readonly string[] AllowedRoles = [Roles.Agent, Roles.User];

    public async Task<Result> AddRoleAsync(AddRoleRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result.Failure(Error.NotFound("USER_NOT_FOUND", "User not found."));

        // Allow-list rather than a RoleExistsAsync check alone. That check accepts
        // any role present in the database, including "Admin", so an admin could
        // grant full administrative access to another account through an endpoint
        // whose purpose is agent onboarding. Roles are also case-sensitive in
        // ASP.NET Identity, so an unvalidated value is as likely to be a typo as an
        // escalation attempt.
        if (!AllowedRoles.Contains(request.Role))
            return Result.Failure(Error.Validation(
                "ROLE_NOT_ALLOWED",
                $"'{request.Role}' cannot be assigned here. Allowed roles: {string.Join(", ", AllowedRoles)}."));

        if (!await roleManager.RoleExistsAsync(request.Role))
            return Result.Failure(Error.Validation("ROLE_NOT_FOUND", $"The {request.Role} role does not exist."));

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        if (string.Equals(request.Role, Roles.Agent, StringComparison.OrdinalIgnoreCase))
        {
            var agent = await dbContext.Agents.FirstOrDefaultAsync(a => a.UserId == user.Id, ct);
            if (agent is null)
            {
                dbContext.Agents.Add(new Agent
                {
                    Bio = null,
                    Title = "Agent",
                    AgencyName = "PIPDC Agency",
                    LicenseNumber = null,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    IsVerified = false,
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow
                });
                await dbContext.SaveChangesAsync(ct);
            }
        }

        var addResult = await userManager.AddToRoleAsync(user, request.Role);
        if (!addResult.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            return Result.Failure(Error.Validation("ROLE_ADD_FAILED",
                string.Join("; ", addResult.Errors.Select(e => e.Description))));
        }

        await transaction.CommitAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveRoleAsync(RemoveRoleRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Result.Failure(Error.NotFound("USER_NOT_FOUND", "User not found."));

        if (!await roleManager.RoleExistsAsync(request.Role))
            return Result.Failure(Error.Validation("ROLE_NOT_FOUND", "Role does not exist."));

        if (string.Equals(request.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1 && await userManager.IsInRoleAsync(user, Roles.Admin))
                return Result.Failure(Error.Conflict("ADMIN_REQUIRED", "You cannot remove the last admin user."));
        }

        // Agent status belongs to the lifecycle in AgentService, not to a generic role
        // strip. Handling it here used to hard-delete the Agent row while leaving the
        // application Approved, stranding the user with no agent record and no route to
        // appeal. Refuse, and point the caller at the endpoint that records a reason.
        if (string.Equals(request.Role, Roles.Agent, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Conflict("AGENT_REMOVAL_REQUIRES_LIFECYCLE",
                "Agent status cannot be removed directly. Use the agent removal endpoint instead: it records a reason, revokes the registration and can transfer the agent's listings."));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        var result = await userManager.RemoveFromRoleAsync(user, request.Role);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            return Result.Failure(Error.Validation("ROLE_REMOVE_FAILED",
                string.Join("; ", result.Errors.Select(e => e.Description))));
        }

        await transaction.CommitAsync(ct);
        return Result.Success();
    }

    private async Task<Result<AuthResponse>> BuildAuthResponseAsync(AppUser user, CancellationToken ct)
    {
        var refreshToken = tokenService.CreateRefreshToken();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            Token = HashToken(refreshToken),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays)
        });
        await dbContext.SaveChangesAsync(ct);

        return await BuildAuthResponseAsync(user, refreshToken, ct);
    }

    private async Task<Result<AuthResponse>> BuildAuthResponseAsync(AppUser user, string refreshToken, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (token, expiresAt) = tokenService.CreateAccessToken(user.Id, user.Email!, user.FullName, roles);

        return Result<AuthResponse>.Success(new AuthResponse(
            user.Id,
            user.Email!,
            roles,
            token,
            expiresAt,
            refreshToken,
            DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays)));
    }

    private async Task RevokeAllActiveTokensAsync(string userId, CancellationToken ct)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.Revoked == null && t.Expires > DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.Revoked = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    // ── Verification codes ──────────────────────────────────────────────

    private static string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(1_000_000).ToString("D" + CodeLength);
    }

    private static string HashCode(string code)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            code, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}";
    }

    private static bool CodeMatches(string stored, string candidate)
    {
        var parts = stored.Split(':', 2);
        if (parts.Length != 2)
            return false;

        var expected = Convert.FromHexString(parts[1]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            candidate, Convert.FromHexString(parts[0]),
            100_000, HashAlgorithmName.SHA256, 32);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private async Task<Result> IssueCodeAndEmailAsync(
        AppUser user, VerificationPurpose purpose, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var newerThanInterval = await dbContext.VerificationCodes
            .AnyAsync(v => v.UserId == user.Id
                        && v.Purpose == purpose
                        && v.RevokedAt == null
                        && !v.IsUsed
                        && v.CreatedAt > now - ResendInterval, ct);
        if (newerThanInterval)
            return Result.Failure(Error.Validation("RATE_LIMITED",
                "A code was already sent recently. Please wait a minute before requesting another."));

        var code = GenerateCode();
        dbContext.VerificationCodes.Add(new VerificationCode
        {
            UserId = user.Id,
            CodeHash = HashCode(code),
            Purpose = purpose,
            ExpiresAt = now.Add(CodeLifetime),
            CreatedAt = now
        });

        await InvalidateActiveCodesAsync(user.Id, purpose, ct);
        await dbContext.SaveChangesAsync(ct);

        var baseUrl = gmailOptions.Value.FrontendBaseUrl;
        var message = purpose == VerificationPurpose.EmailConfirmation
            ? EmailTemplates.EmailVerification(user.Email!, user.FullName, code,
                (int)CodeLifetime.TotalMinutes, baseUrl)
            : EmailTemplates.PasswordReset(user.Email!, user.FullName, code,
                (int)CodeLifetime.TotalMinutes, baseUrl);

        // Development convenience: print the code even when delivery succeeds so local
        // testing never depends on the inbox. Never logged outside Development.
        if (hostEnvironment.IsDevelopment())
            logger.LogInformation(
                "DEV verification code for {Email} ({Purpose}): {Code}",
                user.Email, purpose, code);

        try
        {
            // Intentionally NOT queued. The development fallback below depends on
            // this send failing, so the verification code only reaches the console
            // when delivery fails. Queueing would make delivery appear to succeed
            // and the local signup/reset flow would become untestable.
            await emailService.SendAsync(message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Local-dev fallback: when GmailApiSettings is not configured, delivery fails and
            // the code is surfaced in the server console so the flow can still be tested.
            // The code is only logged when delivery FAILS, never on success.
            logger.LogWarning(ex,
                "Email delivery failed for {Email} ({Purpose}); verification code is {Code}.",
                user.Email, purpose, code);
        }

        return Result.Success();
    }

    private async Task<Result> ConsumeCodeAsync(string userId, VerificationPurpose purpose, string code, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var anyPending = await dbContext.VerificationCodes.AnyAsync(
            v => v.UserId == userId && v.Purpose == purpose && v.RevokedAt == null && !v.IsUsed, ct);

        if (!anyPending)
            return Result.Failure(Error.Validation("CODE_INVALID",
                "No active code was found. Please request a new one."));

        var active = await dbContext.VerificationCodes
            .Where(v => v.UserId == userId
                     && v.Purpose == purpose
                     && v.RevokedAt == null
                     && !v.IsUsed
                     && v.ExpiresAt > now)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(ct);

        foreach (var stored in active)
        {
            if (CodeMatches(stored.CodeHash, code))
            {
                stored.IsUsed = true;
                await dbContext.SaveChangesAsync(ct);
                return Result.Success();
            }
        }

        if (active.Count == 0)
            return Result.Failure(Error.Validation("CODE_EXPIRED",
                "This code has expired. Please request a new one."));

        var newest = active[0];
        newest.Attempts++;
        if (newest.Attempts >= MaxAttempts)
        {
            newest.RevokedAt = now;
            await dbContext.SaveChangesAsync(ct);
            return Result.Failure(Error.Validation("CODE_EXPIRED",
                "Too many failed attempts. Please request a new code."));
        }
        await dbContext.SaveChangesAsync(ct);

        return Result.Failure(Error.Validation("CODE_INVALID", "The code you entered is incorrect."));
    }

    private async Task InvalidateActiveCodesAsync(string userId, VerificationPurpose purpose, CancellationToken ct)
    {
        var active = await dbContext.VerificationCodes
            .Where(v => v.UserId == userId
                     && v.Purpose == purpose
                     && v.RevokedAt == null
                     && !v.IsUsed)
            .ToListAsync(ct);

        foreach (var code in active)
            code.RevokedAt = DateTime.UtcNow;

        if (active.Count > 0)
            await dbContext.SaveChangesAsync(ct);
    }
}
