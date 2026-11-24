# Vehicle Rental Admin

A small, API-backed admin console. Uses the same stack and dependency versions as `fe/`: React 19, TypeScript, Vite 8, Tailwind CSS 4, React Router, TanStack Query, Axios, React Hook Form + Zod, Radix Dialog, Lucide icons, and Sonner.

## Run

Use Node 24 (compatible with the frontend tooling).

```powershell
cd admin-portal
Copy-Item .env.example .env # only if .env does not already exist
npm install
npm run dev
```

Open http://localhost:3001. The customer frontend can continue running on port 3000. Configure `VITE_API_BASE_URL` in `.env` if the API is not at `http://localhost:8080/api`. Restart Vite after changing the environment.

```powershell
npm run build
npm run preview
npm run lint
```

The backend must be running and allow the portal's origin through CORS. Production hosting must serve `index.html` for application routes such as `/catalog`.

## Sign in

Use an existing backend account whose role is **Admin**. The portal checks `/api/users/me`; Customer and Owner accounts cannot enter. The backend currently seeds only Owner and Customer accounts, so those seed credentials cannot be used here. An administrator account must be provisioned separately in the backend; this portal does not create or promote admins.

Tokens are stored in sessionStorage for the current tab, with refresh-token rotation and server logout. The API remains responsible for authorization. No mock adapter is installed.

## Features

- **Vehicles:** published list, search within results, lookup by vehicle or owner UUID; approve/reject pending listings and unpublish published listings. Rejection and unpublishing require a reason.
- **Catalog:** list, create, edit, delete brands and models. API validation errors (including items in use) are shown inline.
- **Users:** ban/unban by UUID, with explicit confirmation and a guard against modifying your own account.

## API limitations

There is no admin user directory or pending-approval list endpoint. Use known IDs for those operations. User access changes do not claim to display the account's current status because the API provides no admin account lookup. Mock reporting, booking cancellation, and payment actions are intentionally excluded. Vehicle images are storage keys, not guaranteed public URLs, so the portal does not attempt to render them.

Backend defects identified in review (including search projection synchronization) remain backend concerns; this project does not modify `be/` or `fe/`.
