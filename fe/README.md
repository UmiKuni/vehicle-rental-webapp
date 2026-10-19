# 🚗 Vehicle Rental — Frontend

Admin Dashboard & Mobile PWA for vehicle rental management, built according to **Production-Ready React** standards.

---

## ⚡ Tech Stack

| Layer | Technology |
|---|---|
| Framework | React 19 + TypeScript (Strict) + Vite 8 |
| Styling | Tailwind CSS v4 + shadcn/ui primitives (Radix UI) |
| Server State | TanStack Query v5 |
| Routing | React Router v7 |
| HTTP Client | Axios (Interceptors + Refresh Token) |
| Form & Validation | React Hook Form + Zod |
| Client State | React Context + useState |
| Notifications | Sonner (toast) |
| Icons | Lucide React |
| Date | date-fns |
| Linting | ESLint 10 + Prettier |

---

## 🗂️ Directory Structure

```
fe/
├── src/
│   ├── app/
│   │   ├── layouts/
│   │   │   ├── DashboardLayout.tsx   # Sidebar + Topbar layout (Desktop)
│   │   │   └── MobileLayout.tsx      # Bottom Navigation layout (Mobile/PWA)
│   │   ├── providers/
│   │   │   └── AppProviders.tsx      # QueryClient + Theme + Auth
│   │   ├── router/
│   │   │   ├── index.tsx             # createBrowserRouter, lazy pages
│   │   │   └── ProtectedRoute.tsx    # Auth guard + RBAC
│   │   └── styles/
│   │       └── globals.css           # Design tokens (light/dark) + Tailwind v4
│   │
│   ├── features/                     # Feature-Based Architecture
│   │   ├── auth/
│   │   │   ├── api/auth.api.ts
│   │   │   ├── hooks/useLogin.ts
│   │   │   ├── pages/LoginPage.tsx
│   │   │   └── schemas/login.schema.ts
│   │   ├── dashboard/
│   │   │   └── pages/DashboardPage.tsx
│   │   ├── users/
│   │   │   ├── api/
│   │   │   │   ├── user.api.ts        # CRUD API functions
│   │   │   │   └── user.query-keys.ts # Query key factory
│   │   │   ├── components/
│   │   │   │   ├── CreateUserDialog.tsx
│   │   │   │   ├── DeleteUserDialog.tsx
│   │   │   │   ├── EditUserDialog.tsx
│   │   │   │   ├── UserRoleBadge.tsx
│   │   │   │   ├── UserStatusBadge.tsx
│   │   │   │   └── UserTable.tsx      # Full data table with search, pagination
│   │   │   ├── hooks/useUsers.ts      # Query + Mutation hooks
│   │   │   ├── pages/UsersPage.tsx
│   │   │   └── schemas/user.schema.ts # Zod schemas
│   │   ├── vehicles/
│   │   ├── bookings/
│   │   ├── profile/
│   │   └── not-found/
│   │       └── pages/NotFoundPage.tsx
│   │
│   ├── shared/                        # Shared across the application
│   │   ├── components/
│   │   │   ├── common/
│   │   │   │   ├── Pagination.tsx
│   │   │   │   ├── Skeleton.tsx
│   │   │   │   └── StateViews.tsx     # ErrorState, EmptyState
│   │   │   └── ui/
│   │   │       ├── Badge.tsx
│   │   │       ├── Button.tsx
│   │   │       ├── Dialog.tsx
│   │   │       ├── FormField.tsx
│   │   │       ├── Input.tsx
│   │   │       └── Select.tsx
│   │   ├── constants/index.ts
│   │   ├── contexts/
│   │   │   ├── AuthContext.tsx        # Auth state + login/logout
│   │   │   └── ThemeContext.tsx       # Dark/Light/System mode
│   │   ├── hooks/
│   │   │   └── useDebounce.ts
│   │   ├── lib/
│   │   │   ├── axios.ts              # Axios instance + Refresh Token
│   │   │   ├── mock-api.ts           # Dev-only mock interceptor
│   │   │   └── query-client.ts       # QueryClient config
│   │   └── utils/index.ts            # cn, formatDate, getInitials, …
│   │
│   ├── types/                         # Centralized type definitions
│   │   ├── api.types.ts              # ApiResponse<T>, PaginatedResponse<T>
│   │   ├── auth.types.ts             # AuthUser, TokenResponse, UserRole
│   │   ├── common.types.ts
│   │   ├── pagination.types.ts
│   │   └── user.types.ts             # UserResponse, CreateUserRequest, …
│   │
│   └── main.tsx                       # Entry point
│
├── .env.example
├── .prettierrc
├── eslint.config.js
├── tsconfig.app.json
├── vite.config.ts
└── README.md
```

---

## 🚀 Getting Started

### 1. Clone and Install Dependencies

```bash
cd fe
cp .env.example .env
npm install
```

### 2. Run Development Server

```bash
npm run dev
# → http://localhost:3000
```

> **Demo login:** username `admin` / password `admin123`
>
> Mock API runs automatically in development mode — no backend required.

### 3. Connect to Real Backend

When the backend (e.g. Spring Boot) is ready:

1. Update `.env`:
   ```env
   VITE_API_BASE_URL=http://localhost:8080/api
   ```
2. Disable the mock API in `src/main.tsx`:
   ```ts
   // import './shared/lib/mock-api'
   ```

### 4. Build for Production

```bash
npm run build
npm run preview
```

---

## 🔍 Linting & Formatting

```bash
npm run lint          # Run ESLint checks
npm run lint:fix      # Automatically fix ESLint issues
npm run format        # Format with Prettier
npm run format:check  # Check formatting with Prettier
```

---

## 🌗 Dark / Light Mode

Toggle the 🌙/☀️ button on the top right. Preference is persisted in `localStorage`.

---

## 🔐 RBAC — Role-Based Access Control

| Role | Permissions |
|---|---|
| `ADMIN` | Full permissions |
| `MANAGER` | User & vehicle management |
| `STAFF` | View & basic operations |
| `CUSTOMER` | Bookings, profile & search |

> ⚠️ The backend is **always the source of truth for authorization**. The frontend conditionally displays UI based on role.

---

## 📡 API Contract (Spring Boot)

### Base URL
```
http://localhost:8080/api
```

### Auth Endpoints
| Method | Path | Body |
|---|---|---|
| POST | `/auth/login` | `{ username, password }` |
| POST | `/auth/logout` | — |
| POST | `/auth/refresh` | `{ refreshToken }` |
| GET | `/auth/me` | — |

### Users Endpoints
| Method | Path | Params / Body |
|---|---|---|
| GET | `/users` | `?page&size&search&sort&direction` |
| GET | `/users/:id` | — |
| POST | `/users` | `CreateUserRequest` |
| PUT | `/users/:id` | `UpdateUserRequest` |
| DELETE | `/users/:id` | — |

### Response Format
```json
{
  "success": true,
  "message": "OK",
  "data": { ... }
}
```

### Paginated Response
```json
{
  "success": true,
  "data": {
    "content": [...],
    "totalElements": 100,
    "totalPages": 10,
    "size": 10,
    "number": 0,
    "first": true,
    "last": false,
    "empty": false
  }
}
```

---

## 🛠️ Adding a New Feature

1. Create a feature directory `src/features/<feature-name>/`
2. Add types in `src/types/` if needed
3. Create `api/`, `schemas/`, `hooks/`, `components/`, `pages/`
4. Register routes in `src/app/router/index.tsx`
5. Add navigation items in `src/app/layouts/DashboardLayout.tsx` or `MobileLayout.tsx`

---

## 📦 Backend Integration

- API Client: [`src/shared/lib/axios.ts`](src/shared/lib/axios.ts) — configure via `VITE_API_BASE_URL`
- Refresh Token: automatically handled in Axios interceptors
- CORS: configure backend to allow `http://localhost:3000`

---

*Built by Senior Frontend Architect — Production-Ready, Feature-Based, TypeScript Strict.*
