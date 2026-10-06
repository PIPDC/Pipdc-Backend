using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Developments;

public class DevelopmentProjectPublicService(IAppDbContext dbContext) : IDevelopmentProjectPublicService
{
    public async Task<Result<PaginatedResult<DevelopmentProjectDto>>> GetPublicAllAsync(DevelopmentProjectQueryParameters q, CancellationToken ct)
    {
        IQueryable<DevelopmentProject> query = dbContext.DevelopmentProjects
            .Where(p => p.Status != DevelopmentProjectStatus.OnHold);

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

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.Featured).ThenByDescending(p => p.CreatedAt)
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

    public async Task<Result<DevelopmentProjectDetailDto>> GetPublicBySlugAsync(string slug, CancellationToken ct)
    {
        var project = await dbContext.DevelopmentProjects
            .Include(p => p.Images)
            .Include(p => p.Units)
            .Include(p => p.Updates)
            .FirstOrDefaultAsync(p => p.Slug == slug, ct);

        if (project is null)
            return Result<DevelopmentProjectDetailDto>.Failure(
                Error.NotFound("development.notfound", $"Development project with slug '{slug}' was not found."));

        return Result<DevelopmentProjectDetailDto>.Success(ToDetailDto(project));
    }

    public async Task<Result<DevelopmentProjectDetailDto>> GetPublicByIdAsync(int id, CancellationToken ct)
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
                 u.Amenities,
                 // Listing state is internal admin information: whether a unit has
                 // been promoted, and what it still needs. Not exposed publicly.
                 null, null, false, [])).ToList(),
            p.Updates.OrderByDescending(u => u.UpdateDate).Select(u => new DevelopmentUpdateDto(
                u.Id, u.Title, u.Description, u.ProgressPercentage, u.UpdateDate,
                u.ImageUrls, u.ImagePublicIds, u.CreatedAt, u.UpdatedAt)).ToList());
    }
}
