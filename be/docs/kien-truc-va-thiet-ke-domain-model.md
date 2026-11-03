# Giải Thích Các Quyết Định Kiến Trúc & Thiết Kế Domain Model (DDD)

> **Tài liệu tham khảo kỹ thuật nội bộ**  
> **Dự án:** Nền tảng Cho thuê xe Tự lái & Có tài xế (Vehicle Rental Platform)  
> **Áp dụng cho:** Backend .NET 9, PostgreSQL 17, Entity Framework Core, Domain-Driven Design (DDD)

---

## Mục lục
1. [Tại sao áp dụng Rich Domain Model (Mô hình Thực thể Giàu nghiệp vụ)?](#1-tại-sao-áp-dụng-rich-domain-model)
2. [Khóa định danh kiểu mạnh (Strongly-Typed IDs: `BookingId`, `VehicleId`) vs. `Guid` thông thường](#2-khoá-định-danh-kiểu-mạnh-strongly-typed-ids-vs-guid-thông-thường)
3. [Aggregate Root vs. Child Entity (Thực thể gốc và Thực thể con)](#3-aggregate-root-vs-child-entity)
4. [Kiểm soát đồng thời lạc quan (Optimistic Concurrency Control & PostgreSQL `xmin`)](#4-kiểm-soát-đồng-thời-lạc-quan-occ--postgresql-xmin)
5. [Cơ chế ánh xạ Entity Framework Core (Value Converters & Owned Types)](#5-cơ-chế-ánh-xạ-entity-framework-core)
6. [Sự cô lập thuần túy của Domain Layer (Pure Domain Isolation)](#6-sự-cô-lập-thuần-túy-của-domain-layer)

---

## 1. Tại sao áp dụng Rich Domain Model?

### 1.1. Vấn đề của mô hình truyền thống (Anemic Domain Model)
Trong cách lập trình C# / .NET truyền thống với EF Core, các lập trình viên thường viết Entity như những lớp "chứa dữ liệu thuần túy" (DTO):

```csharp
// ❌ ANEMIC DOMAIN MODEL (Mô hình nghèo nàn - Bị cấm trong hệ thống)
public class Booking
{
    public Guid Id { get; set; }
    public BookingStatus Status { get; set; }
    public DateTimeOffset? PaymentDeadline { get; set; }
    public decimal TotalAmount { get; set; }
}
```

Hậu quả của cách làm này:
- **Tình trạng phân mảnh nghiệp vụ (Logic Leakage):** Mọi logic kiểm tra điều kiện (ví dụ: "chỉ khi đang ở trạng thái `Pending` thì chủ xe mới được duyệt") bị phân tán rải rác ở hàng chục file Controller, Service, hoặc Endpoint khác nhau.
- **Dễ vi phạm tính toàn vẹn (Broken Invariants):** Bất kỳ đoạn mã nào ở tầng ngoài cũng có thể gọi `booking.Status = BookingStatus.Confirmed` mà quên kiểm tra xem khách đã thanh toán chưa, hoặc quên cập nhật lịch sử trạng thái (`BookingStatusHistory`).
- **Khó bảo trì và kiểm thử:** Viết Unit Test cho nghiệp vụ đòi hỏi phải dựng toàn bộ DbContext và Service giả lập khổng lồ thay vì chỉ test logic thuần túy trên Entity.

### 1.2. Giải pháp: Rich Domain Model (Mô hình Giàu nghiệp vụ)
Trong hệ thống của chúng ta, **tất cả setter đều là `private set`**. Dữ liệu chỉ có thể thay đổi thông qua các phương thức nghiệp vụ rõ ràng:

```csharp
//  RICH DOMAIN MODEL (Đóng gói nghiệp vụ chặt chẽ)
public class Booking : AggregateRoot<BookingId>
{
    public BookingStatus Status { get; private set; }
    public DateTimeOffset? PaymentDeadline { get; private set; }
    
    // Constructor private cho EF Core Materialization
    private Booking() { }

    // Phương thức Factory khởi tạo với đầy đủ guard clause
    public static Result<Booking> Request(...) { ... }

    // Phương thức thể hiện hành vi nghiệp vụ rõ ràng
    public Result Approve(Guid ownerId, DateTimeOffset now)
    {
        if (ownerId != OwnerId)
            return Result.Failure(new Error("booking.unauthorized", "Chỉ chủ xe mới có quyền duyệt đơn này."));

        if (Status != BookingStatus.Pending)
            return Result.Failure(new Error("booking.invalid_state", "Chỉ đơn ở trạng thái Pending mới được duyệt."));

        Status = BookingStatus.Confirmed;
        PaymentDeadline = now.AddHours(2);
        UpdatedAt = now;

        _statusHistory.Add(new BookingStatusHistory(Guid.CreateVersion7(), Id.Value, BookingStatus.Confirmed, now, "Approved by owner"));
        return Result.Success();
    }
}
```

**Lợi ích đạt được:**
1. **Bảo vệ bất biến (Invariants):** Không một ai ở tầng ngoài có thể đưa Booking vào trạng thái không hợp lệ.
2. **Khả năng tự ghi nhận lịch sử:** Mỗi khi chuyển trạng thái, chính Entity tự ghi thêm dòng `BookingStatusHistory` và cập nhật `UpdatedAt`.
3. **Unit Test siêu nhanh:** Không cần mở database hay mock framework phức tạp; chỉ cần gọi `booking.Approve(...)` và assert kết quả trả về trong vài micro-giây.

---

## 2. Khóa định danh kiểu mạnh (Strongly-Typed IDs) vs. `Guid` thông thường

### 2.1. Vấn nạn "Primitive Obsession" (Ám ảnh kiểu nguyên thủy)
Giả sử ta viết một phương thức phân quyền nhận các tham số `Guid` thông thường:

```csharp
public async Task<bool> VerifyAccessAsync(Guid customerId, Guid ownerId, Guid vehicleId, Guid bookingId);
```

Khi lập trình viên gọi phương thức này:
```csharp
//  Lỗi nhầm vị trí tham số nhưng Compiler hoàn toàn IM LẶNG!
await VerifyAccessAsync(ownerId, customerId, bookingId, vehicleId);
```
Trình biên dịch C# vẫn xem các tham số là `Guid` hợp lệ. Lỗi logic này sẽ lọt ra môi trường chạy (Runtime) và dẫn đến những sự cố rò rỉ bảo mật hoặc sai lệch dữ liệu cực kỳ khó gỡ lỗi (debug).

### 2.2. Giải pháp: Zero-Cost `readonly record struct`
Để giải quyết triệt để, mỗi Aggregate Root sở hữu một kiểu ID chuyên biệt:

```csharp
public readonly record struct BookingId(Guid Value)
{
    public static BookingId New() => new(Guid.CreateVersion7());
    public static BookingId From(Guid value) => new(value);
    public static implicit operator Guid(BookingId id) => id.Value;
    public static implicit operator BookingId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
```

Bây giờ chữ ký phương thức trở thành:
```csharp
public async Task<bool> VerifyAccessAsync(Guid customerId, Guid ownerId, VehicleId vehicleId, BookingId bookingId);
```
Nếu bạn vô tình truyền `BookingId` vào vị trí của `VehicleId`, **trình biên dịch C# sẽ báo lỗi đỏ ngay lập tức (Compile-time Safety)**.

**Tại sao dùng `readonly record struct`?**
- `struct`: Không cấp phát trên heap, không gây gánh nặng cho Garbage Collector (zero heap allocation overhead).
- `readonly`: Bất biến tuyệt đối.
- `implicit operator`: Cho phép gán tự do qua lại với `Guid` thông thường mà không cần ép kiểu thủ công rườm rà.

### 2.3. Tại sao một số thực thể lại dùng `Entity<Guid>` thông thường?
- **Aggregate Roots (`BookingId`, `VehicleId`, `PaymentId`,...):** Là đầu mối giao tiếp chính giữa các API, URL query, và integration events, nên **bắt buộc** dùng Strongly-typed ID để ngăn ngừa nhầm lẫn tham số.
- **Child Entities (`BookingLineItem`, `HandoverPhoto`, `TicketMessage`):** Chỉ tồn tại nội bộ bên trong Aggregate Root của nó, không bao giờ được truy vấn độc lập từ tầng ngoài. Việc tạo thêm `BookingLineItemId` hay `HandoverPhotoId` sẽ chỉ sinh thêm boilerplate không cần thiết mà không mang lại giá trị an toàn thực tế. Vì vậy, các thực thể con kế thừa `Entity<Guid>` là lựa chọn tối ưu và thực dụng.

---

## 3. Aggregate Root vs. Child Entity

### 3.1. Khái niệm cốt lõi
- **Aggregate (Cụm thực thể):** Một nhóm các thực thể và giá trị có mối liên hệ mật thiết, được quản lý như một khối thống nhất trong một giao dịch cơ sở dữ liệu.
- **Aggregate Root (Thực thể gốc):** Cửa ngõ duy nhất để tương tác với cả cụm. Không có thành phần bên ngoài nào được phép truy cập hoặc chỉnh sửa trực tiếp thực thể con mà không thông qua thực thể gốc.

### 3.2. So sánh trách nhiệm trong kiến trúc

| Tiêu chí | Aggregate Root (`AggregateRoot<TId>`) | Child Entity (`Entity<TId>`) |
|---|---|---|
| **Ví dụ trong dự án** | `Booking`, `Vehicle`, `Payment`, `Deposit`, `Handover`, `SupportTicket` | `BookingLineItem`, `BookingStatusHistory`, `HandoverPhoto`, `VehicleCondition`, `TicketMessage` |
| **Vòng đời (Lifecycle)** | Độc lập; có thể được tìm kiếm trực tiếp qua API (ví dụ: `GET /api/bookings/{id}`). | Phụ thuộc hoàn toàn vào Root; nếu Root bị xóa, con bị xóa theo (`Cascade Delete`). |
| **Repository riêng?** | Có (`IBookingRepository`, `IVehicleRepository`). | **Không bao giờ có**; mọi thao tác đọc/ghi phải đi qua Aggregate Root. |
| **Domain Events** | Có quyền kích hoạt sự kiện nghiệp vụ (`Raise(IDomainEvent)`). | Không có quyền kích hoạt sự kiện độc lập. |
| **Optimistic Concurrency** | Sở hữu token phiên bản (`Version` / `xmin`). | Không có token phiên bản riêng; dựa vào Root. |

---

## 4. Kiểm soát đồng thời lạc quan (OCC & PostgreSQL `xmin`)

### 4.1. Vấn đề "Cập nhật bị mất" (Lost Update)
Xét trường hợp 2 tiến trình đồng thời xảy ra tại cùng 1 giây:
1. **Khách hàng** bấm nút "Hủy đơn" (`Cancel`).
2. **Hệ thống chạy ngầm** (Background Job) quét thấy đơn quá hạn thanh toán và gọi `Expire()`.

Nếu không có cơ chế khóa:
- Tiến trình A đọc đơn (trạng thái `Pending`).
- Tiến trình B đọc đơn (trạng thái `Pending`).
- Tiến trình A ghi đè: `UPDATE ... SET status = 'Cancelled'`.
- Tiến trình B ghi đè: `UPDATE ... SET status = 'Expired'`.
-> Hành động hủy của khách hàng bị xóa sạch không dấu vết (Lost Update).

### 4.2. Khóa lạc quan (Optimistic Concurrency) với `xmin` của PostgreSQL
Thay vì dùng khóa bi quan (`SELECT FOR UPDATE` gây treo bảng và giảm throughput), hệ thống dùng **Khóa lạc quan**:

1. Trong PostgreSQL, mỗi hàng (row) đều tự động có một cột hệ thống ẩn tên là **`xmin`**. `xmin` là mã định danh giao dịch (Transaction ID) đã tạo hoặc cập nhật hàng đó lần cuối.
2. Khi Entity Framework Core cấu hình:
   ```csharp
   builder.Property(b => b.Version)
       .IsRowVersion()
       .HasColumnName("xmin");
   ```
3. Mỗi câu lệnh `UPDATE` do EF Core sinh ra sẽ có dạng:
   ```sql
   UPDATE booking.bookings
   SET status = 'Cancelled', updated_at = '...'
   WHERE id = @id AND xmin = @originalVersion; -- Kiểm tra phiên bản gốc lúc đọc
   ```
4. Nếu một tiến trình khác đã cập nhật trước đó, `xmin` trong DB đã đổi sang số mới -> Mệnh đề `WHERE` trả về 0 hàng bị ảnh hưởng (`RowsAffected = 0`).
5. EF Core lập tức ném ra ngoại lệ `DbUpdateConcurrencyException`, hủy giao dịch và ngăn chặn hoàn toàn việc ghi đè dữ liệu sai lệch.

### 4.3. Tại sao chỉ có Aggregate Root mới chứa `Version`?
Vì Aggregate Root là **Ranh giới nhất quán (Consistency Boundary)**. Mọi thao tác sửa đổi con (ví dụ: thêm ảnh chụp xe, thêm phụ phí quá km) đều cập nhật `UpdatedAt` của Aggregate Root. Khi Aggregate Root được lưu, `xmin` của bảng chính tự động thay đổi, từ đó bảo vệ toàn bộ cụm thực thể trong một lần kiểm tra duy nhất.

---

## 5. Cơ chế ánh xạ Entity Framework Core

### 5.1. Chuyển đổi kiểu (Value Converters)
Để PostgreSQL hiểu được `BookingId`, `PaymentId`... EF Core sử dụng phương thức `HasConversion`:

```csharp
builder.Property(b => b.Id)
    .HasConversion(
        id => id.Value,              // C# -> Database (ghi UUID vào cột id)
        val => new BookingId(val)     // Database -> C# (đọc UUID bọc thành BookingId)
    )
    .HasColumnName("id");
```

### 5.2. Đối tượng giá trị (Value Objects qua Owned Types)
Các khái niệm như `Money`, `DateTimeRange`, `GeoPoint` không có danh tính độc lập mà cấu thành từ nhiều thuộc tính. EF Core ánh xạ chúng bằng `OwnsOne`:

```csharp
builder.OwnsOne(l => l.Amount, nav =>
{
    nav.Property(m => m.Amount)
        .IsRequired()
        .HasColumnName("amount");          // Cột lưu số tiền (long / bigint)
    nav.Property(m => m.Currency)
        .IsRequired()
        .HasMaxLength(8)
        .HasColumnName("currency");        // Cột lưu đơn vị tiền tệ (ví dụ 'VND')
});
```

Điều này giúp mã nguồn C# làm việc với các đối tượng giàu tính toán (`money.Add(...)`, `money.Percent(...)`) trong khi Database lưu trữ các cột đơn giản và tối ưu hóa index.

---

## 6. Sự cô lập thuần túy của Domain Layer

Một nguyên tắc kiến trúc tối thượng được bảo vệ bằng kiểm thử tự động trong `CarRental.ArchitectureTests`:

> **Các class trong thư mục `Entities/` TUYỆT ĐỐI KHÔNG ĐƯỢC tham chiếu đến `Microsoft.EntityFrameworkCore` hay `Microsoft.AspNetCore`.**

- Domain Entity phải là C# thuần khiết (POCO - Plain Old CLR Objects).
- Mọi chú thích (Annotation) như `[Table]`, `[Key]`, `[Column]` bị cấm hoàn toàn.
- Tất cả cấu hình bảng, quan hệ Foreign Key, Index, và JSONB Type đều được tách riêng sang thư mục `Data/Configurations/` thông qua `IEntityTypeConfiguration<T>`.

Nhờ sự phân tách này, logic nghiệp vụ của hệ thống hoàn toàn độc lập với cơ sở dữ liệu, có thể chạy thử nghiệm ở bất kỳ đâu và không bị ảnh hưởng khi nâng cấp thư viện ORM hay framework web.
