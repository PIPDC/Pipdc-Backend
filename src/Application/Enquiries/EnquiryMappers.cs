using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Enquiries;

public static class EnquiryMappers
{
    /// <summary>
    /// An enquiry counts as attended once the agent has read it, and stays
    /// attended when it is subsequently progressed. Acting on an enquiry is
    /// proof of attention, so a resolved or scheduled viewing must never keep
    /// showing as "new" and keep inflating the agent's unread count.
    /// </summary>
    public static bool IsAttended(this Enquiry enquiry) =>
        enquiry.AgentReadAt is not null
        || enquiry.Status is EnquiryStatus.InProgress
            or EnquiryStatus.ViewingScheduled
            or EnquiryStatus.Resolved;

    public static EnquiryDto ToDto(this Enquiry enquiry) =>
        new(
            enquiry.Id,
            enquiry.FullName,
            enquiry.Email,
            enquiry.Phone,
            enquiry.Message,
            enquiry.Status.ToString(),
            enquiry.PropertyId,
            enquiry.Property.Title,
            enquiry.Property.Slug,
            enquiry.UserId,
            enquiry.Property.AgentId,
            enquiry.Property.Agent?.User.FullName ?? string.Empty,
            enquiry.AgentReadAt,
            enquiry.IsAttended(),
            enquiry.CreatedAt,
            enquiry.UpdatedAt);
}
