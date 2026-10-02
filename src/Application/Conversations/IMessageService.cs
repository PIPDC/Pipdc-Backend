using PIPDC.Domain.Common;

namespace PIPDC.Application.Conversations;

public interface IMessageService
{
    Task<Result<MessageDto>> SendAsync(int conversationId, SendMessageRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    Task<Result<FirstMessageResultDto>> SendByEnquiryAsync(int enquiryId, SendMessageRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    Task<Result<IReadOnlyList<MessageDto>>> GetByConversationAsync(int conversationId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    Task<Result> MarkReadAsync(int conversationId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
}
