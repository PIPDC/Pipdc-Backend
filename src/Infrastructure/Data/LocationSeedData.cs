using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Infrastructure.Data;

/// <summary>
/// Canonical seed data for the location hierarchy.
///
/// Extracted from <see cref="AppDbContext"/> so that seed data has one owner and
/// can be extended per level (State -> LGA -> City -> Area) without further
/// growing the DbContext.
///
/// IMPORTANT: only the 37 Nigerian States are seeded. No LGA, City or Area rows
/// are added, because no authoritative dataset has been approved for this
/// project. Populating those levels must not be done with guessed or
/// approximate data. An empty level is handled by the application (the
/// location picker degrades to free text), not by fabricating rows.
/// </summary>
internal static class LocationSeedData
{
    public static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The 36 states plus the Federal Capital Territory.</summary>
    public static readonly (string Name, string Slug)[] States =
    [
        ("Abia", "abia"),
        ("Adamawa", "adamawa"),
        ("Akwa Ibom", "akwa-ibom"),
        ("Anambra", "anambra"),
        ("Bauchi", "bauchi"),
        ("Bayelsa", "bayelsa"),
        ("Benue", "benue"),
        ("Borno", "borno"),
        ("Cross River", "cross-river"),
        ("Delta", "delta"),
        ("Ebonyi", "ebonyi"),
        ("Edo", "edo"),
        ("Ekiti", "ekiti"),
        ("Enugu", "enugu"),
        ("FCT", "fct"),
        ("Gombe", "gombe"),
        ("Imo", "imo"),
        ("Jigawa", "jigawa"),
        ("Kaduna", "kaduna"),
        ("Kano", "kano"),
        ("Katsina", "katsina"),
        ("Kebbi", "kebbi"),
        ("Kogi", "kogi"),
        ("Kwara", "kwara"),
        ("Lagos", "lagos"),
        ("Nasarawa", "nasarawa"),
        ("Niger", "niger"),
        ("Ogun", "ogun"),
        ("Ondo", "ondo"),
        ("Osun", "osun"),
        ("Oyo", "oyo"),
        ("Plateau", "plateau"),
        ("Rivers", "rivers"),
        ("Sokoto", "sokoto"),
        ("Taraba", "taraba"),
        ("Yobe", "yobe"),
        ("Zamfara", "zamfara"),
    ];

    /// <summary>
    /// Seed rows for the location table. Root rows use explicit Ids 1..N so the
    /// data stays stable across migrations.
    /// </summary>
    public static Location[] BuildStateRows() =>
        States.Select((s, i) => new Location
        {
            Id = i + 1,
            Name = s.Name,
            Slug = s.Slug,
            Type = LocationType.State,
            ParentId = null,
            CreatedAt = SeedDate,
        }).ToArray();
}
