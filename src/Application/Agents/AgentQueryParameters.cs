namespace PIPDC.Application.Agents;

public class AgentQueryParameters
{
    public string? Keyword { get; set; }
    public bool? IsVerified { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;

    /// <summary>
    /// Opt in to seeing suspended and revoked agents as well as visible ones.
    /// Only honoured for an admin, and only when explicitly requested.
    /// </summary>
    /// <remarks>
    /// This used to be implied by the caller holding the Admin role. That meant an
    /// admin browsing the public directory at /agents received every revoked and
    /// suspended agent, rendered indistinguishably from a live one, because the
    /// list DTO carried no revocation state. Admin visibility of moderated agents
    /// is a real need, but it belongs to the admin directory, which asks for it.
    /// </remarks>
    public bool IncludeModerated { get; set; }

    public int? Page { get; set; }

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
        set => _pageSize = value is < 1 or > 100 ? 10 : value;
    }

    public int EffectivePageNumber => Math.Max(Page ?? PageNumber, 1);
}
