using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PIPDC.Application.Auth;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Application.Email;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Developments;

public class DevelopmentProjectService(
    IAppDbContext dbContext,
    UserManager<AppUser> userManager,
    IEmailQueue emailQueue,
    IOptions<GmailApiSettings> gmailOptions,
    IDevelopmentListingPromoter listingPromoter,
    ILogger<DevelopmentProjectService> logger) : IDevelopmentProjectService
{
    public async Task<Result<PaginatedResult<DevelopmentProjectDto>>> GetAllAsync(DevelopmentProjectQueryParameters q, CancellationToken ct)
    {
        IQueryable<DevelopmentProject> query = dbContext.DevelopmentProjects;

        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var search = q.Keyword.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(search)
                                  || p.Description.ToLower().Contains(search)
                                  || p.Location.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(q.Status)
            && Enum.TryParse<DevelopmentProjectStatus>(q.Status, true, out var status))
        {
            query = query.Where(p => p.Status == status);
        }

        if (q.Featured.HasValue)
            query = query.Where(p => p.Featured == q.Featured.Value);

        if (q.LocationId.HasValue)
            query = query.Where(p => p.LocationRefId == q.LocationId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .Include(p => p.Images)
            .Include(p => p.Units)
            .Include(p => p.Updates)
            .Include(p => p.Property)
            .ToListAsync(ct);

        var dtos = items.Select(p => new DevelopmentProjectDto(
            p.Id,
            p.Name,
            p.Slug,
            p.Description,
            p.Location,
            p.LocationRefId,
            p.Developer,
            p.Status.ToString(),
            p.ExpectedCompletionDate,
            p.ProgressPercentage,
            p.Featured,
            p.Images.OrderBy(i => i.DisplayOrder).Select(i => new DevelopmentProjectImageDto(
                i.Id, i.Url, i.PublicId, i.IsCover, i.DisplayOrder)).ToList(),
            p.Units.Count,
            p.Updates.Count,
            p.CreatedAt,
            p.UpdatedAt,
            p.PropertyId,
            p.Property?.Title)).ToList();

        return Result<PaginatedResult<DevelopmentProjectDto>>.Success(
            PaginatedResult<DevelopmentProjectDto>.Create(dtos, totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<DevelopmentProjectDetailDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var project = await dbContext.DevelopmentProjects
            .Include(p => p.Images)
            .Include(p => p.Units)
            .Include(p => p.Updates)
            .Include(p => p.Property)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null)
            return Result<DevelopmentProjectDetailDto>.Failure(
                Error.NotFound("development.notfound", $"Development project with id {id} was not found."));

        return Result<DevelopmentProjectDetailDto>.Success(ToDetailDto(project));
    }

    public async Task<Result<DevelopmentProjectDto>> CreateAsync(CreateDevelopmentProjectRequest request, CancellationToken ct)
    {
        var slug = await EnsureUniqueSlugAsync(request.Slug, request.Name, ct);

        var status = string.IsNullOrWhiteSpace(request.Status)
            ? DevelopmentProjectStatus.Planned
            : Enum.Parse<DevelopmentProjectStatus>(request.Status, true);

        // A project can be linked to its listing on creation, not only on edit.
        // The unique index on PropertyId is what actually enforces one project per
        // listing, so validate it here too and let the admin pick another property
        // instead of hitting a raw database error on save.
        if (request.PropertyId.HasValue)
        {
            // No existing project to exclude: this is a brand new project, so any
            // property already linked to one is unavailable.
            var linkError = await ValidatePropertyLinkAsync(request.PropertyId, projectId: 0, ct);
            if (linkError is not null)
                return Result<DevelopmentProjectDto>.Failure(linkError);
        }

        var project = new DevelopmentProject
        {
            Name = request.Name,
            Description = request.Description,
            Slug = slug,
            Location = request.Location,
            LocationRefId = request.LocationRefId,
            Developer = request.Developer,
            Status = status,
            ExpectedCompletionDate = request.ExpectedCompletionDate,
            // A completed project is by definition finished, so the same rule that
            // UpdateAsync applies has to hold on creation.
            ProgressPercentage = status == DevelopmentProjectStatus.Completed ? 100 : request.ProgressPercentage ?? 0,
            Featured = request.Featured,
            PropertyId = request.PropertyId,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.DevelopmentProjects.Add(project);
        await dbContext.SaveChangesAsync(ct);

        // A project created straight into Completed never had a chance to promote
        // its units, so it does that here too.
        if (status == DevelopmentProjectStatus.Completed)
        {
            var promoted = await listingPromoter.PromoteProjectUnitsAsync(project, ct);
            if (promoted.IsFailure)
                return Result<DevelopmentProjectDto>.Failure(promoted.Error);
        }

        if (request.Images is { Count: > 0 })
        {
            var images = request.Images.Select((img, idx) => new DevelopmentProjectImage
            {
                DevelopmentProjectId = project.Id,
                Url = img.Url,
                PublicId = img.PublicId,
                IsCover = img.IsCover,
                DisplayOrder = img.DisplayOrder == 0 ? idx : img.DisplayOrder
            }).ToList();

            dbContext.DevelopmentProjectImages.AddRange(images);
            await dbContext.SaveChangesAsync(ct);
        }

        return await GetByIdDtoAsync(project.Id, ct);
    }

    public async Task<Result<DevelopmentProjectDto>> UpdateAsync(int id, UpdateDevelopmentProjectRequest request, CancellationToken ct)
    {
        var project = await dbContext.DevelopmentProjects
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null)
            return Result<DevelopmentProjectDto>.Failure(
                Error.NotFound("development.notfound", $"Development project with id {id} was not found."));

        var slug = await EnsureUniqueSlugAsync(request.Slug, request.Name, ct, excludeId: id);

        // Captured before the overwrite below. The whole listing prompt depends on
        // noticing that the status actually moved; comparing against the value we
        // are about to write would make every update look like a transition.
        var previousStatus = project.Status;

        project.Name = request.Name;
        project.Description = request.Description;
        project.Slug = slug;
        project.Location = request.Location;
        project.LocationRefId = request.LocationRefId;
        project.Developer = request.Developer;
        project.Status = Enum.Parse<DevelopmentProjectStatus>(request.Status, true);
        project.ExpectedCompletionDate = request.ExpectedCompletionDate;
        // A project that reports itself as Completed while showing 10% progress is
        // self-contradictory, and the public project page renders that percentage.
        // Completion therefore pins it to 100 rather than storing the contradiction.
        project.ProgressPercentage = project.Status == DevelopmentProjectStatus.Completed
            ? 100
            : request.ProgressPercentage ?? 0;
        project.Featured = request.Featured;
        project.UpdatedAt = DateTime.UtcNow;

        if (request.PropertyId != project.PropertyId)
        {
            var linkError = await ValidatePropertyLinkAsync(request.PropertyId, id, ct);
            if (linkError is not null)
                return Result<DevelopmentProjectDto>.Failure(linkError);
            project.PropertyId = request.PropertyId;
        }

        // Completing a project does not list anything by itself. A project is an estate
        // made of many houses, and it is the units that clients buy. Each unit that
        // has enough detail to stand as a listing is promoted to its own Property
        // here, and the admin is prompted per unit.
        if (project.Status == DevelopmentProjectStatus.Completed)
        {
            var promoted = await listingPromoter.PromoteProjectUnitsAsync(project, ct);
            if (promoted.IsFailure)
                return Result<DevelopmentProjectDto>.Failure(promoted.Error);
        }

if (request.Images is { Count: > 0 })
        {
            var existing = await dbContext.DevelopmentProjectImages
                .Where(i => i.DevelopmentProjectId == id)
                .ToListAsync(ct);

            dbContext.DevelopmentProjectImages.RemoveRange(existing);

            var images = request.Images.Select((img, idx) => new DevelopmentProjectImage
            {
                DevelopmentProjectId = id,
                Url = img.Url,
                PublicId = img.PublicId,
                IsCover = img.IsCover,
                DisplayOrder = img.DisplayOrder == 0 ? idx : img.DisplayOrder
            }).ToList();

            dbContext.DevelopmentProjectImages.AddRange(images);
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<DevelopmentProjectDto>.Failure(Error.Concurrency());
        }

        // Only after the status change is committed, so a notification can never
        // point at a transition that was rolled back.
        if (previousStatus != project.Status)
            await NotifyOnStatusChangeAsync(project, previousStatus, ct);

        return await GetByIdDtoAsync(project.Id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var project = await dbContext.DevelopmentProjects
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null)
            return Result.Failure(
                Error.NotFound("development.notfound", $"Development project with id {id} was not found."));

        dbContext.DevelopmentProjects.Remove(project);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Concurrency());
        }
        return Result.Success();
    }

    public async Task<Result> UpdateFeaturedAsync(int id, bool featured, CancellationToken ct)
    {
        var project = await dbContext.DevelopmentProjects
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null)
            return Result.Failure(
                Error.NotFound("development.notfound", $"Development project with id {id} was not found."));

        project.Featured = featured;
        project.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Concurrency());
        }
        return Result.Success();
    }

    private async Task<Result<DevelopmentProjectDto>> GetByIdDtoAsync(int id, CancellationToken ct)
    {
        var result = await GetByIdAsync(id, ct);
        if (result.IsFailure)
            return Result<DevelopmentProjectDto>.Failure(result.Error);

        var d = result.Value;
        return Result<DevelopmentProjectDto>.Success(new DevelopmentProjectDto(
            d.Id, d.Name, d.Slug, d.Description, d.Location, d.LocationRefId, d.Developer,
            d.Status, d.ExpectedCompletionDate, d.ProgressPercentage, d.Featured,
            d.Images, d.UnitCount, d.UpdateCount, d.CreatedAt, d.UpdatedAt,
            d.PropertyId, d.PropertyTitle));
    }

    private static DevelopmentProjectDetailDto ToDetailDto(DevelopmentProject p)
    {
        return new DevelopmentProjectDetailDto(
            p.Id,
            p.Name,
            p.Slug,
            p.Description,
            p.Location,
            p.LocationRefId,
            p.Developer,
            p.Status.ToString(),
            p.ExpectedCompletionDate,
            p.ProgressPercentage,
            p.Featured,
            p.Images.OrderBy(i => i.DisplayOrder).Select(i => new DevelopmentProjectImageDto(
                i.Id, i.Url, i.PublicId, i.IsCover, i.DisplayOrder)).ToList(),
            p.Units.Count,
            p.Updates.Count,
            p.CreatedAt,
            p.UpdatedAt,
p.Units.OrderBy(u => u.UnitIdentifier).Select(u => new DevelopmentUnitDto(
                 u.Id, u.UnitIdentifier, u.UnitType, u.Status.ToString(),
                 u.Price, u.Currency, u.Description, u.CreatedAt, u.UpdatedAt,
                 u.ListingType.ToString(), u.PropertyType.ToString(), u.Period,
                 u.Bedrooms, u.Bathrooms, u.Size, u.SizeUnit, u.YearBuilt,
                 u.Amenities, u.PropertyId, u.Property?.Status.ToString(),
                 DevelopmentListingPromoter.IsListable(u),
                 DevelopmentListingPromoter.MissingListingDetails(u))).ToList(),
            p.Updates.OrderByDescending(u => u.UpdateDate).Select(u => new DevelopmentUpdateDto(
                u.Id, u.Title, u.Description, u.ProgressPercentage, u.UpdateDate,
                u.ImageUrls, u.ImagePublicIds, u.CreatedAt, u.UpdatedAt)).ToList(),
            p.PropertyId,
            p.Property?.Title,
            p.Property?.Slug,
            p.Property?.Status.ToString());
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLower(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "project" : slug;
    }

    private async Task<string> EnsureUniqueSlugAsync(string? slug, string name, CancellationToken ct, int? excludeId = null)
    {
        var baseSlug = Slugify(string.IsNullOrWhiteSpace(slug) ? name : slug);
        var candidate = baseSlug;
        var suffix = 2;

        while (await dbContext.DevelopmentProjects.AnyAsync(p => p.Slug == candidate && (excludeId == null || p.Id != excludeId), ct))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    /// <summary>
    /// The listing created from a project needs its own slug namespace. Projects
    /// and properties are separate tables with separate unique indexes and
    /// separate public routes, so reusing the project's uniqueness check would let
    /// the new listing collide with an unrelated property.
    /// </summary>
    private async Task<string> EnsureUniquePropertySlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slugify(name);
        var candidate = baseSlug;
        var suffix = 2;

        while (await dbContext.Properties.AnyAsync(p => p.Slug == candidate, ct))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }
    public async Task<Result<IEnumerable<NotificationDto>>> GetNotificationsAsync(string userId, CancellationToken ct)
    {
        var notifications = await dbContext.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.Link, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
        return Result<IEnumerable<NotificationDto>>.Success(notifications);
    }

    public async Task<Result> MarkNotificationReadAsync(int notificationId, string userId, CancellationToken ct)
    {
        var notification = await dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);
        if (notification is null)
            return Result.Failure(Error.NotFound("notification.notfound", "Notification not found"));
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await dbContext.SaveChangesAsync(ct);
        }
        return Result.Success();
    }

    public async Task<Result<int>> CreateNotificationAsync(string userId, string type, string title, string message, string? link, string deduplicationKey, CancellationToken ct)
    {
        var existing = await dbContext.Notifications.FirstOrDefaultAsync(n => n.UserId == userId && n.DeduplicationKey == deduplicationKey, ct);
        if (existing is not null)
            return Result<int>.Success(existing.Id);
        var n = new Notification { UserId = userId, Type = type, Title = title, Message = message, Link = link, DeduplicationKey = deduplicationKey, IsRead = false, CreatedAt = DateTime.UtcNow };
        dbContext.Notifications.Add(n);
        await dbContext.SaveChangesAsync(ct);
        return Result<int>.Success(n.Id);
    }

    /// <summary>
    /// Fans a development project's status change out to the two audiences that
    /// care about it: administrators, who may need to turn the project into a
    /// listing, and the users tracking the project, who asked to be told.
    /// </summary>
    /// <remarks>
    /// Both go through the existing Notification table so there is still exactly
    /// one notification channel. Administrators additionally get an email,
    /// because they are the ones expected to act on a completion. Neither path
    /// publishes anything: listing stays an explicit admin decision.
    /// </remarks>
    private async Task NotifyOnStatusChangeAsync(
        DevelopmentProject project,
        DevelopmentProjectStatus previousStatus,
        CancellationToken ct)
    {
        // ── Tracked users ────────────────────────────────────────────────
        // Everyone watching the project hears about any status move. In-app only:
        // people opt into tracking a project, they did not opt into email.
        var trackers = await dbContext.DevelopmentTrackings
            .AsNoTracking()
            .Where(t => t.DevelopmentProjectId == project.Id)
            .Select(t => t.UserId)
            .Distinct()
            .ToListAsync(ct);

        var statusText = HumanizeStatus(project.Status);
        foreach (var trackerId in trackers)
        {
            var key = $"development-status:{project.Id}:{trackerId}:{previousStatus}->{project.Status}";
            await CreateNotificationAsync(
                trackerId,
                "DevelopmentStatusChanged",
                $"{project.Name}: {statusText}",
                $"The development project you are tracking moved from {HumanizeStatus(previousStatus)} to {statusText}.",
                $"/developments/{project.Slug}",
                key,
                ct);
        }

        // ── Administrators ───────────────────────────────────────────────
        // The prompt is per unit, because a unit is what gets listed. Telling an
        // admin to "list the project" was misleading: the project is the estate and
        // is never a listing itself.
        if (project.Status != DevelopmentProjectStatus.Completed)
            return;

        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
        if (admins.Count == 0)
            return;

        var units = await dbContext.DevelopmentUnits
            .AsNoTracking()
            .Where(u => u.DevelopmentProjectId == project.Id)
            .ToListAsync(ct);

        var listed = units.Where(u => u.PropertyId is not null).ToList();
        var needsDetail = units
            .Where(u => u.PropertyId is null && !DevelopmentListingPromoter.IsListable(u))
            .ToList();

        var parts = new List<string>();
        if (listed.Count > 0)
            parts.Add($"{listed.Count} unit(s) are ready to list");
        if (needsDetail.Count > 0)
            parts.Add(
                $"{needsDetail.Count} still need a price and unit details before they can be listed");
        if (parts.Count == 0)
            return;

        var title = "Development project completed";
        var message =
            $"\"{project.Name}\" is now Completed: {string.Join(", and ", parts)}. " +
            "Open the project's Units tab to review each unit's listing details and publish.";
        var link = $"/dashboard/developments/{project.Id}?tab=units";

        foreach (var admin in admins)
        {
            // Keyed on project plus the transition that triggered it, so repeated
            // saves that do not change status add nothing, while a genuine later
            // re-completion still notifies.
            var key = $"development-completed:{project.Id}:{previousStatus}->{project.Status}";
            await CreateNotificationAsync(admin.Id, "DevelopmentCompleted", title, message, link, key, ct);

            if (admin.Email is null)
                continue;

            emailQueue.QueueEmail(logger, EmailTemplates.DevelopmentStatusChanged(
                admin.Email, admin.FullName, project.Name, previousStatus.ToString(), project.Status.ToString(), gmailOptions.Value.FrontendBaseUrl), "development-status-changed");
        }
    }

    /// <summary>
    /// Renders a status for a human sentence. The stored enum names read as
    /// identifiers (<c>NearCompletion</c>), which is wrong inside a notification.
    /// </summary>
    private static string HumanizeStatus(DevelopmentProjectStatus status) => status switch
    {
        DevelopmentProjectStatus.Planned => "Planned",
        DevelopmentProjectStatus.UnderConstruction => "Under Construction",
        DevelopmentProjectStatus.NearCompletion => "Near Completion",
        DevelopmentProjectStatus.Completed => "Completed",
        DevelopmentProjectStatus.OnHold => "On Hold",
        _ => status.ToString(),
    };

    /// <summary>
    /// Rejects a link to a property that does not exist, or to a property already
    /// claimed by another development project.
    /// </summary>
    /// <returns>The failure to report, or <c>null</c> when the link is valid.</returns>
    private async Task<Error?> ValidatePropertyLinkAsync(int? propertyId, int projectId, CancellationToken ct)
    {
        if (propertyId is null)
            return null;

        var propertyExists = await dbContext.Properties
            .AsNoTracking()
            .AnyAsync(p => p.Id == propertyId.Value, ct);

        if (!propertyExists)
            return Error.Validation("development.propertyid.notfound", "The selected property does not exist.");

        var alreadyLinked = await dbContext.DevelopmentProjects
            .AsNoTracking()
            .AnyAsync(p => p.PropertyId == propertyId.Value && p.Id != projectId, ct);

        if (alreadyLinked)
            return Error.Validation("development.propertyid.taken", "That property is already linked to another development project.");

        return null;
    }

}
