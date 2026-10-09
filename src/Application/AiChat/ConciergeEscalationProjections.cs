using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Data;
using PIPDC.Domain.Entities;

namespace PIPDC.Application.AiChat;

internal static class ConciergeEscalationProjections
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    internal static IQueryable<ConciergeEscalationProjection> Project(IQueryable<ConciergeEscalation> query) =>
        query.Select(e => new ConciergeEscalationProjection(
            e.Id,
            e.AiChatSessionId,
            e.AiChatSession.UserId,
            e.AiChatSession.User.FirstName,
            e.AiChatSession.User.LastName,
            e.AiChatSession.User.Email ?? string.Empty,
            e.AiChatSession.LastMessageAt,
            e.AiChatSession.MessagesJson,
            e.EscalationStatus.ToString(),
            e.EscalationReason,
            e.EscalatedAt,
            e.AssignedAdminId,
            e.AssignedAdmin == null ? null : e.AssignedAdmin.FirstName + " " + e.AssignedAdmin.LastName,
            e.AssignedAt,
            e.ResolvedByUserId,
            e.ResolvedByUser == null ? null : e.ResolvedByUser.FirstName + " " + e.ResolvedByUser.LastName,
            e.ResolvedAt));

    internal static async Task<ConciergeEscalationDto> SingleAsync(
        IAppDbContext dbContext, int escalationId, CancellationToken ct)
    {
        var record = await Project(dbContext.ConciergeEscalations.Where(e => e.Id == escalationId))
            .FirstAsync(ct);

        return ToDto(record);
    }

    internal static ConciergeEscalationDto ToDto(ConciergeEscalationProjection p) =>
        new(
            p.Id,
            p.AiChatSessionId,
            p.ClientUserId,
            $"{p.ClientFirstName} {p.ClientLastName}".Trim(),
            p.ClientEmail,
            p.EscalationStatus,
            p.EscalationReason,
            p.EscalatedAt,
            p.AssignedAdminId,
            p.AssignedAdminName,
            p.AssignedAt,
            p.ResolvedByUserId,
            p.ResolvedByName,
            p.ResolvedAt,
            p.LastMessageAt,
            CountMessages(p.MessagesJson));

    internal static List<AiChatMessageDto> Transcript(string messagesJson)
    {
        if (string.IsNullOrWhiteSpace(messagesJson))
            return new List<AiChatMessageDto>();

        try
        {
            var persisted = JsonSerializer.Deserialize<List<PersistedAiMessage>>(messagesJson, JsonOpts) ?? new();
            return persisted
                .Select(m => new AiChatMessageDto(m.Role, m.Content, m.SentAt, m.Properties))
                .ToList();
        }
        catch (JsonException)
        {
            return new List<AiChatMessageDto>();
        }
    }

    private static int CountMessages(string messagesJson)
    {
        if (string.IsNullOrWhiteSpace(messagesJson))
            return 0;

        try
        {
            return JsonSerializer.Deserialize<List<PersistedAiMessage>>(messagesJson, JsonOpts)?.Count ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    internal sealed record ConciergeEscalationProjection(
        int Id,
        int AiChatSessionId,
        string ClientUserId,
        string ClientFirstName,
        string ClientLastName,
        string ClientEmail,
        DateTime? LastMessageAt,
        string MessagesJson,
        string EscalationStatus,
        string EscalationReason,
        DateTime EscalatedAt,
        string? AssignedAdminId,
        string? AssignedAdminName,
        DateTime? AssignedAt,
        string? ResolvedByUserId,
        string? ResolvedByName,
        DateTime? ResolvedAt);
}