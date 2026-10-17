using Microsoft.Extensions.Options;
using PIPDC.Application.Email;
using PIPDC.Infrastructure.Email;

namespace PIPDC.ArchitectureTests;

/// <summary>
/// Covers the one place email link targets are decided. A wrong route here is
/// exactly what sends a call to action to the not-found page, so each of the
/// shapes the templates rely on is asserted directly.
/// </summary>
public sealed class FrontendUrlBuilderTests
{
    private const string Base = "https://pipdc.example.gov.ng";

    private static FrontendUrlBuilder CreateBuilder(
        string? frontendBaseUrl = Base,
        string? gmailBaseUrl = null) =>
        new(
            Options.Create(new FrontendOptions { BaseUrl = frontendBaseUrl ?? string.Empty }),
            Options.Create(new GmailApiSettings { FrontendBaseUrl = gmailBaseUrl ?? string.Empty }));

    [Fact]
    public void Builds_absolute_links_against_the_frontend_base_url()
    {
        var urls = CreateBuilder();

        Assert.Equal(Base + "/", urls.Base);
        Assert.Equal(Base + "/dashboard", urls.Dashboard());
        Assert.Equal(Base + "/dashboard/messages", urls.Messages());
        Assert.Equal(Base + "/dashboard/my-agent-application", urls.MyAgentApplication());
    }

    [Fact]
    public void Enquiry_links_deep_link_into_the_messages_page()
    {
        var urls = CreateBuilder();

        // There is no /enquiries/{id} route in the frontend; the messaging view
        // reads the enquiry query parameter instead.
        Assert.Equal(
            Base + FrontendRoutes.Messages + "?enquiry=42",
            urls.EnquiryMessages(42));
    }

    [Fact]
    public void Verification_and_reset_links_carry_the_encoded_address()
    {
        var urls = CreateBuilder();
        var email = "a+b@example.com";

        Assert.Equal(
            Base + FrontendRoutes.VerifyEmail + "?email=a%2Bb%40example.com",
            urls.VerifyEmail(email));
        Assert.Equal(
            Base + FrontendRoutes.ResetPassword + "?email=a%2Bb%40example.com",
            urls.ResetPassword(email));
    }

    [Fact]
    public void Falls_back_to_the_legacy_gmail_base_url()
    {
        var urls = CreateBuilder(frontendBaseUrl: null, gmailBaseUrl: Base);

        Assert.Equal(Base + "/dashboard", urls.Dashboard());
    }

    [Fact]
    public void Trailing_slash_on_the_base_url_never_doubles_up()
    {
        var urls = CreateBuilder(frontendBaseUrl: Base + "/");

        Assert.Equal(Base + "/dashboard/messages", urls.Messages());
    }

    [Fact]
    public void An_unconfigured_base_url_yields_relative_links()
    {
        // Production refuses to start in this state (see Program). The builder
        // stays tolerant so a development run does not fail on link creation.
        var urls = CreateBuilder(frontendBaseUrl: null);

        Assert.Equal(FrontendRoutes.Dashboard, urls.Dashboard());
    }
}
