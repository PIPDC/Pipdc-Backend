namespace PIPDC.Application.Transactions;

/// <summary>
/// Decides how far ahead of a lease end a tenant is reminded, based on how long
/// the tenancy runs. A short let needs a tighter heads-up so the reminder is not
/// sent before the tenant has really settled in, while a longer term is told a
/// month ahead so there is time to renew or re-let.
/// </summary>
public static class LeaseReminderPolicy
{
    /// <summary>Tenancies running this many months or fewer are treated as short.</summary>
    public const int ShortTermMonths = 6;

    /// <summary>Notice given to a short let, in days.</summary>
    public const int ShortTermWindowDays = 7;

    /// <summary>Notice given to a longer let, in days.</summary>
    public const int LongTermWindowDays = 30;

    /// <summary>
    /// The number of days before <paramref name="leaseEndDate"/> that the reminder
    /// falls due for a tenancy running from <paramref name="leaseStartDate"/>.
    /// </summary>
    public static int WindowDays(DateTime leaseStartDate, DateTime leaseEndDate) =>
        MonthsBetween(leaseStartDate, leaseEndDate) <= ShortTermMonths
            ? ShortTermWindowDays
            : LongTermWindowDays;

    /// <summary>
    /// Whole calendar months between the two dates. This is deliberately not the
    /// day-based <c>MonthsIn</c> used for lease term value: a six-month tenancy is
    /// six months whether or not the handful of days between the endpoints rounds
    /// up, and the short/long dividing line is stated in months.
    /// </summary>
    private static int MonthsBetween(DateTime start, DateTime end)
    {
        var months = ((end.Year - start.Year) * 12) + end.Month - start.Month;
        if (end.Date.Day < start.Date.Day)
            months--;

        return Math.Max(months, 0);
    }
}
