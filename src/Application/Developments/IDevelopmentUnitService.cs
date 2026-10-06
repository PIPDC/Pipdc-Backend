using PIPDC.Domain.Common;

namespace PIPDC.Application.Developments;

public interface IDevelopmentUnitService
{
    Task<Result<IReadOnlyList<DevelopmentUnitDto>>> GetByProjectAsync(int projectId, CancellationToken ct);
    Task<Result<DevelopmentUnitDto>> CreateAsync(int projectId, CreateDevelopmentUnitRequest request, CancellationToken ct);
    Task<Result<DevelopmentUnitDto>> UpdateAsync(int projectId, int unitId, UpdateDevelopmentUnitRequest request, CancellationToken ct);
    Task<Result<DevelopmentUnitDto>> PromoteAsync(int projectId, int unitId, CancellationToken ct);
    Task<Result> DeleteAsync(int projectId, int unitId, CancellationToken ct);
}
