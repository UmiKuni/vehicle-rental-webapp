using System.Reflection;

namespace CarRental.ArchitectureTests;

public static class ArchitectureTestHelper
{
    public static readonly string[] ModuleNames =
    [
        "Audit",
        "Availability",
        "Bookings",
        "Fleet",
        "Handover",
        "Notifications",
        "Payments",
        "Pricing",
        "Reporting",
        "Reviews",
        "Search",
        "Support",
        "Users"
    ];

    public static Assembly BuildingBlocksAssembly => typeof(CarRental.BuildingBlocks.Domain.Entity<>).Assembly;

    public static Assembly ApiHostAssembly => Assembly.Load("CarRental.Api");

    public static Assembly LoadAssembly(string name)
    {
        try
        {
            return Assembly.Load(name);
        }
        catch
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"{name}.dll");
            return Assembly.LoadFrom(path);
        }
    }

    public static IReadOnlyList<Assembly> GetModuleAssemblies()
    {
        return ModuleNames
            .Select(m => LoadAssembly($"CarRental.Modules.{m}"))
            .ToList();
    }

    public static IReadOnlyList<Assembly> GetContractsAssemblies()
    {
        return ModuleNames
            .Select(m => LoadAssembly($"CarRental.Modules.{m}.Contracts"))
            .ToList();
    }

    public static IReadOnlyList<Assembly> GetAllSolutionAssemblies()
    {
        var list = new List<Assembly>
        {
            BuildingBlocksAssembly,
            ApiHostAssembly
        };
        list.AddRange(GetModuleAssemblies());
        list.AddRange(GetContractsAssemblies());
        return list;
    }

    public static string FindSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CarRental.slnx")) ||
                File.Exists(Path.Combine(current.FullName, "Directory.Packages.props")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate solution root directory.");
    }
}
