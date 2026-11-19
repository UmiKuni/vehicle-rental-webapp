using System.Xml.Linq;
using CarRental.ArchitectureTests.Rules;
using FluentAssertions;
using NetArchTest.Rules;

namespace CarRental.ArchitectureTests;

/// <summary>
/// Architectural guardrails for modular monolith encapsulation.
/// Rule 1: Modules must not reference other modules' internal namespaces (only *.Contracts and CarRental.BuildingBlocks).
/// </summary>
public class ModuleEncapsulationTests
{
    [Fact]
    public void ModuleProjects_MustNotReferenceOtherModulesInternalProjects()
    {
        var solutionRoot = ArchitectureTestHelper.FindSolutionRoot();
        var modulesDir = Path.Combine(solutionRoot, "src", "Modules");

        var moduleCsprojFiles = Directory.GetFiles(modulesDir, "CarRental.Modules.*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains(".Contracts.csproj", StringComparison.OrdinalIgnoreCase))
            .ToList();

        moduleCsprojFiles.Should().NotBeEmpty("Module projects must be discovered in src/Modules");

        var violations = new List<string>();

        foreach (var csprojFile in moduleCsprojFiles)
        {
            var currentModuleName = Path.GetFileNameWithoutExtension(csprojFile).Replace("CarRental.Modules.", string.Empty);
            var doc = XDocument.Load(csprojFile);
            var projectReferences = doc.Descendants("ProjectReference")
                .Select(pr => pr.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();

            foreach (var pr in projectReferences)
            {
                var targetFileName = Path.GetFileName(pr);
                if (!string.IsNullOrEmpty(targetFileName) &&
                    targetFileName.StartsWith("CarRental.Modules.", StringComparison.OrdinalIgnoreCase) &&
                    !targetFileName.Contains(".Contracts.csproj", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{currentModuleName} references forbidden internal project '{targetFileName}' in {csprojFile}");
                }
            }
        }

        violations.Should().BeEmpty(
            "Modules must only reference other modules' *.Contracts projects and CarRental.BuildingBlocks.");
    }

    [Fact]
    public void Modules_MustNotDependOn_OtherModulesInternalNamespaces()
    {
        foreach (var moduleName in ArchitectureTestHelper.ModuleNames)
        {
            var moduleAssembly = ArchitectureTestHelper.LoadAssembly($"CarRental.Modules.{moduleName}");
            var types = Types.InAssembly(moduleAssembly);

            if (!types.GetTypes().Any())
            {
                continue;
            }

            var rule = new NoCrossModuleInternalDependencyRule(moduleName, ArchitectureTestHelper.ModuleNames);
            var result = types.Should().MeetCustomRule(rule).GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Module '{moduleName}' violates isolation boundaries: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
        }
    }
    [Fact]
    public void ManagedModules_ImplementationTypes_ShouldBeInternal()
    {
        string[] managedModules = ["Pricing", "Availability", "Bookings", "Payments", "Handover", "Reporting"];
        string[] internalNamespaceSuffixes = [".Entities", ".Data", ".Services", ".Validators", ".Options", ".Errors"];

        var publicViolations = new List<string>();

        foreach (var moduleName in managedModules)
        {
            var assembly = ArchitectureTestHelper.LoadAssembly($"CarRental.Modules.{moduleName}");
            var prefix = $"CarRental.Modules.{moduleName}";

            var offendingTypes = assembly.GetTypes()
                .Where(t => !t.IsNested
                            && t.IsPublic
                            && !string.IsNullOrEmpty(t.Namespace)
                            && !string.Equals(t.Namespace, $"{prefix}.Data.Migrations", StringComparison.Ordinal)
                            && internalNamespaceSuffixes.Any(suffix =>
                                string.Equals(t.Namespace, $"{prefix}{suffix}", StringComparison.Ordinal)
                                || t.Namespace.StartsWith($"{prefix}{suffix}.", StringComparison.Ordinal)))
                .Select(t => t.FullName!)
                .ToList();

            publicViolations.AddRange(offendingTypes);
        }

        publicViolations.Should().BeEmpty(
            "Implementation types in managed modules (Entities, Data, Services, Validators, Options, Errors) must be internal.");
    }
}
