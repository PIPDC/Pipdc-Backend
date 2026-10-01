using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Agents;
using PIPDC.Application.Auth;
using PIPDC.Application.Data;
using PIPDC.Application.Enquiries;
using PIPDC.Application.Properties;
using PIPDC.Application.SavedProperties;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Dashboard;

public class DashboardService(
    IAppDbContext dbContext,
    UserManager<AppUser> userManager,
    IPropertyService propertyService,
    IEnquiryService enquiryService,
    ISavedPropertyService savedPropertyService,
    IAgentService agentService) : IDashboardService
{
    public async Task<Result<AdminDashboardDto>> GetAdminAsync(string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var totalProperties = await dbContext.Properties.CountAsync(ct);
        var totalAgents = await dbContext.Agents.CountAsync(ct);
        var totalEnquiries = await dbContext.Enquiries.CountAsync(ct);
        var totalUsers = await userManager.Users.CountAsync(ct);
        var totalDevelopmentProjects = await dbContext.DevelopmentProjects.CountAsync(ct);
        var totalBlogPosts = await dbContext.BlogPosts.CountAsync(ct);

        var properties = await propertyService.GetAllAsync(
            new PropertyQueryParameters { PageSize = 5 }, currentUserId, includeSuspendedAgents: true, ct);
        if (properties.IsFailure)
            return Result<AdminDashboardDto>.Failure(properties.Error);

        var enquiries = await enquiryService.GetAllAsync(
            new EnquiryQueryParameters { PageSize = 5 }, currentUserId, currentUserRoles, ct);
        if (enquiries.IsFailure)
            return Result<AdminDashboardDto>.Failure(enquiries.Error);

        return Result<AdminDashboardDto>.Success(new AdminDashboardDto(
            totalProperties,
            totalAgents,
            totalEnquiries,
            totalUsers,
            totalDevelopmentProjects,
            totalBlogPosts,
            properties.Value.Items,
            enquiries.Value.Items));
    }

    public async Task<Result<AgentDashboardDto>> GetAgentAsync(string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var profile = await agentService.GetMyProfileAsync(currentUserId, ct);
        if (profile.IsFailure)
            return Result<AgentDashboardDto>.Failure(profile.Error);

        var totalProperties = await dbContext.Properties
            .CountAsync(p => p.AgentId == profile.Value.Id, ct);

        var properties = await propertyService.GetAllAsync(
            new PropertyQueryParameters { AgentId = profile.Value.Id, PageSize = 5 }, currentUserId, includeSuspendedAgents: true, ct);
        if (properties.IsFailure)
            return Result<AgentDashboardDto>.Failure(properties.Error);

        var enquiries = await enquiryService.GetAllAsync(
            new EnquiryQueryParameters { PageSize = 5 }, currentUserId, currentUserRoles, ct);
        if (enquiries.IsFailure)
            return Result<AgentDashboardDto>.Failure(enquiries.Error);

        var pending = await enquiryService.GetAllAsync(
            new EnquiryQueryParameters { PageSize = 1, Status = EnquiryStatus.Pending.ToString() },
            currentUserId, currentUserRoles, ct);
        if (pending.IsFailure)
            return Result<AgentDashboardDto>.Failure(pending.Error);

        return Result<AgentDashboardDto>.Success(new AgentDashboardDto(
            profile.Value,
            totalProperties,
            properties.Value.Items,
            enquiries.Value.TotalCount,
            pending.Value.TotalCount,
            enquiries.Value.Items));
    }

    public async Task<Result<ClientDashboardDto>> GetClientAsync(string currentUserId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(currentUserId);
        if (user is null)
            return Result<ClientDashboardDto>.Failure(
                Error.NotFound("user.notfound", "User not found."));

        var roles = await userManager.GetRolesAsync(user);
        var profile = new CurrentUserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.FullName,
            roles);

        var saved = await savedPropertyService.GetSavedAsync(
            currentUserId, new SavedPropertyQueryParameters { PageSize = 5 }, ct);
        if (saved.IsFailure)
            return Result<ClientDashboardDto>.Failure(saved.Error);

        var enquiries = await enquiryService.GetMineAsync(
            currentUserId, new EnquiryQueryParameters { PageSize = 5 }, ct);
        if (enquiries.IsFailure)
            return Result<ClientDashboardDto>.Failure(enquiries.Error);

        var pending = await enquiryService.GetMineAsync(
            currentUserId, new EnquiryQueryParameters { PageSize = 1, Status = EnquiryStatus.Pending.ToString() }, ct);
        if (pending.IsFailure)
            return Result<ClientDashboardDto>.Failure(pending.Error);

        return Result<ClientDashboardDto>.Success(new ClientDashboardDto(
            profile,
            saved.Value.TotalCount,
            saved.Value.Items.Select(sp => sp.Property).ToList(),
            enquiries.Value.TotalCount,
            pending.Value.TotalCount,
            enquiries.Value.Items));
    }
}
