using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Data;
using PIPDC.Domain.Entities;

namespace PIPDC.Application.Conversations;

internal static class ConversationProjections
{
    internal static async Task<ConversationDto> SingleAsync(IAppDbContext dbContext, int conversationId, string currentUserId, CancellationToken ct)
    {
        var projection = await Project(dbContext.Conversations.Where(c => c.Id == conversationId), currentUserId)
            .FirstAsync(ct);

        return ToDto(projection);
    }

    internal static IQueryable<ConversationProjection> Project(IQueryable<Conversation> query, string currentUserId) =>
        query.Select(c => new ConversationProjection(
            c.Id,
            c.EnquiryId,
            c.ClientUserId,
            c.Client.FirstName,
            c.Client.LastName,
            c.Client.Email ?? string.Empty,
            c.AgentId,
            c.Agent.UserId,
            c.Agent.User.FirstName,
            c.Agent.User.LastName,
            c.Agent.AgencyName,
            c.Agent.PhotoUrl,
            c.Enquiry.PropertyId,
            c.Enquiry.Property.Title,
            c.Enquiry.Property.Slug,
            c.LastMessageAt,
            c.Messages.Count(),
            c.Messages.Count(m => m.SenderUserId != currentUserId && m.ReadAt == null),
            c.CreatedAt,
            c.UpdatedAt,
            c.EscalationStatus.ToString(),
            c.EscalationReason,
            c.EscalatedAt,
            c.EscalatedByUser == null ? null : c.EscalatedByUser.FirstName + " " + c.EscalatedByUser.LastName,
            c.AssignedAdminId,
            c.AssignedAdmin == null ? null : c.AssignedAdmin.FirstName + " " + c.AssignedAdmin.LastName,
            c.AssignedAt,
            c.ResolvedAt,
            c.ResolvedByUser == null ? null : c.ResolvedByUser.FirstName + " " + c.ResolvedByUser.LastName));

    internal static ConversationDto ToDto(ConversationProjection p) =>
        new(
            p.Id,
            p.EnquiryId,
            new ConversationClientDto(p.ClientUserId, $"{p.ClientFirstName} {p.ClientLastName}".Trim(), p.ClientEmail),
            new ConversationAgentDto(p.AgentId, p.AgentUserId, $"{p.AgentFirstName} {p.AgentLastName}".Trim(), p.AgentAgencyName, p.AgentPhotoUrl),
            new ConversationPropertyDto(p.PropertyId, p.PropertyTitle, p.PropertySlug),
            p.LastMessageAt,
            p.MessageCount,
            p.UnreadCount,
            p.CreatedAt,
            p.UpdatedAt,
            p.EscalationStatus,
            p.EscalationReason,
            p.EscalatedAt,
            p.EscalatedByName,
            p.AssignedAdminId,
            p.AssignedAdminName,
            p.AssignedAt,
            p.ResolvedAt,
            p.ResolvedByName);

    internal sealed record ConversationProjection(
        int Id,
        int EnquiryId,
        string ClientUserId,
        string ClientFirstName,
        string ClientLastName,
        string ClientEmail,
        int AgentId,
        string? AgentUserId,
        string AgentFirstName,
        string AgentLastName,
        string AgentAgencyName,
        string? AgentPhotoUrl,
        int PropertyId,
        string PropertyTitle,
        string PropertySlug,
        DateTime? LastMessageAt,
        int MessageCount,
        int UnreadCount,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        string EscalationStatus,
        string? EscalationReason,
        DateTime? EscalatedAt,
        string? EscalatedByName,
        string? AssignedAdminId,
        string? AssignedAdminName,
        DateTime? AssignedAt,
        DateTime? ResolvedAt,
        string? ResolvedByName);
}
