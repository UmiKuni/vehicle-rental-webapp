## Frontend deployment

Deploy the single React app in [`fe/`](fe/). It contains both the customer portal and the Admin workspace, so CI/CD only needs one frontend build. Admin routes live under `/admin`; signed-in Admins can switch to the customer portal without changing sessions.

## Development database seed

When the API runs with `ASPNETCORE_ENVIRONMENT=Development`, it automatically applies
pending EF Core migrations and inserts an idempotent development dataset:

- Three seeded accounts:
  - `admin.seed@example.com`
  - `owner.seed@example.com`
  - `customer.seed@example.com`
- 8 vehicle brands (Toyota, VinFast, Honda, Hyundai, Mazda, Kia, Mitsubishi, Ford)
- 15 vehicle models across sedans, electric crossovers, MPVs, and SUVs
- 12 published vehicles across Ho Chi Minh City (Auto & Manual transmissions, 5-seater & 7-seater options)
- Vehicle images, pricing plans, and pre-indexed search projections

Both seeded users use the development password `Password123!`. The seed only runs in
Development and identifies existing records by email, brand/model ID, and plate number,
so restarting the API does not create duplicates.

The ready-to-run login and search scenarios are in
[`be/src/Host/CarRental.Api/CarRental.Api.http`](be/src/Host/CarRental.Api/CarRental.Api.http).
