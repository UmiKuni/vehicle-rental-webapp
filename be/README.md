# 🚗 Car Rental Platform - Backend API

Hệ thống Backend cho nền tảng cho thuê xe tự lái và xe du lịch (**Car Rental Platform**), được xây dựng theo kiến trúc **Modular Monolith** hiện đại trên nền tảng **.NET 9**, **PostgreSQL 17 (PostGIS)**, **Redis 7**, **Wolverine Message Bus & Outbox**, và giao diện tài liệu API tương tác với **Scalar**.

---

## 📑 Mục lục

- [1. Tổng quan Kiến trúc](#1-tổng-quan-kiến-trúc)
- [2. Yêu cầu Hệ thống](#2-yêu-cầu-hệ-thống)
- [3. Cấu hình Môi trường (.env)](#3-cấu-hình-môi-trường-env)
- [4. Khởi chạy với Docker Compose (Khuyên dùng)](#4-khởi-chạy-với-docker-compose-khuyên-dùng)
- [5. Tài liệu API & Kiểm thử (Scalar / OpenAPI)](#5-tài-liệu-api--kiểm-thử-scalar--openapi)
- [6. Hướng dẫn Phát triển Cục bộ (Local Development)](#6-hướng-dẫn-phát-triển-cục-bộ-local-development)
- [7. Cấu trúc Dự án (Project Structure)](#7-cấu-trúc-dự-án-project-structure)
- [8. Khắc phục Sự cố (Troubleshooting)](#8-khắc-phục-sự-cố-troubleshooting)

---

## 1. Tổng quan Kiến trúc

Hệ thống chia tách rõ ràng thành **13 Modules nghiệp vụ độc lập**, áp dụng chiến lược **Schema-per-Module** trên cùng một Database instance:

| Module | Schema | Chức năng chính |
| :--- | :--- | :--- |
| **Users** | `users` | Quản lý người dùng, định danh (Identity), quyền hạn (Roles/Claims), KYC |
| **Fleet** | `fleet` | Quản lý đội xe, chủ xe (Host), thông tin xe, trang thiết bị, giấy tờ |
| **Pricing** | `pricing` | Bảng giá cơ bản, phụ phí, giảm giá theo thời gian, tính giá dự kiến (Quote) |
| **Availability** | `availability` | Quản lý lịch xe, chặn ngày bảo dưỡng, kiểm tra xe trống |
| **Bookings** | `booking` | Đặt xe, xác nhận/hủy đơn đặt xe, trạng thái vòng đời chuyến đi |
| **Payments** | `payments` | Cổng thanh toán (VNPay, MoMo), tiền cọc (Deposit), hoàn tiền, hóa đơn |
| **Handover** | `handover` | Biên bản bàn giao nhận/trả xe, kiểm tra ODO, tình trạng thân vỏ, ảnh check-in/out |
| **Reviews** | `reviews` | Đánh giá & phản hồi về xe và khách thuê sau chuyến đi |
| **Support** | `support` | Hỗ trợ sự cố, yêu cầu bồi thường, xử lý tranh chấp khiếu nại |
| **Search** | `search` | Tìm kiếm xe theo vị trí địa lý (PostGIS), bộ lọc nâng cao |
| **Notifications** | `notifications` | Gửi email, SMS, thông báo đẩy (Push Notifications) |
| **Audit** | `audit` | Lưu vết hoạt động và lịch sử thao tác hệ thống |
| **Reporting** | `reporting` | Báo cáo doanh thu, thống kê hiệu suất đội xe, dashboard quản trị |

---

## 2. Yêu cầu Hệ thống

Tùy theo hình thức chạy dự án, bạn cần cài đặt các công cụ sau:

- **Chạy toàn bộ qua Docker (Khuyến nghị):**
  - [Docker Desktop](https://www.docker.com/products/docker-desktop/) (hỗ trợ Docker Compose v2+)
- **Chạy trực tiếp mã nguồn trên máy (Local Dev):**
  - [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
  - PostgreSQL 17 (kèm extensions `postgis`, `btree_gist`, `uuid-ossp`)
  - Redis 7+

---

## 3. Cấu hình Môi trường (.env)

Trước khi khởi chạy hệ thống, hãy tạo file `.env` từ file mẫu `.env.example`:

```bash
# Di chuyển vào thư mục be (nếu chưa đứng tại đây)
cd be

# Sao chép file cấu hình mẫu
cp .env.example .env
```

### Các biến môi trường chính:

| Biến | Giá trị mặc định | Mô tả |
| :--- | :--- | :--- |
| `POSTGRES_DB` | `carrental` | Tên cơ sở dữ liệu PostgreSQL |
| `POSTGRES_USER` | `postgres` | Tài khoản quản trị database |
| `POSTGRES_PASSWORD` | `your_secure_password_here` | Mật khẩu database |
| `POSTGRES_PORT` | `5432` | Cổng map ra máy chủ host của PostgreSQL |
| `REDIS_PORT` | `6379` | Cổng map ra máy chủ host của Redis |
| `APP_PORT` | `8080` | Cổng truy cập ứng dụng Backend API |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Môi trường ASP.NET Core (`Development` / `Production`) |

> [!NOTE]
> Khi `ASPNETCORE_ENVIRONMENT=Development`, ứng dụng sẽ **tự động áp dụng Migrations** cho toàn bộ 13 DbContexts khi khởi động và kích hoạt tài liệu API tương tác **Scalar**.

---

## 4. Khởi chạy với Docker Compose (Khuyên dùng)

Hệ thống được cấu hình sẵn 3 dịch vụ trong file `docker-compose.yml`:
1. `carrental-postgres`: PostgreSQL 17 với PostGIS.
2. `carrental-redis`: Redis 7 Alpine.
3. `carrental-api`: Ứng dụng Backend .NET 9.

### 4.1. Khởi động hệ thống

Chạy lệnh sau tại thư mục `be`:

```bash
docker compose up --build -d
```

### 4.2. Kiểm tra trạng thái các Containers

```bash
docker compose ps
```

Kết quả mẫu khi các dịch vụ đã sẵn sàng:
```text
NAME                 IMAGE                     COMMAND                  SERVICE    STATUS
carrental-postgres   postgis/postgis:17-3.5    "docker-entrypoint.s…"   postgres   Up (healthy)
carrental-redis      redis:7-alpine            "docker-entrypoint.s…"   redis      Up (healthy)
carrental-api        be-app                    "dotnet CarRental.Ap…"   app        Up
```

### 4.3. Theo dõi Log ứng dụng

Để theo dõi log thời gian thực của ứng dụng API:

```bash
docker compose logs -f app
```

Xem log của PostgreSQL hoặc Redis:
```bash
docker compose logs -f postgres
docker compose logs -f redis
```

### 4.4. Dừng hệ thống

Dừng các container (dữ liệu database và redis vẫn được lưu trong volumes):
```bash
docker compose down
```

Nếu muốn xóa toàn bộ containers và xóa sạch cả dữ liệu database/redis để chạy lại từ đầu:
```bash
docker compose down -v
```

---

## 5. Tài liệu API & Kiểm thử (Scalar / OpenAPI)

Khi container `carrental-api` đã khởi chạy thành công ở chế độ `Development`, bạn có thể truy cập các đường dẫn sau từ trình duyệt:

| Tài liệu / Công cụ | Đường dẫn (URL) | Mô tả |
| :--- | :--- | :--- |
| **Scalar API Reference** | [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1) | Giao diện tài liệu API hiện đại, trực quan, hỗ trợ gửi request kiểm thử trực tiếp |
| **OpenAPI Schema (JSON)** | [http://localhost:8080/openapi/v1.json](http://localhost:8080/openapi/v1.json) | Chuẩn đặc tả OpenAPI v3 dạng JSON dùng cho Swagger UI, Postman, Insomnia |

### Danh sách các nhóm API Endpoint chính đã triển khai:

- **Pricing (`/api/pricing`)**:
  - `POST /api/pricing/quotes` - Tính báo giá thuê xe dự kiến
  - `GET /api/pricing/rates/{vehicleId}` - Lấy bảng giá theo xe
- **Availability (`/api/availability`)**:
  - `GET /api/availability/{vehicleId}/calendar` - Xem lịch khả dụng của xe
  - `POST /api/availability/check` - Kiểm tra xem xe có trống trong khoảng thời gian chỉ định
  - `POST /api/availability/block` - Chặn lịch xe (để bảo dưỡng hoặc chủ xe giữ lại)
- **Bookings (`/api/bookings`)**:
  - `POST /api/bookings` - Tạo yêu cầu đặt xe mới
  - `GET /api/bookings/me` - Danh sách đơn đặt xe của người thuê hiện tại
  - `GET /api/bookings/owner` - Danh sách đơn đặt xe gửi tới chủ xe
  - `POST /api/bookings/{id}/approve` - Chủ xe phê duyệt đơn đặt
  - `POST /api/bookings/{id}/reject` - Chủ xe từ chối đơn
  - `POST /api/bookings/{id}/cancel` - Người thuê hủy đơn đặt xe
- **Payments (`/api/payments`)**:
  - `POST /api/payments/checkout` - Khởi tạo phiên thanh toán (VNPay, MoMo)
  - `GET /api/payments/deposit-status/{bookingId}` - Kiểm tra trạng thái giữ/hoàn cọc
  - `POST /api/payments/webhook-mock` - Giả lập callback webhook từ cổng thanh toán
- **Handover (`/api/handover`)**:
  - `POST /api/handover/{bookingId}/check-in` - Ghi nhận nhận xe (ODO, xăng, trầy xước)
  - `POST /api/handover/{bookingId}/check-out` - Ghi nhận trả xe và phụ phí phát sinh
  - `POST /api/handover/{bookingId}/confirm` - Khách thuê hoặc chủ xe xác nhận biên bản
- **Reporting (`/api/reporting`)**:
  - `GET /api/reporting/admin/dashboard` - Báo cáo tổng quan dành cho quản trị viên
  - `GET /api/reporting/owner/revenue` - Thống kê doanh thu theo thời gian của chủ xe

---

## 6. Hướng dẫn Phát triển Cục bộ (Local Development)

Nếu bạn muốn debug trực tiếp mã nguồn bằng Visual Studio, Rider hoặc VS Code mà không cần đóng gói container ứng dụng:

### Bước 1: Khởi động Postgres và Redis bằng Docker

Chỉ bật Postgres và Redis trong nền:
```bash
docker compose up -d postgres redis
```

### Bước 2: Kiểm tra kết nối trong `appsettings.json`

File `src/Host/CarRental.Api/appsettings.json` đã cấu hình sẵn kết nối tới máy chủ `localhost`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;"
  }
}
```

### Bước 3: Khởi chạy API qua .NET CLI

Tại thư mục `be`:

```bash
# Khôi phục các gói NuGet
dotnet restore

# Chạy ứng dụng API
dotnet run --project src/Host/CarRental.Api
```

Ứng dụng sẽ tự động chạy các migration và lắng nghe tại cổng `http://localhost:5000` hoặc cổng được cấu hình trong `launchSettings.json`.

### Bước 4: Chạy Unit Tests & Architecture Tests

```bash
dotnet test
```

---

## 7. Cấu trúc Dự án (Project Structure)

```text
be/
├── CarRental.slnx                  # Solution file của dự án (.NET 9)
├── Directory.Build.props           # Cấu hình build chung cho toàn bộ projects
├── Directory.Packages.props        # Quản lý phiên bản NuGet tập trung (CPM)
├── Dockerfile                      # Multi-stage Docker build cho Backend API
├── docker-compose.yml              # File điều phối Docker (App, Postgres, Redis)
├── .env.example                    # Template biến môi trường
├── scripts/
│   └── init-db.sql                 # Script khởi tạo PostGIS extensions & schemas
├── docs/                           # Tài liệu phân tích kiến trúc, domain model & API
├── src/
│   ├── Host/
│   │   └── CarRental.Api/          # Entrypoint của toàn bộ hệ thống API (.NET 9 Minimal APIs)
│   ├── Modules/                    # 13 Modules nghiệp vụ (Mỗi module gồm core & contracts)
│   │   ├── Audit/
│   │   ├── Availability/
│   │   ├── Bookings/
│   │   ├── Fleet/
│   │   ├── Handover/
│   │   ├── Notifications/
│   │   ├── Payments/
│   │   ├── Pricing/
│   │   ├── Reporting/
│   │   ├── Reviews/
│   │   ├── Search/
│   │   ├── Support/
│   │   └── Users/
│   └── Shared/                     # Thư viện dùng chung, BuildingBlocks, Event Bus abstractions
└── tests/
    ├── CarRental.ArchitectureTests # Kiểm tra tính toàn vẹn ranh giới giữa các modules
    ├── CarRental.BuildingBlocks.Tests
    └── CarRental.Modules.Tests
```

---

## 8. Khắc phục Sự cố (Troubleshooting)

### 1. Trùng cổng (Port Conflict)
- **Triệu chứng:** Báo lỗi `port is already allocated` đối với cổng `8080`, `5432`, hoặc `6379`.
- **Xử lý:** Mở file `.env` và điều chỉnh các giá trị `APP_PORT`, `POSTGRES_PORT` hoặc `REDIS_PORT` thành các cổng chưa bị chiếm dụng (ví dụ: `APP_PORT=8081`).

### 2. Database không kết nối được hoặc thiếu Extensions
- **Triệu chứng:** Container `carrental-api` thoát hoặc báo lỗi không tìm thấy extension `postgis` hoặc `uuid-ossp`.
- **Xử lý:** Container PostgreSQL sử dụng image `postgis/postgis:17-3.5` và tự động nạp file `scripts/init-db.sql`. Nếu trước đó bạn đã từng khởi tạo volume Postgres cũ bị lỗi, hãy reset volume bằng lệnh:
  ```bash
  docker compose down -v
  docker compose up --build -d
  ```

### 3. Không vào được giao diện Scalar API Reference
- **Triệu chứng:** Truy cập `http://localhost:8080/scalar/v1` nhận mã lỗi 404.
- **Xử lý:** Đảm bảo biến môi trường `ASPNETCORE_ENVIRONMENT=Development` trong file `.env` (Scalar chỉ được bật trong môi trường Development theo cấu hình `Program.cs`).
