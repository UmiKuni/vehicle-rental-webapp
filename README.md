## Development database seed

When the API runs with `ASPNETCORE_ENVIRONMENT=Development`, it automatically applies
pending EF Core migrations and inserts an idempotent development dataset:

- Two users:
  - `owner.seed@example.com`
  - `customer.seed@example.com`
- Three published vehicles for Toyota and VinFast
- Vehicle brands, models, images, and pricing plans

Both seeded users use the development password `Password123!`. The seed only runs in
Development and identifies existing records by email, brand/model ID, and plate number,
so restarting the API does not create duplicates.

The ready-to-run login and search scenarios are in
[`be/src/Host/CarRental.Api/CarRental.Api.http`](be/src/Host/CarRental.Api/CarRental.Api.http).