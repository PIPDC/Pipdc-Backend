using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Domain.Entities;

public class IdempotencyRecord : BaseEntity
{
    public string? UserId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public IdempotencyStatus Status { get; set; }

    public int ResponseStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}