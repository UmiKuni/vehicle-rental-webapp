using Xunit;

namespace CarRental.IntegrationTests.Fixtures;

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<CarRentalApiFactory>
{
}
