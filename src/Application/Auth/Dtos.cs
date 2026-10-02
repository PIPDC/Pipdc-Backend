using System.ComponentModel.DataAnnotations;

namespace PIPDC.Application.Auth;

public record RegisterRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);
public record RefreshRequest(string RefreshToken);
public record RevokeRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record VerifyEmailRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, StringLength(6, MinimumLength = 6)] string Code);
public record ResendVerificationRequest(
    [Required, EmailAddress, MaxLength(256)] string Email);
public record ResetPasswordRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, StringLength(6, MinimumLength = 6)] string Code,
    [Required, MinLength(8)] string NewPassword);
public record AddRoleRequest(string Email, string Role);
public record RemoveRoleRequest(string Email, string Role);

public record UpdateProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [MaxLength(20)] string? PhoneNumber,
    // Batch 5. Location is opt-in and is never touched by an ordinary profile save:
    // a nullable int cannot distinguish "not supplied" from "explicitly null", so
    // clearing is requested deliberately through ClearLocation instead of inferred.
    int? LocationId = null,
    double? Latitude = null,
    double? Longitude = null,
    bool ClearLocation = false);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);
public record CurrentUserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string FullName,
    IEnumerable<string> Roles,
    // Batch 5. Trailing and optional so existing construction sites stay valid.
    int? LocationId = null,
    string? LocationName = null,
    string? LocationType = null,
    bool HasCoordinates = false);
public record AuthResponse(
    string UserId,
    string Email,
    IEnumerable<string> Roles,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);
