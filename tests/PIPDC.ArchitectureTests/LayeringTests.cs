using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using PIPDC.API.Hubs;
using PIPDC.Domain.Entities;

namespace PIPDC.ArchitectureTests;

/// <summary>
/// Compiler-enforced version of the reference graph in ARCHITECTURE.md section 1.
///
/// The project split already makes the forbidden references *unbuildable*; these
/// tests restate the rule and fail loudly if someone wires a bad ProjectReference
/// (or otherwise sneaks a layer in).
///
///     Domain <- Application <- Infrastructure
///     API references Application + Infrastructure.
///     Nothing references API except API itself.
///
/// How it works: we walk every TypeReference in each layer assembly's metadata
/// (the compiler emits one for every external type used anywhere - fields, method
/// bodies, base types, attributes, generics) and assert the set of referenced
/// namespaces never starts with a forbidden root.
///
/// Why not NetArchTest.Rules: its HaveDependencyOn matches an exact namespace
/// string, and every PIPDC layer is split across sub-namespaces
/// (PIPDC.Application.Properties, ...), so the rule passes vacuously - a real
/// Application -> Infrastructure.Whatever reference would not be caught. This
/// scanner has prefix semantics and depends only on the in-box
/// System.Reflection.Metadata.
/// </summary>
public sealed class LayeringTests
{
    [Fact]
    public void Domain_references_nothing_from_any_other_layer()
    {
        LayeringAssertions.LacksReferenceRoots(
            typeof(AppUser).Assembly,
            "PIPDC.Domain",
            "PIPDC.Application",
            "PIPDC.Infrastructure",
            "PIPDC.API");
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_Api_or_SignalR()
    {
        LayeringAssertions.LacksReferenceRoots(
            typeof(Application.Conversations.IMessageNotifier).Assembly,
            "PIPDC.Application",
            "PIPDC.Infrastructure",
            "PIPDC.API",
            // SignalR hubs are an API-layer concern; from step 1 on the
            // Application layer communicates through IMessageNotifier instead.
            "Microsoft.AspNetCore.SignalR");
    }

    [Fact]
    public void Infrastructure_does_not_reference_API()
    {
        LayeringAssertions.LacksReferenceRoots(
            typeof(Infrastructure.DependencyInjection).Assembly,
            "PIPDC.Infrastructure",
            "PIPDC.API");
    }

    [Fact]
    public void Reference_graph_edges_that_must_exist_are_detected()
    {
        // Positive controls: proves the scanner actually sees real edges, so
        // the LacksReferenceRoots rules above are not passing vacuously.
        LayeringAssertions.HasReferenceRoot(typeof(Application.Conversations.IMessageNotifier).Assembly, "PIPDC.Domain.");
        LayeringAssertions.HasReferenceRoot(typeof(Infrastructure.DependencyInjection).Assembly, "PIPDC.Application.");
        LayeringAssertions.HasReferenceRoot(typeof(Infrastructure.DependencyInjection).Assembly, "PIPDC.Domain.");
        LayeringAssertions.HasReferenceRoot(typeof(MessagingHub).Assembly, "PIPDC.Application.");
        LayeringAssertions.HasReferenceRoot(typeof(MessagingHub).Assembly, "PIPDC.Infrastructure.");
    }

    [Fact]
    public void Controllers_depend_only_on_Application_service_interfaces()
    {
        var controllerTypes = typeof(MessagingHub).Assembly.GetTypes()
            .Where(t => t.Namespace == "PIPDC.API.Controllers" && t.IsPublic)
            .ToList();

        Assert.NotEmpty(controllerTypes);

        var violations = controllerTypes
            .SelectMany(t => t.GetConstructors())
            .SelectMany(c => c.GetParameters())
            .Where(p =>
            {
                var ns = p.ParameterType.Namespace ?? string.Empty;
                return ns.StartsWith("PIPDC.Infrastructure", StringComparison.Ordinal)
                    || ns.StartsWith("PIPDC.Domain", StringComparison.Ordinal)
                    || p.ParameterType.Name == "IAppDbContext";
            })
            .Select(p => $"{p.Member.DeclaringType?.Name}.ctor({p.ParameterType.FullName})")
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Controllers must take Application service interfaces only (no data access, "
            + "no Infrastructure service types, no Domain entities):\n"
            + string.Join("\n", violations));
    }
}

internal static class LayeringAssertions
{
    /// <summary>
    /// Asserts that every namespace the given assembly's types reference is
    /// either framework/PIPDC-unspecified or matches none of <paramref name="forbiddenRoots"/>.
    /// Intersection semantics with the assembly's own root are excluded from the
    /// scan; forbidden roots are checked with prefix semantics.
    /// </summary>
    public static void LacksReferenceRoots(Assembly assembly, string ownRoot, params string[] forbiddenRoots)
    {
        var referenced = GetReferencedNamespaces(assembly.Location)
            .Where(ns => !ns.StartsWith(ownRoot + ".", StringComparison.Ordinal))
            .ToArray();

        var hits = referenced
            .Where(ns => forbiddenRoots.Any(root => ns.StartsWith(root, StringComparison.Ordinal)))
            .OrderBy(ns => ns)
            .ToArray();

        Assert.True(
            hits.Length == 0,
            $"{assembly.GetName().Name} references these forbidden namespaces:\n"
            + string.Join("\n", hits.Distinct()));
    }

    public static void HasReferenceRoot(Assembly assembly, string root)
    {
        var referenced = GetReferencedNamespaces(assembly.Location)
            .Where(ns => ns.StartsWith(root, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            referenced.Length > 0,
            $"{assembly.GetName().Name} should reference {root} — scanner blind to that edge?");
    }

    /// <summary>
    /// Collects the namespace of every TypeReference in the module metadata.
    /// The C# compiler emits a TypeReference for each external type referenced
    /// from any member (including method bodies and attributes), so this is the
    /// complete, precision set of the assembly's outward type dependencies.
    /// </summary>
    private static IEnumerable<string> GetReferencedNamespaces(string assemblyPath)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        using var stream = File.OpenRead(assemblyPath);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();
        foreach (var handle in metadata.TypeReferences)
        {
            var name = metadata.GetString(metadata.GetTypeReference(handle).Namespace);
            if (!string.IsNullOrWhiteSpace(name))
                namespaces.Add(name);
        }
        return namespaces;
    }
}