# Nền tảng Cho thuê xe

# Thiết kế Cơ sở Dữ liệu & API

## 1\. Thiết kế Cơ sở dữ liệu (PostgreSQL)

### 1.1. Domain Cốt lõi: Luồng Thuê xe (Rental Flow)

**Schema: `booking` (Quản lý Đặt xe)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **Bookings** *(Agg. Root)* | `Id` | UUID | Khóa chính (Primary Key) |
|  | `CustomerId` | UUID | ID tham chiếu đến `users` (Khách thuê) |
|  | `OwnerId` | UUID | ID tham chiếu đến `users` (Chủ xe) |
|  | `VehicleId` | UUID | ID tham chiếu đến `fleet` (Xe) |
|  | `Status` | Enum | Pending, Confirmed, Paid, InProgress, Completed, Cancelled, Rejected, Expired |
|  | `StartTime` | Timestamptz | Thời gian bắt đầu thuê |
|  | `EndTime` | Timestamptz | Thời gian kết thúc thuê |
|  | `PickupLocation` | Text | Địa điểm giao/nhận xe |
|  | `CreatedAt` | Timestamptz |  |
|  | `UpdatedAt` | Timestamptz |  |
| **BookingLineItems** | `Id` | UUID | Khóa chính |
|  | `BookingId` | UUID | Khóa ngoại trỏ đến `Bookings` |
|  | `Type` | Enum | Loại phí: Rent (Thuê), Deposit (Cọc), CleaningFee, OverKmFee |
|  | `Amount` | Decimal | Số tiền |
| **CancellationPolicySnapshots** | `BookingId` | UUID | Khóa chính / Khóa ngoại trỏ đến `Bookings` |
|  | `PolicyDetails` | JSONB | Lưu bản snapshot chính sách hủy để tránh bị ảnh hưởng nếu Chủ xe đổi giá sau này (BR-08) |
| **BookingStatusHistories** | `Id` | UUID | Khóa chính |
|  | `BookingId` | UUID | Khóa ngoại trỏ đến `Bookings` |
|  | `Status` | Enum | Trạng thái thay đổi thành |
|  | `ChangedAt` | Timestamptz | Thời gian đổi trạng thái |
|  | `Reason` | Text | Lý do thay đổi (nếu có) |

**Schema: `pricing` (Cấu hình Giá & Báo giá)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **PricingPlans** | `Id` | UUID | Khóa chính |
|  | `VehicleId` | UUID | Đánh chỉ mục (Indexed). ID tham chiếu đến `fleet` |
|  | `WeekdayRate` | Decimal | Giá ngày thường |
|  | `WeekendRate` | Decimal | Giá cuối tuần |
|  | `DepositAmount` | Decimal | Số tiền đặt cọc |
|  | `KmLimit` | Int | Giới hạn số Km mỗi ngày |
| **FeeRules** | `Id` | UUID | Khóa chính |
|  | `PricingPlanId` | UUID | Khóa ngoại trỏ đến `PricingPlans` |
|  | `Type` | Enum | Loại phụ phí: OverKm (Quá km), Cleaning (Vệ sinh) |
|  | `Amount` | Decimal | Mức thu phụ phí |
| **PricingPlanVersions** | `Id` | UUID | Khóa chính |
|  | `VehicleId` | UUID | ID tham chiếu đến `fleet` |
|  | `Config` | JSONB | Lưu lịch sử thay đổi giá cho mục đích Audit (NFR-13) |
|  | `CreatedAt` | Timestamptz |  |

**Schema: `availability` (Quản lý Lịch & Tính sẵn sàng)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **VehicleCalendars** | `VehicleId` | UUID | Khóa chính |
|  | `LastSyncedAt` | Timestamptz | Lần cập nhật lịch gần nhất |
| **CalendarEntries** | `Id` | UUID | Khóa chính |
|  | `VehicleId` | UUID | Đánh chỉ mục |
|  | `Type` | Enum | Booked (Đã chốt), Hold (Đang chờ duyệt), OwnerBlocked, Maintenance |
|  | `TimeRange` | tstzrange | **Sử dụng Exclusion constraint của Postgres để ngăn chặn trùng lặp lịch (Double Booking \- BR-05)** |
|  | `ExpiresAt` | Timestamptz | Null nếu là Booked/Blocked. Giới hạn 12h nếu là Hold (BR-07) |

**Schema: `payments` (Thanh toán & Đặt cọc)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **Payments** | `Id` | UUID | Khóa chính |
|  | `BookingId` | UUID | ID tham chiếu đến `booking` |
|  | `Amount` | Decimal | Tổng tiền thanh toán |
|  | `Status` | Enum | Pending, Success, Failed |
|  | `GatewayTransactionId` | Text | Mã giao dịch từ cổng thanh toán |
|  | `Method` | Enum | VNPay, Momo, Transfer |
| **Deposits** | `Id` | UUID | Khóa chính |
|  | `BookingId` | UUID | ID tham chiếu đến `booking` |
|  | `Amount` | Decimal |  |
|  | `Status` | Enum | Held (Đang giữ), Released (Đã hoàn), Deducted (Khấu trừ), Forfeited (Mất cọc) |
| **WebhookInboxes** | `Id` | Text | Khóa chính (Dùng làm Idempotency Key chống trùng lặp Webhook) |
|  | `ProcessedAt` | Timestamptz |  |
|  | `Payload` | JSONB | Dữ liệu gốc từ đối tác thanh toán |

**Schema: `handover` (Bàn giao xe: Giao / Nhận)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **Handovers** | `Id` | UUID | Khóa chính |
|  | `BookingId` | UUID | ID tham chiếu đến `booking` |
|  | `Type` | Enum | Pickup (Nhận xe), Return (Trả xe) |
|  | `ServerReceivedAt` | Timestamptz | Dấu thời gian chuẩn từ máy chủ (BR-13) |
|  | `Status` | Enum | Draft (Bản nháp), Confirmed (Đã xác nhận) |
| **VehicleConditions** | `HandoverId` | UUID | Khóa chính / Khóa ngoại trỏ đến `Handovers` |
|  | `Odo` | Int | Số Km trên đồng hồ |
|  | `FuelLevel` | Int | Mức nhiên liệu (0-100%) |
|  | `DamageNote` | Text | Ghi chú trầy xước/hư hỏng |
| **HandoverPhotos** | `Id` | UUID | Khóa chính |
|  | `HandoverId` | UUID | Khóa ngoại trỏ đến `Handovers` |
|  | `Corner` | Enum | FrontLeft, FrontRight, BackLeft, BackRight, Dash... |
|  | `StorageKey` | Text | Khóa lưu trữ S3/Blob |
|  | `FileHash` | Text | Hash file chống sửa đổi (BR-13) |
|  | `ClientCapturedAt` | Timestamptz | Thời gian chụp tại máy khách (hỗ trợ lưu offline BR-14) |

### 1.2. Domain Danh mục và Người dùng

**Schema: `users` (Người dùng & Định danh)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **Users** | `Id` | UUID | Khóa chính |
|  | `Email` | Text | Duy nhất (Unique) |
|  | `Phone` | Text | Duy nhất (Unique) |
|  | `PasswordHash` | Text | Đã băm (NFR-04) |
|  | `Role` | Enum | Guest, Customer, Owner, Admin |
| **CustomerProfiles** | `UserId` | UUID | Khóa chính / Khóa ngoại trỏ đến `Users` |
|  | `FullName` | Text | Tên khách hàng |
|  | `Address` | Text |  |
| **OwnerProfiles** | `UserId` | UUID | Khóa chính / Khóa ngoại trỏ đến `Users` |
|  | `TaxCode` | Text | Mã số thuế doanh nghiệp/cá nhân |
|  | `ApprovalStatus` | Enum | Pending, Approved, Rejected (Duyệt Chủ xe) |
| **IdentityDocuments** | `Id` | UUID | Khóa chính |
|  | `UserId` | UUID | Khóa ngoại trỏ đến `Users` |
|  | `Type` | Enum | CCCD, GPLX (Bằng lái) |
|  | `StorageKey` | Text | Chỉ truy cập qua Presigned URLs tạm thời (NFR-05) |

**Schema: `fleet` (Danh mục Phương tiện)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **Vehicles** | `Id` | UUID | Khóa chính |
|  | `OwnerId` | UUID | Đánh chỉ mục. ID tham chiếu đến `users` |
|  | `PlateNumber` | Text | Biển số xe (Duy nhất) |
|  | `BrandId` | UUID | ID Hãng xe |
|  | `ModelId` | UUID | ID Dòng xe |
|  | `Transmission` | Enum | Auto (Tự động), Manual (Số sàn) |
|  | `Seats` | Int | Số chỗ ngồi |
|  | `Status` | Enum | Draft, PendingApproval, Published, Hidden |
| **VehicleImages** | `Id` | UUID | Khóa chính |
|  | `VehicleId` | UUID | Khóa ngoại trỏ đến `Vehicles` |
|  | `StorageKey` | Text | Đường dẫn ảnh |
|  | `IsPrimary` | Bool | Ảnh đại diện |
| **VehicleApprovals** | `Id` | UUID | Khóa chính |
|  | `VehicleId` | UUID | Khóa ngoại trỏ đến `Vehicles` |
|  | `AdminId` | UUID | ID tham chiếu đến `users` (Admin duyệt) |
|  | `Decision` | Enum | Approved, Rejected |
|  | `Reason` | Text | Lý do từ chối/hạ xe |
|  | `CreatedAt` | Timestamptz |  |

**Schema: `search` (Đồng bộ Tìm kiếm)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **VehicleSearchDocuments** | `VehicleId` | UUID | Khóa chính (Bảng phi chuẩn hóa để Read thật nhanh \- NFR-01) |
|  | `Location` | Geometry | Kiểu PostGIS để tìm kiếm theo bán kính địa lý |
|  | `Brand` | Text |  |
|  | `Model` | Text |  |
|  | `Seats` | Int |  |
|  | `Transmission` | Text |  |
|  | `PriceFrom` | Decimal | Đọc từ sự kiện Pricing |
|  | `AverageRating` | Float | Đọc từ sự kiện Reviews |

**Schema: `reviews` (Đánh giá & Kiểm duyệt)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **Reviews** | `Id` | UUID | Khóa chính |
|  | `BookingId` | UUID | Duy nhất. ID tham chiếu đến `booking` |
|  | `VehicleId` | UUID | Đánh chỉ mục. ID tham chiếu đến `fleet` |
|  | `CustomerId` | UUID | ID tham chiếu đến `users` |
|  | `Rating` | Int | Điểm từ 1 đến 5 |
|  | `Content` | Text | Nội dung đánh giá |
|  | `Status` | Enum | Published, Hidden |
| **RatingSummaries** | `VehicleId` | UUID | Khóa chính |
|  | `AverageRating` | Float | Điểm trung bình |
|  | `TotalReviews` | Int | Tổng số lượt đánh giá |

### 1.3. Domain Hỗ trợ và Nền tảng (Platform)

**Schema: `support` (Hỗ trợ & Khiếu nại)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **SupportTickets** | `Id` | UUID | Khóa chính |
|  | `BookingId` | UUID | ID tham chiếu đến `booking` |
|  | `ReporterId` | UUID | Người báo cáo (ID tham chiếu đến `users`) |
|  | `Status` | Enum | Open, InProgress, Resolved (Giải quyết tiền cọc) |
| **TicketMessages** | `Id` | UUID | Khóa chính |
|  | `TicketId` | UUID | Khóa ngoại trỏ đến `SupportTickets` |
|  | `SenderId` | UUID | Người gửi tin |
|  | `Content` | Text |  |
|  | `AttachmentKey` | Text | Link ảnh bằng chứng đính kèm |

**Schema: `notifications` (Thông báo)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **Notifications** | `Id` | UUID | Khóa chính |
|  | `UserId` | UUID | ID tham chiếu đến `users` |
|  | `Type` | Enum | Email, SMS, Push |
|  | `Content` | JSONB | Nội dung payload |
|  | `Status` | Enum | Pending, Sent, Failed |

**Schema: `audit` (Vết kiểm toán hệ thống)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **AuditLogEntries** | `Id` | UUID | Khóa chính (Chỉ thêm mới \- Append Only) |
|  | `Timestamp` | Timestamptz | Thời gian thao tác |
|  | `ActorId` | UUID | Người thực hiện (Thường là Admin) |
|  | `IpAddress` | Text | IP mạng |
|  | `Action` | Text | Tên hành động (VD: 'ForceCancelBooking', 'BanUser') |
|  | `EntityName` | Text | Tên bảng bị tác động |
|  | `EntityId` | UUID | ID bản ghi |
|  | `BeforeState` | JSONB | Trạng thái trước khi sửa |
|  | `AfterState` | JSONB | Trạng thái sau khi sửa |

**Schema: `reporting` (Báo cáo Thống kê)**

| Tên Bảng | Tên Cột | Kiểu Dữ liệu | Ràng buộc / Ghi chú |
| :---- | :---- | :---- | :---- |
| **DailyBookingStats** | `Date` | Date | Khóa chính |
|  | `TotalBookings` | Int | Tổng số chuyến |
|  | `TotalRevenue` | Decimal | Tổng doanh thu |
|  | `CancellationCount` | Int | Số lượng hủy |

---

## 2\. Thiết kế API (RESTful)

### 2.1 Guest / Tìm kiếm (G01, G02, G03, G04)

* `GET /api/search/vehicles` \- Tìm kiếm và lọc xe (Hỗ trợ Phân trang, Bộ lọc PostGIS vị trí địa lý, Thông số, Mức giá).  
* `GET /api/fleet/vehicles/{id}` \- Xem chi tiết xe (Hình ảnh, thông số, thông tin chủ xe).  
* `GET /api/reviews/vehicles/{id}` \- Xem các bài đánh giá và điểm trung bình của xe.  
* `POST /api/users/auth/register` \- Đăng ký tài khoản mới.  
* `POST /api/users/auth/login` \- Đăng nhập và nhận chuỗi JWT token.

### 2.2 Các thao tác của Khách thuê (C01 \- C10)

**Hồ sơ & Định danh**

* `PUT /api/users/me/profile` \- Cập nhật thông tin cá nhân.  
* `POST /api/users/me/identity-docs` \- Tải lên CCCD/GPLX (Trả về Presigned URL để upload file trực tiếp lên S3/Blob).

**Luồng Đặt xe**

* `GET /api/availability/vehicles/{id}/check` \- Kiểm tra xe trống (Tham số: `startTime`, `endTime`).  
* `POST /api/pricing/quotes` \- Lấy báo giá chi tiết (Body: `vehicleId`, `startTime`, `endTime`). Server tự tính giá cuối cùng.  
* `POST /api/bookings` \- Khởi tạo yêu cầu đặt xe (Khóa lịch xe trạng thái *Hold* trong tối đa 12h). Trạng thái đơn: `Pending`.  
* `GET /api/bookings/me` \- Xem lịch sử thuê xe của khách hàng.  
* `PUT /api/bookings/{id}/cancel` \- Hủy đặt xe (Tự động tính phí phạt, giải phóng lịch cho xe).

**Thanh toán**

* `POST /api/payments/checkout` \- Khởi tạo thanh toán/đặt cọc cho đơn hàng đã được Chủ xe xác nhận.  
* `POST /api/payments/webhook` \- (Dành cho Hệ thống) Webhook nhận phản hồi từ cổng thanh toán (VNPay, Momo).

**Giao nhận (Handover) & Đánh giá**

* `POST /api/handovers` \- Bắt đầu quy trình Giao xe (Check-in) hoặc Nhận xe (Check-out).  
* `POST /api/handovers/{id}/photos` \- Tải lên ảnh 4 góc xe, đồng hồ Odo, mức xăng.  
* `PUT /api/handovers/{id}/confirm` \- Cả hai bên xác nhận bàn giao. Dữ liệu trở thành bất biến.  
* `POST /api/reviews` \- Gửi đánh giá (Chỉ thực hiện được khi đơn hàng đã ở trạng thái `Completed`).

**Hỗ trợ**

* `POST /api/support/tickets` \- Gửi khiếu nại hoặc báo cáo tranh chấp cọc/hư hỏng.

### 2.3 Các thao tác của Chủ xe (O01 \- O08)

**Quản lý Xe (Fleet)**

* `POST /api/users/me/owner-onboarding` \- Đăng ký trở thành Chủ xe (Cung cấp mã số thuế, giấy tờ kinh doanh).  
* `POST /api/fleet/vehicles` \- Thêm xe mới (Trạng thái ban đầu: `PendingApproval`).  
* `PUT /api/fleet/vehicles/{id}` \- Cập nhật thông tin xe.  
* `PUT /api/pricing/vehicles/{id}` \- Cấu hình bảng giá (Giá cuối tuần, phụ phí vượt km, phí vệ sinh).

**Tính sẵn sàng & Xử lý Yêu cầu Đặt xe**

* `POST /api/availability/vehicles/{id}/blocks` \- Khóa lịch xe thủ công (khi đem xe đi bảo dưỡng, hoặc việc cá nhân).  
* `GET /api/bookings/owner` \- Xem danh sách các đơn đặt xe đang tới.  
* `PUT /api/bookings/{id}/approve` \- Chấp thuận đơn (Đổi trạng thái lịch từ *Hold* sang *Booked*).  
* `PUT /api/bookings/{id}/reject` \- Từ chối yêu cầu đặt xe.

### 2.4 Các thao tác của Quản trị viên \- Admin (A01 \- A06)

*Các Endpoint của Admin sẽ được định tuyến vào các module tương ứng đang quản lý resource đó.*

* **Users:** `PUT /api/users/{id}/ban` \- Khóa (Ban) tài khoản người dùng vi phạm.  
* **Fleet:** `PUT /api/fleet/vehicles/{id}/approve` \- Phê duyệt đăng xe hoặc gỡ xe (Unpublish) kém chất lượng.  
* **Bookings:** `PUT /api/bookings/{id}/admin-cancel` \- Hủy ngang đơn hàng (can thiệp thủ công).  
* **Reviews:** `PUT /api/reviews/{id}/moderate` \- Ẩn/Xóa bình luận thô tục, vi phạm.  
* **Support:** `PUT /api/support/tickets/{id}/resolve` \- Phán quyết cuối cùng về tỷ lệ chia tiền cọc khi có tranh chấp.  
* **Reporting:** `GET /api/reporting/dashboard` \- Lấy thống kê doanh thu, lượng người dùng, đơn hủy.

---

## 3\. Các Mô hình Giao tiếp Liên Module Chính (Communication Patterns)

1. **Tạo đơn Đặt xe (Giao tiếp Đồng bộ):**  
   * Client gọi `POST /api/bookings`.  
   * Module `Booking` gọi module `Pricing` (Interface Đồng bộ) để chốt bản báo giá snapshot không đổi.  
   * Module `Booking` gọi module `Availability` (Interface Đồng bộ) để áp dụng khóa chống trùng lặp `tstzrange` trong cơ sở dữ liệu Postgres.  
2. **Thanh toán Thành công (Xử lý Đồng bộ kết hợp Outbox cho Notification & Reporting):**  
   * Khách thanh toán hoặc Webhook gọi vào module `Payment` -> Đổi trạng thái `Payment` thành Success, giữ tiền cọc `Deposit`.  
   * Module `Payment` gọi trực tiếp `IBookingApi.MarkPaidAsync` (Giao tiếp Đồng bộ) -> Đổi trạng thái Booking sang `Paid` tức thì.  
   * Đẩy sự kiện `PaymentSucceededIntegrationEvent` vào Outbox -> Module `Notification` gửi Email/SMS và Module `Reporting` cộng dồn doanh thu hàng ngày.  
3. **Đồng bộ dữ liệu Tìm kiếm (Giao tiếp Bất đồng bộ / Outbox Pattern):**  
   * Khi module `Fleet` duyệt xe \-\> Đẩy sự kiện `VehiclePublishedIntegrationEvent`.  
   * Khi cấu hình giá trong `Pricing` thay đổi \-\> Đẩy sự kiện `PricingUpdatedIntegrationEvent`.  
   * Module `Search` sẽ hứng (consume) các sự kiện này để cập nhật lại dữ liệu vào bảng phi chuẩn hóa `VehicleSearchDocuments` chạy trên PostGIS để đảm bảo tốc độ tra cứu \< 2s (NFR-01).  
* 