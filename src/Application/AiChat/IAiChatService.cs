using PIPDC.Domain.Common;

namespace PIPDC.Application.AiChat;

public interface IAiChatService
{
    Task<Result<AiChatSessionDto>> GetSessionAsync(string userId, CancellationToken ct);
    Task<Result<SendAiMessageResponseDto>> SendAsync(string userId, string content, CancellationToken ct);
    Task<Result<SendAiMessageResponseDto>> SubmitContactPreferenceAsync(string userId, string choice, string? phone, CancellationToken ct);
    Task<Result> DeleteAsync(string userId, CancellationToken ct);
}