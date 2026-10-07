using System.Reflection;
using PIPDC.API.Hubs;
using PIPDC.Domain.Entities;

namespace PIPDC.ArchitectureTests;

/// <summary>
/// Single-assembly version of the reference graph in ARCHITECTURE.md section 1.
///
/// The backend is now ONE project (src/PIPDC.csproj, assembly "PIPDC"), so the
/// old per-assembly metadata scanner cannot express the layering any more: within
/// one module every layer is a TypeDef, not a TypeReference, so cross-assembly
/// metadata has nothing to say. These tests restate the direction of dependency
/// by reflecting over the single assembly's types and asserting that a type in a
/// layer never declares a member/attribute whose type lives in a lower layer.
///
///     Domain <- Application <- Infrastructure <- API (outward)
///     Application may not use Infrastructure or API;
///     Infrastructure may not use API.
///
/// Precise enough to police the layer table; replaced by the NetArchTest.Rules
/// suite (same rules, one assembly, prefix semantics) in the consolidation's
/// step 3.
/// </summary>
public sealed class LayeringTests
{
    private static readonly Assembly Backend = typeof(AppUser).Assembly;

    [Fact]
    public void Domain_does_not_reference_Application_Infrastructure_or_Api()
    {
        var hits = LayerReferenceVisitor.Violations("PIPDC.Domain", new[] { "PIPDC.Application", "PIPDC.Infrastructure", "PIPDC.API" });
        Assert.True(hits.Count == 0, "Domain must not reference any other layer:\n" + string.Join("\n", hits));
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_Api_or_SignalR()
    {
        // SignalR hubs are an API-layer concern; the Application layer
        // communicates through IMessageNotifier instead.
        var hits = LayerReferenceVisitor.Violations("PIPDC.Application", new[] { "PIPDC.Infrastructure", "PIPDC.API", "Microsoft.AspNetCore.SignalR" });
        Assert.True(hits.Count == 0, "Application must not reference Infrastructure, API or SignalR:\n" + string.Join("\n", hits));
    }

    [Fact]
    public void Infrastructure_does_not_reference_Api()
    {
        var hits = LayerReferenceVisitor.Violations("PIPDC.Infrastructure", new[] { "PIPDC.API" });
        Assert.True(hits.Count == 0, "Infrastructure must not reference API:\n" + string.Join("\n", hits));
    }

    [Fact]
    public void Reference_graph_edges_that_must_exist_are_detected()
    {
        // Positive controls: proves the scanner actually sees real edges, so the
        // rules above are not passing vacuously.
        Assert.True(LayerReferenceVisitor.HasRoot("PIPDC.Application", "PIPDC.Domain."), "Application -> Domain edge not detected");
        Assert.True(LayerReferenceVisitor.HasRoot("PIPDC.Infrastructure", "PIPDC.Application."), "Infrastructure -> Application edge not detected");
        Assert.True(LayerReferenceVisitor.HasRoot("PIPDC.Infrastructure", "PIPDC.Domain."), "Infrastructure -> Domain edge not detected");
        Assert.True(LayerReferenceVisitor.HasRoot("PIPDC.API", "PIPDC.Application."), "API -> Application edge not detected");
        Assert.True(LayerReferenceVisitor.HasRoot("PIPDC.API", "PIPDC.Infrastructure."), "API -> Infrastructure edge not detected");
    }

    [Fact]
    public void Controllers_depend_only_on_Application_service_interfaces()
    {
        var controllerTypes = Backend.GetTypes()
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

internal static class LayerReferenceVisitor
{
    /// <summary>
    /// For every type whose namespace starts with <paramref name="layerRoot"/>,
    /// collects the namespaces of every type it declares (base types, interfaces,
    /// fields, properties, events, method/constructor signatures, generic
    /// arguments, attributes) and returns those that start with a forbidden root.
    /// </summary>
    public static List<string> Violations(string layerRoot, string[] forbiddenRoots)
    {
        var assembly = typeof(AppUser).Assembly;
        var hits = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            if (string.IsNullOrEmpty(type.Namespace) ||
                !type.Namespace.StartsWith(layerRoot, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var ns in ReferencedNamespaces(type))
            {
                if (forbiddenRoots.Any(root => ns.StartsWith(root, StringComparison.Ordinal)))
                    hits.Add($"{type.FullName} -> {ns}");
            }
        }

        return hits.Distinct().ToList();
    }

    public static bool HasRoot(string layerRoot, string root)
    {
        var assembly = typeof(AppUser).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            if (string.IsNullOrEmpty(type.Namespace) ||
                !type.Namespace.StartsWith(layerRoot, StringComparison.Ordinal))
            {
                continue;
            }

            if (ReferencedNamespaces(type).Any(ns => ns.StartsWith(root, StringComparison.Ordinal)))
                return true;
        }

        return false;
    }

    private static HashSet<string> ReferencedNamespaces(Type type)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var flags = BindingFlags.Public | BindingFlags.NonPublic
                  | BindingFlags.Instance | BindingFlags.Static
                  | BindingFlags.DeclaredOnly;

        void Add(Type? t)
        {
            if (t is null) return;

            if (t.IsGenericType)
            {
                AddTypeAndNamespace(t);
                foreach (var argument in t.GetGenericArguments())
                    Add(argument);
                return;
            }

            if (t.IsArray)
            {
                Add(t.GetElementType());
                return;
            }

            AddTypeAndNamespace(t);
        }

        void AddTypeAndNamespace(Type t)
        {
            if (!string.IsNullOrWhiteSpace(t.Namespace))
                set.Add(t.Namespace!);
        }

        for (var b = type.BaseType; b is not null; b = b.BaseType)
            Add(b);

        foreach (var i in type.GetInterfaces())
            Add(i);

        foreach (var f in type.GetFields(flags))
            Add(f.FieldType);

        foreach (var p in type.GetProperties(flags))
            Add(p.PropertyType);

        foreach (var e in type.GetEvents(flags))
            Add(e.EventHandlerType);

        foreach (var m in type.GetMethods(flags))
        {
            Add(m.ReturnType);
            foreach (var p in m.GetParameters())
                Add(p.ParameterType);
            foreach (var ga in m.GetGenericArguments())
                Add(ga);
            foreach (var attr in m.GetCustomAttributesData())
                Add(attr.AttributeType);
        }

        foreach (var c in type.GetConstructors(flags))
        {
            foreach (var p in c.GetParameters())
                Add(p.ParameterType);
            foreach (var attr in c.GetCustomAttributesData())
                Add(attr.AttributeType);
        }

        foreach (var ga in type.GetGenericArguments())
            Add(ga);

        foreach (var attr in type.GetCustomAttributesData())
            Add(attr.AttributeType);

        return set;
    }
}