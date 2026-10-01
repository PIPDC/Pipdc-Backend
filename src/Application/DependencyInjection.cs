using Microsoft.Extensions.DependencyInjection;
using PIPDC.Application.Agents;
using PIPDC.Application.AiChat;
using PIPDC.Application.Blog;
using PIPDC.Application.Conversations;
using PIPDC.Application.Dashboard;
using PIPDC.Application.Developments;
using PIPDC.Application.Enquiries;
using PIPDC.Application.Locations;
using PIPDC.Application.Properties;
using PIPDC.Application.SavedProperties;
using PIPDC.Application.Services;
using PIPDC.Application.Users;

namespace PIPDC.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPropertyService, PropertyService>();
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<IAgentService, AgentService>();
services.AddScoped<IAgentApplicationService, AgentApplicationService>();
        services.AddScoped<IEnquiryService, EnquiryService>();
        services.AddScoped<ISavedPropertyService, SavedPropertyService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IBlogService, BlogService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDashboardService, DashboardService>();

        services.AddScoped<IDevelopmentProjectService, DevelopmentProjectService>();
        services.AddScoped<IDevelopmentProjectPublicService, DevelopmentProjectPublicService>();
        services.AddScoped<IDevelopmentUnitService, DevelopmentUnitService>();
        services.AddScoped<IDevelopmentUpdateService, DevelopmentUpdateService>();
        services.AddScoped<IDevelopmentTrackingService, DevelopmentTrackingService>();
        services.AddScoped<IImageService, ImageService>();

        return services;
    }
}
