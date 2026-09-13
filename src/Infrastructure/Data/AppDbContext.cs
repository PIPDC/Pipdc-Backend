using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PIPDC.Application.Data;
using PIPDC.Domain.Auth;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options), IAppDbContext
{
    private static readonly ValueConverter<DateTime, DateTime> DateTimeToUtcConverter = new(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableDateTimeToUtcConverter = new(
        v => v.HasValue
            ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime())
            : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Enquiry> Enquiries => Set<Enquiry>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SaleRecord> SaleRecords => Set<SaleRecord>();
    public DbSet<LeaseRecord> LeaseRecords => Set<LeaseRecord>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<BlogPostTag> BlogPostTags => Set<BlogPostTag>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<SavedProperty> SavedProperties => Set<SavedProperty>();
    public DbSet<AiChatSession> AiChatSessions => Set<AiChatSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();
    public DbSet<DevelopmentProject> DevelopmentProjects => Set<DevelopmentProject>();
    public DbSet<DevelopmentUnit> DevelopmentUnits => Set<DevelopmentUnit>();
    public DbSet<DevelopmentUpdate> DevelopmentUpdates => Set<DevelopmentUpdate>();
    public DbSet<DevelopmentProjectImage> DevelopmentProjectImages => Set<DevelopmentProjectImage>();
    public DbSet<DevelopmentTracking> DevelopmentTrackings => Set<DevelopmentTracking>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Market Trends", Slug = "market-trends", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Category { Id = 2, Name = "Investment Guide", Slug = "investment-guide", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Category { Id = 3, Name = "Legal & Tax", Slug = "legal-tax", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Category { Id = 4, Name = "Home Buying Tips", Slug = "home-buying-tips", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        SeedLocations(builder);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(DateTimeToUtcConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(NullableDateTimeToUtcConverter);
            }
        }
    }

    private static void SeedLocations(ModelBuilder builder)
    {
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var states = new (string Name, string Slug)[]
        {
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
        };

        var locationData = states.Select((s, i) => new Location
        {
            Id = i + 1,
            Name = s.Name,
            Slug = s.Slug,
            Type = LocationType.State,
            ParentId = null,
            CreatedAt = seedDate,
        }).ToArray();

        builder.Entity<Location>().HasData(locationData);
    }

}
