using System.Reflection;
using CarRental.BuildingBlocks.Domain;
using FluentAssertions;
using NetArchTest.Rules;

namespace CarRental.ArchitectureTests;

/// <summary>
/// Architectural guardrails for rich domain models and pure domain isolation.
/// Rule 2: Domain entities must not have public setters (no anemic entities).
/// Rule 3: Domain classes must not depend on EF Core or ASP.NET Core packages.
/// </summary>
public class DomainEncapsulationTests
{
    private class FakeAnemicEntity : Entity<Guid>
    {
        public string Title { get; set; } = string.Empty;
    }

    [Fact]
    public void DomainEntities_MustNotHavePublicSetters()
    {
        var assemblies = ArchitectureTestHelper.GetAllSolutionAssemblies();

        var entityTypes = assemblies
            .SelectMany(a =>
            {
                try
                {
                    return a.GetExportedTypes();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            })
            .Where(t => t.IsClass && !t.IsAbstract && IsEntityOrAggregateRoot(t))
            .ToList();

        var violations = new List<string>();

        foreach (var entity in entityTypes)
        {
            var publicSetters = entity.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod != null && p.SetMethod.IsPublic && !IsInitOnly(p))
                .Select(p => $"{entity.FullName}.{p.Name}")
                .ToList();

            violations.AddRange(publicSetters);
        }

        violations.Should().BeEmpty(
            "Domain entities and aggregate roots must encapsulate state and mutate via explicit domain methods, not public setters.");
    }

    [Fact]
    public void DomainEntitiesValidator_CorrectlyDetectsAnemicEntities()
    {
        var properties = typeof(FakeAnemicEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod != null && p.SetMethod.IsPublic && !IsInitOnly(p))
            .Select(p => p.Name)
            .ToList();

        properties.Should().Contain(nameof(FakeAnemicEntity.Title));
    }

    [Fact]
    public void DomainClasses_MustNotDependOn_EFCore_Or_AspNetCore()
    {
        var assemblies = ArchitectureTestHelper.GetAllSolutionAssemblies();

        var domainTypes = Types.InAssemblies(assemblies)
            .That()
            .ResideInNamespaceStartingWith("CarRental.BuildingBlocks.Domain")
            .Or()
            .ResideInNamespaceEndingWith(".Domain")
            .Or()
            .ResideInNamespaceEndingWith(".Entities")
            .Or()
            .ResideInNamespaceContaining(".Domain.")
            .Or()
            .ResideInNamespaceContaining(".Entities.");

        var result = domainTypes
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain layer must not depend on EF Core or ASP.NET Core: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    private static bool IsEntityOrAggregateRoot(Type type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.IsGenericType)
            {
                var genericDef = current.GetGenericTypeDefinition();
                if (genericDef == typeof(Entity<>) || genericDef == typeof(AggregateRoot<>))
                {
                    return true;
                }
            }
            current = current.BaseType;
        }

        return false;
    }

    private static bool IsInitOnly(PropertyInfo property)
    {
        var setMethod = property.SetMethod;
        if (setMethod == null) return false;
        return setMethod.ReturnParameter.GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
    }
}
