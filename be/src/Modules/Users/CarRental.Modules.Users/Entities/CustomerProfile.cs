using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Users.Entities;

public class CustomerProfile : Entity<UserId>
{
    public UserId UserId => Id;
    public string FullName { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;

    private CustomerProfile() { }

    internal CustomerProfile(UserId userId, string fullName, string address)
    {
        Id = userId;
        FullName = fullName;
        Address = address;
    }

    internal void Update(string fullName, string address)
    {
        FullName = fullName;
        Address = address;
    }
}
