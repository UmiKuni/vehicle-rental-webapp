# Stage 1: Base Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Stage 2: Build & Restore Cache
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and centralized package management configuration
COPY Directory.Build.props Directory.Packages.props CarRental.slnx ./

# Copy Host, Shared, and Test project files
COPY src/Host/CarRental.Api/*.csproj src/Host/CarRental.Api/
COPY src/Shared/CarRental.BuildingBlocks/*.csproj src/Shared/CarRental.BuildingBlocks/
COPY tests/CarRental.ArchitectureTests/*.csproj tests/CarRental.ArchitectureTests/
COPY tests/CarRental.BuildingBlocks.Tests/*.csproj tests/CarRental.BuildingBlocks.Tests/
COPY tests/CarRental.Modules.Tests/*.csproj tests/CarRental.Modules.Tests/

# Copy Core Rental Flow Module projects
COPY src/Modules/Bookings/CarRental.Modules.Bookings/*.csproj src/Modules/Bookings/CarRental.Modules.Bookings/
COPY src/Modules/Bookings/CarRental.Modules.Bookings.Contracts/*.csproj src/Modules/Bookings/CarRental.Modules.Bookings.Contracts/
COPY src/Modules/Pricing/CarRental.Modules.Pricing/*.csproj src/Modules/Pricing/CarRental.Modules.Pricing/
COPY src/Modules/Pricing/CarRental.Modules.Pricing.Contracts/*.csproj src/Modules/Pricing/CarRental.Modules.Pricing.Contracts/
COPY src/Modules/Availability/CarRental.Modules.Availability/*.csproj src/Modules/Availability/CarRental.Modules.Availability/
COPY src/Modules/Availability/CarRental.Modules.Availability.Contracts/*.csproj src/Modules/Availability/CarRental.Modules.Availability.Contracts/
COPY src/Modules/Payments/CarRental.Modules.Payments/*.csproj src/Modules/Payments/CarRental.Modules.Payments/
COPY src/Modules/Payments/CarRental.Modules.Payments.Contracts/*.csproj src/Modules/Payments/CarRental.Modules.Payments.Contracts/
COPY src/Modules/Handover/CarRental.Modules.Handover/*.csproj src/Modules/Handover/CarRental.Modules.Handover/
COPY src/Modules/Handover/CarRental.Modules.Handover.Contracts/*.csproj src/Modules/Handover/CarRental.Modules.Handover.Contracts/

# Copy Catalog & People Module projects
COPY src/Modules/Users/CarRental.Modules.Users/*.csproj src/Modules/Users/CarRental.Modules.Users/
COPY src/Modules/Users/CarRental.Modules.Users.Contracts/*.csproj src/Modules/Users/CarRental.Modules.Users.Contracts/
COPY src/Modules/Fleet/CarRental.Modules.Fleet/*.csproj src/Modules/Fleet/CarRental.Modules.Fleet/
COPY src/Modules/Fleet/CarRental.Modules.Fleet.Contracts/*.csproj src/Modules/Fleet/CarRental.Modules.Fleet.Contracts/
COPY src/Modules/Search/CarRental.Modules.Search/*.csproj src/Modules/Search/CarRental.Modules.Search/
COPY src/Modules/Search/CarRental.Modules.Search.Contracts/*.csproj src/Modules/Search/CarRental.Modules.Search.Contracts/
COPY src/Modules/Reviews/CarRental.Modules.Reviews/*.csproj src/Modules/Reviews/CarRental.Modules.Reviews/
COPY src/Modules/Reviews/CarRental.Modules.Reviews.Contracts/*.csproj src/Modules/Reviews/CarRental.Modules.Reviews.Contracts/

# Copy Supporting & Platform Module projects
COPY src/Modules/Support/CarRental.Modules.Support/*.csproj src/Modules/Support/CarRental.Modules.Support/
COPY src/Modules/Support/CarRental.Modules.Support.Contracts/*.csproj src/Modules/Support/CarRental.Modules.Support.Contracts/
COPY src/Modules/Notifications/CarRental.Modules.Notifications/*.csproj src/Modules/Notifications/CarRental.Modules.Notifications/
COPY src/Modules/Notifications/CarRental.Modules.Notifications.Contracts/*.csproj src/Modules/Notifications/CarRental.Modules.Notifications.Contracts/
COPY src/Modules/Audit/CarRental.Modules.Audit/*.csproj src/Modules/Audit/CarRental.Modules.Audit/
COPY src/Modules/Audit/CarRental.Modules.Audit.Contracts/*.csproj src/Modules/Audit/CarRental.Modules.Audit.Contracts/
COPY src/Modules/Reporting/CarRental.Modules.Reporting/*.csproj src/Modules/Reporting/CarRental.Modules.Reporting/
COPY src/Modules/Reporting/CarRental.Modules.Reporting.Contracts/*.csproj src/Modules/Reporting/CarRental.Modules.Reporting.Contracts/

# Restore dependencies across the entire solution
RUN dotnet restore CarRental.slnx

# Copy complete backend source code
COPY . .

# Build API Host in Release configuration
WORKDIR /src/src/Host/CarRental.Api
RUN dotnet build CarRental.Api.csproj -c Release --no-restore

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish CarRental.Api.csproj -c Release -o /app/publish /p:UseAppHost=false --no-build

# Stage 4: Final Runtime
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CarRental.Api.dll"]
