using System.Reflection;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Messaging;
using FluentAssertions;
using NetArchTest.Rules;

namespace CarRental.ArchitectureTests;

/// <summary>
/// Architectural guardrails for integration contract design.
/// Rule 4: Integration events in *.Contracts must be immutable records.
/// </summary>
public class ContractsDesignTests
{
    private class FakeMutableEvent : IIntegrationEvent
    {
        public Guid EventId { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
        public string Payload { get; set; } = string.Empty;
    }

    [Fact]
    public void IntegrationEvents_MustBeImmutableRecords()
    {
        var assemblies = ArchitectureTestHelper.GetAllSolutionAssemblies();

        var integrationEventTypes = assemblies
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
            .Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        var violations = new List<string>();

        foreach (var eventType in integrationEventTypes)
        {
            // 1. Must be a record
            var isRecord = eventType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Any(m => m.Name == "<Clone>$") ||
                eventType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Any(p => p.Name == "EqualityContract");

            if (!isRecord)
            {
                violations.Add($"{eventType.FullName} is not a C# record.");
            }

            // 2. Must be immutable: no public mutable setters
            var mutableProperties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod != null && p.SetMethod.IsPublic && !IsInitOnly(p))
                .Select(p => $"{eventType.FullName}.{p.Name}")
                .ToList();

            if (mutableProperties.Count > 0)
            {
                violations.Add($"{eventType.FullName} has mutable properties: {string.Join(", ", mutableProperties)}");
            }

            // 3. No public mutable fields
            var mutableFields = eventType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsInitOnly)
                .Select(f => $"{eventType.FullName}.{f.Name}")
                .ToList();

            if (mutableFields.Count > 0)
            {
                violations.Add($"{eventType.FullName} has mutable fields: {string.Join(", ", mutableFields)}");
            }
        }

        violations.Should().BeEmpty("All integration events must be immutable records.");
    }

    [Fact]
    public void IntegrationEvents_NetArchTest_MustBeImmutable()
    {
        var assemblies = ArchitectureTestHelper.GetAllSolutionAssemblies();

        var result = Types.InAssemblies(assemblies)
            .That()
            .ImplementInterface(typeof(IIntegrationEvent))
            .And()
            .AreNotAbstract()
            .And()
            .AreNotInterfaces()
            .Should()
            .BeImmutable()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Integration events must be immutable: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Fact]
    public void IntegrationEventValidator_CorrectlyDetectsMutableClass()
    {
        var isRecord = typeof(FakeMutableEvent).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Any(m => m.Name == "<Clone>$") ||
            typeof(FakeMutableEvent).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Any(p => p.Name == "EqualityContract");

        isRecord.Should().BeFalse();

        var mutableProperties = typeof(FakeMutableEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod != null && p.SetMethod.IsPublic && !IsInitOnly(p))
            .Select(p => p.Name)
            .ToList();

        mutableProperties.Should().Contain(nameof(FakeMutableEvent.Payload));
    }

    private static bool IsInitOnly(PropertyInfo property)
    {
        var setMethod = property.SetMethod;
        if (setMethod == null) return false;
        return setMethod.ReturnParameter.GetRequiredCustomModifiers()
            .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
    }
}
