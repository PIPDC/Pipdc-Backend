using Microsoft.Extensions.DependencyInjection;
using PIPDC.Application.Agents;
using PIPDC.Application.AiChat;
using PIPDC.Application.Blog;
using PIPDC.Application.Contact;
using PIPDC.Application.Conversations;
using PIPDC.Application.Dashboard;
using PIPDC.Application.Developments;
using PIPDC.Application.Enquiries;
using PIPDC.Application.Locations;
using PIPDC.Application.Properties;
using PIPDC.Application.SavedProperties;
using PIPDC.Application.Services;
using PIPDC.Application.Transactions;
using PIPDC.Application.Users;

namespace PIPDC.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPropertyService, PropertyService>();
    services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IAgentApplicationService, AgentApplicationService>();
        services.AddScoped<IAgentReportService, AgentReportService>();
        services.AddScoped<IAgentReviewService, AgentReviewService>();

        // Scoped, not singleton: the generator queries IAppDbContext to avoid
        // reissuing a licence number that is already taken, and a DbContext is
        // scoped. A singleton here would be a captive dependency and would also
        // hold a DbContext that is never disposed.
        services.AddScoped<IAgentLicenseGenerator, AgentLicenseGenerator>();
        services.AddScoped<IEnquiryService, EnquiryService>();
        services.AddScoped<ISavedPropertyService, SavedPropertyService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IConversationEscalationService, ConversationEscalationService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IBlogService, BlogService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IContactService, ContactService>();

        services.AddScoped<IDevelopmentProjectService, DevelopmentProjectService>();
        services.AddScoped<IDevelopmentProjectPublicService, DevelopmentProjectPublicService>();
        services.AddScoped<IDevelopmentUnitService, DevelopmentUnitService>();
        // Shared by the project and unit services: both can promote units, so the
        // rule that decides what a listing needs lives in one place.
        services.AddScoped<IDevelopmentListingPromoter, DevelopmentListingPromoter>();
        services.AddScoped<IDevelopmentUpdateService, DevelopmentUpdateService>();
        services.AddScoped<IDevelopmentTrackingService, DevelopmentTrackingService>();
        services.AddScoped<IImageService, ImageService>();

        return services;
    }
}
