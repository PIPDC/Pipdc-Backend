using PIPDC.Domain.Common;

namespace PIPDC.Domain.Entities;

public class Notification : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public AppUser User { get; set; } = null!;
}