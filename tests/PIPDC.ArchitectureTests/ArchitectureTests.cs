using NetArchTest.Rules;
using PIPDC.Domain.Common;

namespace PIPDC.ArchitectureTests;

/// <summary>
/// Enforces the layer table of ARCHITECTURE.md section 1 against the single
/// backend assembly (assembly "PIPDC", namespace root "PIPDC"), with the same
/// four request families (rules 1-4), the same boundary constraints (5-7) and
/// the prefix semantics used before consolidation:
///
///     Domain <- Application <- Infrastructure <- API (outward)
///
///   1. Domain does not depend on Application, Infrastructure or API.
///   2. Application does not depend on Infrastructure or API.
///   3. Infrastructure does not depend on API.
///   4. Domain does not use EF Core, ASP.NET Core MVC, SignalR or Npgsql.
///   5. Application does not use Npgsql, MVC, SignalR or ASP.NET Core Http
///      (it MAY use EF Core abstractions).
///   6. Types whose name ends in "Controller" do not depend on IAppDbContext
///      or anything in Infrastructure.
///   7. Application service interfaces (I*Service) each have exactly one
///      implementation, and no Application type ending in "Service" is a
///      public static utility class.
///
/// Each rule is its own fact so a failing boundary reports the exact rule;
/// a mistake in the rule itself is caught by the negative-proof test
/// (Domain_must_reject_an_injected_Infrastructure_reference is temporarily
/// re-armed during development to prove the pipeline detects violations).
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly System.Reflection.Assembly Backend = typeof(Result).Assembly;

    private static void AssertRule(TestResult result)
    {
        if (result.IsSuccessful)
            return;

        var offenders = result.FailingTypeNames is null
            ? "<no failing types reported>"
            : string.Join("\n", result.FailingTypeNames);

        Assert.Fail("Layer/technology boundary violated by:\n" + offenders);
    }

    [Fact]
    public void Domain_does_not_depend_on_Application()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Domain")
            .Should()
            .NotHaveDependencyOn("PIPDC.Application")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Domain_does_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Domain")
            .Should()
            .NotHaveDependencyOn("PIPDC.Infrastructure")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Domain_does_not_depend_on_Api()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Domain")
            .Should()
            .NotHaveDependencyOn("PIPDC.API")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Domain_does_not_use_EfCore()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Domain")
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Domain_does_not_use_AspNetCore_Mvc()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Domain")
            .Should()
            .NotHaveDependencyOn("Microsoft.AspNetCore.Mvc")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Domain_does_not_use_SignalR()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Domain")
            .Should()
            .NotHaveDependencyOn("Microsoft.AspNetCore.SignalR")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Domain_does_not_use_Npgsql()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Domain")
            .Should()
            .NotHaveDependencyOn("Npgsql")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Application_does_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Application")
            .Should()
            .NotHaveDependencyOn("PIPDC.Infrastructure")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Application_does_not_depend_on_Api()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Application")
            .Should()
            .NotHaveDependencyOn("PIPDC.API")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Application_does_not_use_Npgsql()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Application")
            .Should()
            .NotHaveDependencyOn("Npgsql")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Application_does_not_use_AspNetCore_Mvc()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Application")
            .Should()
            .NotHaveDependencyOn("Microsoft.AspNetCore.Mvc")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Application_does_not_use_SignalR()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Application")
            .Should()
            .NotHaveDependencyOn("Microsoft.AspNetCore.SignalR")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Application_does_not_use_AspNetCore_Http()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Application")
            .Should()
            .NotHaveDependencyOn("Microsoft.AspNetCore.Http")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .ResideInNamespaceStartingWith("PIPDC.Infrastructure")
            .Should()
            .NotHaveDependencyOn("PIPDC.API")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Controllers_do_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .HaveNameEndingWith("Controller")
            .Should()
            .NotHaveDependencyOn("PIPDC.Infrastructure")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Controllers_do_not_depend_on_IAppDbContext()
    {
        var result = Types.InAssembly(Backend)
            .That()
            .HaveNameEndingWith("Controller")
            .Should()
            .NotHaveDependencyOn("PIPDC.Application.Data.IAppDbContext")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void Application_service_interfaces_have_exactly_one_implementation()
    {
        var interfaces = Backend.GetTypes()
            .Where(t => t.IsInterface
                        && t.Namespace?.StartsWith("PIPDC.Application", StringComparison.Ordinal) == true
                        && t.Name.StartsWith("I", StringComparison.Ordinal)
                        && t.Name.EndsWith("Service", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(interfaces);

        var failures = new List<string>();
        foreach (var service in interfaces)
        {
            var implementations = Backend.GetTypes()
                .Where(t => !t.IsInterface && !t.IsAbstract && service.IsAssignableFrom(t))
                .ToList();

            if (implementations.Count != 1)
            {
                failures.Add(
                    $"{service.FullName}: {implementations.Count} implementations "
                    + $"[{string.Join(", ", implementations.Select(i => i.FullName))}]");
            }
        }

        Assert.True(failures.Count == 0, "\n" + string.Join("\n", failures));
    }

    [Fact]
    public void No_Application_service_type_is_a_public_static_utility()
    {
        var offenders = Backend.GetTypes()
            .Where(t => t.Namespace?.StartsWith("PIPDC.Application", StringComparison.Ordinal) == true
                        && t.Name.EndsWith("Service", StringComparison.Ordinal)
                        && t.IsAbstract && t.IsSealed)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Service types must be concrete implementations, not static utility classes:\n"
            + string.Join("\n", offenders.Select(t => t.FullName)));
    }
}