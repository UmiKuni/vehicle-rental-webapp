# 🚗 Vehicle Rental — Frontend

Admin Dashboard cho hệ thống quản lý thuê xe, xây dựng theo chuẩn **Production-Ready React**.

---

## ⚡ Tech Stack

| Lớp | Công nghệ |
|-----|-----------|
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

## 🗂️ Cấu trúc thư mục

```
fe/
├── src/
│   ├── app/
│   │   ├── layouts/
│   │   │   └── DashboardLayout.tsx   # Sidebar + Topbar layout
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
│   │   └── not-found/
│   │       └── pages/NotFoundPage.tsx
│   │
│   ├── shared/                        # Tái sử dụng toàn cục
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

## 🚀 Hướng dẫn chạy

### 1. Clone và cài đặt

```bash
cd fe
cp .env.example .env
npm install
```

### 2. Chạy development server

```bash
npm run dev
# → http://localhost:3000
```

> **Demo login:** username `admin` / password `admin123`
>
> Mock API tự động hoạt động trong dev mode — không cần backend.

### 3. Kết nối Backend thực

Khi backend Spring Boot đã sẵn sàng:

1. Sửa `.env`:
   ```env
   VITE_API_BASE_URL=http://localhost:8080/api
   ```
2. Xoá hoặc disable import mock API trong `src/main.tsx`:
   ```ts
   // import './shared/lib/mock-api' ← Bỏ comment này
   ```

### 4. Build production

```bash
npm run build
npm run preview
```

---

## 🔍 Linting & Formatting

```bash
npm run lint          # Kiểm tra ESLint
npm run lint:fix      # Tự sửa ESLint
npm run format        # Format Prettier
npm run format:check  # Kiểm tra format
```

---

## 🌗 Dark / Light Mode

Toggle nút 🌙/☀️ ở góc trên bên phải. Preference được lưu vào `localStorage`.

---

## 🔐 RBAC — Phân quyền

| Role | Quyền |
|------|-------|
| `ADMIN` | Toàn quyền |
| `MANAGER` | Quản lý người dùng & phương tiện |
| `STAFF` | Xem và thao tác cơ bản |

> ⚠️ Backend **luôn là nơi xác thực thực tế**. Frontend chỉ ẩn/hiện UI dựa trên role.

---

## 📡 API Contract (Spring Boot)

### Base URL
```
http://localhost:8080/api
```

### Auth endpoints
| Method | Path | Body |
|--------|------|------|
| POST | `/auth/login` | `{ username, password }` |
| POST | `/auth/logout` | — |
| POST | `/auth/refresh` | `{ refreshToken }` |
| GET | `/auth/me` | — |

### Users endpoints
| Method | Path | Params / Body |
|--------|------|--------------|
| GET | `/users` | `?page&size&search&sort&direction` |
| GET | `/users/:id` | — |
| POST | `/users` | `CreateUserRequest` |
| PUT | `/users/:id` | `UpdateUserRequest` |
| DELETE | `/users/:id` | — |

### Response format
```json
{
  "success": true,
  "message": "OK",
  "data": { ... }
}
```

### Paginated response
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

## 🛠️ Thêm feature mới

1. Tạo thư mục `src/features/<feature-name>/`
2. Thêm types vào `src/types/` nếu cần
3. Tạo `api/`, `schemas/`, `hooks/`, `components/`, `pages/`
4. Đăng ký route trong `src/app/router/index.tsx`
5. Thêm nav item vào `src/app/layouts/DashboardLayout.tsx`

---

## 📦 Tích hợp Backend Spring Boot

- API Client: [`src/shared/lib/axios.ts`](src/shared/lib/axios.ts) — chỉ cần thay `VITE_API_BASE_URL`
- Refresh Token: tự động xử lý trong Axios interceptor
- CORS: cấu hình Spring Boot cho phép `http://localhost:3000`

---

*Built by Senior Frontend Architect — Production-Ready, Feature-Based, TypeScript Strict.*
