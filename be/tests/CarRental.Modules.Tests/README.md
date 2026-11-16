# `CarRental.Modules.Tests` Organization Guide

To keep module unit tests easy to navigate and avoid monolithic `*ModuleTests.cs` files, split tests into a folder per module by layer and use `Moq` (`Mock<T>`) directly in each test class:

```text
be/tests/CarRental.Modules.Tests/
├── <Module>/
│   ├── <Module>DomainTests.cs          # Aggregate / entity / value-object invariants
│   ├── <Module>ApiServiceTests.cs      # I*Api application service logic + *Errors catalog checks
│   ├── <Module>EventHandlerTests.cs    # Wolverine integration event handlers (when applicable)
│   └── <JobName>Tests.cs               # BackgroundService / cleanup jobs (when applicable)
└── ...
```

## Guidelines for New or Refactored Modules

1. **Keep using this single `CarRental.Modules.Tests.csproj` project** — do not create separate `.csproj` files per module.
2. **Use `Moq` (`Mock<T>`) directly for dependencies** (`IFleetApi`, `IUsersApi`, `IPricingApi`, `IAvailabilityApi`, `IEventOutbox`, repositories, `TimeProvider`, `DbContext.SaveChangesAsync`) instead of hand-writing stub/fake classes.
3. **Match namespaces to folders** (`CarRental.Modules.Tests.<Module>`), so tests can be filtered cleanly:
   - Run all unit tests for one module:
     ```powershell
     dotnet test be/tests/CarRental.Modules.Tests/CarRental.Modules.Tests.csproj --filter "FullyQualifiedName~CarRental.Modules.Tests.Availability"
     ```
   - Run only one layer / class:
     ```powershell
     dotnet test be/tests/CarRental.Modules.Tests/CarRental.Modules.Tests.csproj --filter "FullyQualifiedName~AvailabilityApiServiceTests"
     ```
4. **When expanding an older flat `*ModuleTests.cs` file:** move its domain tests into `<Module>/<Module>DomainTests.cs` (namespace `CarRental.Modules.Tests.<Module>`) and add service/handler tests in separate files inside `<Module>/`.
