namespace PIPDC.Application.AiChat;

public class ConciergeEscalationQueryParameters
{
    private int _pageNumber = 1;
    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    private int _pageSize = 10;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 ? 10 : value > 100 ? 100 : value;
    }

    /// <summary>
    /// Optional escalation status filter for the admin queue. Left null it shows
    /// every escalation, unresolved first, so the queue doubles as an audit trail.
    /// Values that do not parse are ignored rather than rejected, so a stale
    /// bookmark still loads.
    /// </summary>
    public string? Status { get; set; }
}