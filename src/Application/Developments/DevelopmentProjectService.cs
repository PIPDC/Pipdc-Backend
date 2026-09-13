using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Developments;

public class DevelopmentProjectService(IAppDbContext dbContext) : IDevelopmentProjectService
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
            p.UpdatedAt)).ToList();

        return Result<PaginatedResult<DevelopmentProjectDto>>.Success(
            PaginatedResult<DevelopmentProjectDto>.Create(dtos, totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<DevelopmentProjectDetailDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var project = await dbContext.DevelopmentProjects
            .Include(p => p.Images)
            .Include(p => p.Units)
            .Include(p => p.Updates)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (project is null)
            return Result<DevelopmentProjectDetailDto>.Failure(
                Error.NotFound("development.notfound", $"Development project with id {id} was not found."));

        return Result<DevelopmentProjectDetailDto>.Success(ToDetailDto(project));
    }

    public async Task<Result<DevelopmentProjectDto>> CreateAsync(CreateDevelopmentProjectRequest request, CancellationToken ct)
    {
        var slug = await EnsureUniqueSlugAsync(request.Slug, request.Name, ct);

        var project = new DevelopmentProject
        {
            Name = request.Name,
            Description = request.Description,
            Slug = slug,
            Location = request.Location,
            LocationRefId = request.LocationRefId,
            Developer = request.Developer,
            Status = string.IsNullOrWhiteSpace(request.Status)
                ? DevelopmentProjectStatus.Planned
                : Enum.Parse<DevelopmentProjectStatus>(request.Status, true),
            ExpectedCompletionDate = request.ExpectedCompletionDate,
            ProgressPercentage = request.ProgressPercentage ?? 0,
            Featured = request.Featured,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.DevelopmentProjects.Add(project);
        await dbContext.SaveChangesAsync(ct);

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

        project.Name = request.Name;
        project.Description = request.Description;
        project.Slug = slug;
        project.Location = request.Location;
        project.LocationRefId = request.LocationRefId;
        project.Developer = request.Developer;
        project.Status = Enum.Parse<DevelopmentProjectStatus>(request.Status, true);
        project.ExpectedCompletionDate = request.ExpectedCompletionDate;
        project.ProgressPercentage = request.ProgressPercentage ?? 0;
        project.Featured = request.Featured;
        project.UpdatedAt = DateTime.UtcNow;

        if (request.Images is not null)
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
            d.Images, d.UnitCount, d.UpdateCount, d.CreatedAt, d.UpdatedAt));
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
                u.Price, u.Currency, u.Description, u.CreatedAt, u.UpdatedAt)).ToList(),
            p.Updates.OrderByDescending(u => u.UpdateDate).Select(u => new DevelopmentUpdateDto(
                u.Id, u.Title, u.Description, u.ProgressPercentage, u.UpdateDate,
                u.ImageUrls, u.ImagePublicIds, u.CreatedAt, u.UpdatedAt)).ToList());
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
}
