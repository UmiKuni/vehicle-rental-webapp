# Module Payments — Hướng dẫn tích hợp cho Backend & Frontend (Mock vs. Real)

Tài liệu này hướng dẫn team (đặc biệt là **team FE**) cách sử dụng module **Payments** ở cả hai chế độ **Mock** (local dev/test) và **Real** (sandbox/production với VNPay, PayPal, Stripe, Bank Transfer / VietQR).

Tham khảo thêm:
- Endpoints: [`PaymentEndpoints.cs`](./CarRental.Modules.Payments/Endpoints/PaymentEndpoints.cs)
- Service xử lý chính: [`PaymentsApiService.cs`](./CarRental.Modules.Payments/Services/PaymentsApiService.cs)
- DTOs & Enums: [`PaymentDtos.cs`](./CarRental.Modules.Payments.Contracts/Dtos/PaymentDtos.cs), [`PaymentCheckoutDto.cs`](./CarRental.Modules.Payments.Contracts/Dtos/PaymentCheckoutDto.cs), [`PaymentMethod.cs`](./CarRental.Modules.Payments.Contracts/Enums/PaymentMethod.cs), [`PaymentStatus.cs`](./CarRental.Modules.Payments.Contracts/Enums/PaymentStatus.cs), [`DepositStatus.cs`](./CarRental.Modules.Payments.Contracts/Enums/DepositStatus.cs)
- File test HTTP mẫu: [`payments.http`](./payments.http)

---

## 1. Tổng quan & Cấu hình (Mock vs. Real Mode)

Backend cho phép bật/tắt giữa cổng giả lập và cổng thanh toán thật thông qua cấu hình DI trong [`PaymentsModule.cs`](./CarRental.Modules.Payments/PaymentsModule.cs):

- **File biến môi trường (`be/.env`, xem mẫu tại [`be/.env.example`](../../../.env.example)) hoặc [`docker-compose.yml`](../../../docker-compose.yml):**
  ```env
  PAYMENTS_USE_MOCK_GATEWAY=true   # hoặc false để dùng cổng thật
  ```
- **Hoặc trong [`appsettings.json`](../../Host/CarRental.Api/appsettings.json) / [`appsettings.Development.json`](../../Host/CarRental.Api/appsettings.Development.json):**
  ```json
  "Payments": {
    "UseMockGateway": false
  }
  ```
  - Mặc định `appsettings.Development.json` bật `"UseMockGateway": true` để chạy local (`dotnet run`) không bị lỗi thiếu key cổng thanh toán; `appsettings.json` và `docker-compose.yml` giữ mặc định `false` (có thể override cả hai chiều bằng biến môi trường `PAYMENTS_USE_MOCK_GATEWAY=true|false`).

Cơ chế chuyển đổi ở Backend:
- Khi `UseMockGateway = true`: DI inject [`MockPaymentGateway`](./CarRental.Modules.Payments/Services/MockPaymentGateway.cs) — không gọi ra ngoài internet, không cần API keys của VNPay/PayPal/Stripe/VietQR.
- Khi `UseMockGateway = false`: DI inject [`RoutingPaymentGateway`](./CarRental.Modules.Payments/Services/Gateways/RoutingPaymentGateway.cs), tự động điều hướng theo `method` sang gateway tương ứng:
  - [`VnPayPaymentGateway.cs`](./CarRental.Modules.Payments/Services/Gateways/VnPayPaymentGateway.cs) (`VNPay`)
  - [`PayPalPaymentGateway.cs`](./CarRental.Modules.Payments/Services/Gateways/PayPalPaymentGateway.cs) (`Paypal`)
  - [`StripePaymentGateway.cs`](./CarRental.Modules.Payments/Services/Gateways/StripePaymentGateway.cs) (`Stripe`)
  - [`BankTransferPaymentGateway.cs`](./CarRental.Modules.Payments/Services/Gateways/BankTransferPaymentGateway.cs) (`Transfer`)

---

## 2. Dùng Mock hay Real có làm thay đổi code FE không?

**Không.** Toàn bộ API endpoint, cấu trúc JSON Request/Response DTO và luồng polling trạng thái đơn hàng/thanh toán là **giống hệt nhau 100%** ở cả hai chế độ.

Tuy nhiên, có **1 khác biệt thực tế về UX khi test local ở chế độ Mock** mà team FE cần nắm rõ:
- **Ở chế độ Real (`UseMockGateway = false`):** `paymentUrl` là URL phiên thanh toán thật được ký/tạo từ server của VNPay, Stripe, PayPal hoặc ảnh VietQR thật. Khi FE redirect trình duyệt sang `paymentUrl`, người dùng sẽ vào trang thanh toán sandbox/production hợp lệ.
- **Ở chế độ Mock (`UseMockGateway = true`):** [`MockPaymentGateway`](./CarRental.Modules.Payments/Services/MockPaymentGateway.cs) **không gọi API của cổng thanh toán bên ngoài**, nên:
  - `paymentUrl` chỉ là URL minh họa (ví dụ `https://checkout.stripe.com/c/pay/<paymentId>...`). Nếu FE thực hiện `window.location.href = paymentUrl` cho `VNPay`, `Stripe` hoặc `Paypal` ở chế độ Mock, trang đích bên ngoài sẽ báo lỗi/không hợp lệ vì phiên thanh toán chưa từng được tạo trên server của họ.
  - `qrCodeUrl` luôn được tạo dưới dạng URL ảnh QR hợp lệ (`https://api.qrserver.com/v1/create-qr-code/...`) cho **cả 4 phương thức** (trong khi ở chế độ Real, chỉ `Transfer` trả về `qrCodeUrl`, còn `VNPay` / `Stripe` / `Paypal` trả `qrCodeUrl = ""`).

---

## 3. Luồng tích hợp chi tiết cho FE (Step-by-Step)

### Bước 1: Khởi tạo phiên thanh toán (`POST /api/payments/checkout`)

- **Yêu cầu quyền:** `AuthenticatedUser` (cần Bearer JWT của customer sở hữu booking).
- **Điều kiện tiên quyết:** Booking phải đang ở trạng thái `"Confirmed"`. Tổng tiền thanh toán backend tự tính bằng `TotalRentAmount + SecurityDepositAmount`.
- **Request (`InitiatePaymentRequest`):**
  ```json
  {
    "bookingId": "00000000-0000-0000-0000-000000000301",
    "method": "Transfer",
    "returnUrl": "http://localhost:5173/payment/result"
  }
  ```
  - `method`: nhận một trong 4 giá trị string của [`PaymentMethod`](./CarRental.Modules.Payments.Contracts/Enums/PaymentMethod.cs): `"VNPay"`, `"Paypal"`, `"Stripe"`, `"Transfer"` (không phân biệt hoa/thường, không truyền số).
  - `returnUrl`: URL tuyệt đối `http://` hoặc `https://` để cổng thanh toán redirect về sau khi khách xử lý xong.
- **Response `200 OK` (`PaymentCheckoutDto`):**
  ```json
  {
    "paymentId": "01934a1b-...",
    "bookingId": "00000000-0000-0000-0000-000000000301",
    "amount": 3950000,
    "method": "Transfer",
    "paymentUrl": "https://...",
    "qrCodeUrl": "https://...",
    "expiresAt": "2026-11-19T15:00:00+00:00"
  }
  ```

---

### Bước 2: Hiển thị / Điều hướng trên FE theo từng phương thức

| Phương thức (`method`) | Chế độ **Real** (`UseMockGateway=false`) | Chế độ **Mock** (`UseMockGateway=true`) | Cách FE xử lý |
| :--- | :--- | :--- | :--- |
| **`Transfer`** (VietQR) | `qrCodeUrl` = ảnh VietQR thật (`https://img.vietqr.io/image/...` kèm nội dung `CARRENTAL <paymentId>`) | `qrCodeUrl` = ảnh QR giả lập (`https://api.qrserver.com/...`) | **Giống nhau 100%:** FE render `<img src={qrCodeUrl} />` và polling trạng thái thanh toán. Ở Mock mode, dùng thêm nút/request giả lập webhook ở Bước 3 để đổi trạng thái sang `Success`. |
| **`VNPay`** | `paymentUrl` = URL VNPay sandbox có chữ ký HMAC-SHA512 (`qrCodeUrl = ""`) | `paymentUrl` = URL minh họa; có sẵn `qrCodeUrl` giả lập | **Real:** Redirect sang `paymentUrl` → VNPay chuyển về `returnUrl?vnp_ResponseCode=00&vnp_TxnRef=...`<br>**Mock:** Không redirect sang `paymentUrl` giả; hiển thị UI test/QR và gọi `POST /api/payments/webhook` để giả lập kết quả. |
| **`Stripe`** | `paymentUrl` = Stripe Checkout Session URL thật (`qrCodeUrl = ""`) | `paymentUrl` = URL minh họa; có sẵn `qrCodeUrl` giả lập | **Real:** Redirect sang `paymentUrl` → Stripe chuyển về `returnUrl?session_id={CHECKOUT_SESSION_ID}`.<br>**Mock:** Không redirect sang `paymentUrl` giả; gọi `POST /api/payments/webhook` để giả lập kết quả. |
| **`Paypal`** | `paymentUrl` = PayPal approval URL thật (`qrCodeUrl = ""`) | `paymentUrl` = URL minh họa; có sẵn `qrCodeUrl` giả lập | **Real:** Redirect sang `paymentUrl` → PayPal chuyển về `returnUrl?token=<PAYPAL_ORDER_ID>&PayerID=...` → **FE bắt buộc gọi `POST /api/payments/paypal/capture` với `{ "orderId": token }`** để chốt tiền.<br>**Mock:** Không redirect sang `paymentUrl` giả; có thể gọi `POST /api/payments/paypal/capture` với `{ "orderId": "<paymentId>" }` (hoặc `bookingId` có payment `Pending`), hoặc gọi `POST /api/payments/webhook` giống `VNPay` / `Stripe`. |

> **Lưu ý quan trọng với PayPal:**
> - Ở **Real mode**, khi PayPal redirect user về `returnUrl?token=<PAYPAL_ORDER_ID>`, giao dịch mới ở trạng thái *Approved* (chưa trừ tiền). FE cần đọc query param `token` và gọi `POST /api/payments/paypal/capture` (body `{ "orderId": token }`, cần JWT) để backend capture đơn PayPal và cập nhật booking sang `Paid`.
> - Ở **Mock mode**, `POST /api/payments/paypal/capture` với `{ "orderId": "<paymentId>" }` (hoặc `bookingId` có payment `Pending`) tự tra cứu số tiền từ `IPaymentsRepository` nên đi qua bước kiểm tra `IsWebhookAmountValid` và hoàn tất giao dịch bình thường, bên cạnh cách giả lập trực tiếp qua `POST /api/payments/webhook` ở Bước 3.

---

### Bước 3: Hoàn tất giao dịch (Real Webhooks vs. Mock Webhook)

#### Trường hợp A — Chế độ Real (`UseMockGateway = false`)
Hệ thống bên ngoài (cổng thanh toán / dịch vụ ngân hàng) sẽ tự gọi callback/IPN về backend (FE không cần tự gọi các endpoint webhook này):
- `GET /api/payments/webhooks/vnpay` (VNPay IPN, xác thực HMAC-SHA512)
- `POST /api/payments/webhooks/stripe` (Stripe Webhook, xác thực header `Stripe-Signature`)
- `POST /api/payments/webhooks/paypal` (PayPal Webhook, xác thực qua PayPal Verify Webhook Signature API)
- `POST /api/payments/webhooks/transfer` (SePay / Casso / VietQR webhook, xác thực `Authorization: Apikey ...` / `X-Api-Key` / `X-Signature`)
- Riêng PayPal sau redirect: FE gọi `POST /api/payments/paypal/capture` với `{ "orderId": "<token>" }`.

#### Trường hợp B — Chế độ Mock (`UseMockGateway = true`)
Vì không có cổng thanh toán thật gọi IPN về localhost, dev/FE giả lập kết quả thanh toán bằng cách gọi endpoint unified webhook (`AllowAnonymous`):

```http
POST /api/payments/webhook
Content-Type: application/json

{
  "transactionId": "MOCK-TXN-001",
  "orderId": "<paymentId hoặc bookingId>",
  "amount": 3950000,
  "responseCode": "00",
  "signature": "mock-secret"
}
```

**Quy tắc validate của `MockPaymentGateway` cho endpoint `/api/payments/webhook`:**
- `transactionId`: Bắt buộc khác rỗng và **phải duy nhất cho mỗi lần test mới**. Nếu gửi trùng `transactionId` đã xử lý trước đó, backend trả `200 OK` với `"Webhook already processed."` (idempotent) và không chạy lại logic cộng tiền.
- `orderId`: `Guid` của `paymentId` (khuyên dùng, lấy từ response của `POST /api/payments/checkout`) hoặc `bookingId` đang có payment `Pending`.
- `amount`: **Phải khớp chính xác** với `amount` trả về từ bước checkout (`TotalRentAmount + SecurityDepositAmount`), nếu lệch sẽ trả lỗi `payment.amount_mismatch`.
- `responseCode`:
  - `"00"` → Thanh toán **thành công** (`PaymentStatus.Success`), tạo/giữ tiền cọc (`DepositStatus.Held` nếu có cọc), đánh dấu booking là `Paid` và phát event `PaymentSucceeded`.
  - Giá trị khác `"00"` (ví dụ `"24"`, `"99"`) → Thanh toán **thất bại** (`PaymentStatus.Failed`) và phát event `PaymentFailed`.
- `signature`:
  - Nếu backend **không cấu hình** `Payments:WebhookSecret`, chấp nhận **mọi chuỗi khác rỗng ngoại trừ `"INVALID"`**.
  - Nếu backend **có cấu hình** `Payments:WebhookSecret`, `signature` phải khớp đúng giá trị đó.

---

### Bước 4: Kiểm tra / Polling kết quả thanh toán & tiền cọc

Ở **cả Mock lẫn Real**, tại trang `returnUrl` (ví dụ `/payment/result`) hoặc trong lúc hiện modal QR `Transfer`, FE dùng 2 endpoint `GET` sau để lấy trạng thái chính thức từ backend (yêu cầu JWT của người tham gia booking hoặc Admin):

1. **Lấy chi tiết giao dịch theo `paymentId`:**
   - `GET /api/payments/{paymentId}` → Trả về `PaymentDetailDto`:
     - `status`: `"Pending"` | `"Success"` | `"Failed"` | `"PartiallyRefunded"` | `"Refunded"`
     - `method`: `"VNPay"` | `"Paypal"` | `"Stripe"` | `"Transfer"`
     - `gatewayTransactionId`, `amount`, `createdAt`, `processedAt`, `refundedAmount`, `refundedAt`

2. **Lấy tổng hợp thanh toán & tiền cọc theo `bookingId`:**
   - `GET /api/payments/booking/{bookingId}` → Trả về `BookingPaymentSummaryDto`:
     - `mainPayment`: `PaymentDetailDto | null`
     - `deposit`: `DepositDetailDto | null`, trong đó `deposit.status` thuộc [`DepositStatus`](./CarRental.Modules.Payments.Contracts/Enums/DepositStatus.cs): `"Held"` | `"Released"` | `"Deducted"` | `"Forfeited"`

---

### Bước 5: Các endpoint quản lý Hoàn tiền & Tiền cọc (Owner / Admin)

Các endpoint này hoạt động giống nhau giữa Mock và Real (yêu cầu JWT của chủ xe của booking hoặc Admin):

- **Hoàn tiền (toàn phần hoặc một phần):**
  `POST /api/payments/{id}/refund`
  ```json
  {
    "amount": 1000000,
    "reason": "Early return partial refund"
  }
  ```
  Trả về `PaymentRefundDto` (`status` là `"PartiallyRefunded"` hoặc `"Refunded"`).
  *(Lưu ý: ở chế độ Real, Stripe yêu cầu `gatewayTransactionId` bắt đầu bằng `pi_` hoặc `ch_`; Transfer chỉ ghi nhận refund nội bộ và log để chuyển khoản thủ công.)*

- **Hoàn trả toàn bộ tiền cọc sau khi trả xe:**
  `POST /api/payments/deposits/{depositId}/release`
  ```json
  {
    "note": "Vehicle returned without reported damage"
  }
  ```

- **Khấu trừ tiền cọc (hư hỏng, quá km, trễ hạn):**
  `POST /api/payments/deposits/{depositId}/deduct`
  ```json
  {
    "deductAmount": 250000,
    "reason": "Cleaning fee"
  }
  ```

---

## 4. Khuyến nghị (Best Practices) cho Team FE

Để **không phải sửa code FE** khi backend chuyển từ Mock sang Real:

1. **Tách riêng UI màn hình chờ thanh toán QR (`Transfer`) và luồng redirect (`VNPay` / `Stripe` / `Paypal`):**
   - Với `Transfer`: luôn hiển thị `<img src={checkout.qrCodeUrl} />` và polling `GET /api/payments/{paymentId}` mỗi vài giây cho tới khi `status === "Success"` hoặc `"Failed"`. Luồng này chạy mượt mà ở cả Mock và Real.
2. **Nhận diện Mock mode khéo léo hoặc dùng nút Dev-only trong local:**
   - Khi ở môi trường dev (`import.meta.env.DEV`), nếu phương thức là `VNPay` / `Stripe` / `Paypal` mà backend trả về `qrCodeUrl` khác rỗng (dấu hiệu đặc trưng của `MockPaymentGateway`), hoặc qua một biến cờ env local của FE (ví dụ `VITE_PAYMENT_MOCK_TOOLS=true`), FE có thể hiển thị thêm 2 nút tiện ích **"Giả lập thanh toán thành công"** và **"Giả lập thất bại"** thay vì redirect ra URL giả:
     - Bấm nút → gọi `POST /api/payments/webhook` với `orderId = checkout.paymentId`, `amount = checkout.amount`, `responseCode = "00"` (hoặc `"24"`), `transactionId = "MOCK-" + crypto.randomUUID()`, `signature = "mock-secret"`.
     - Sau đó điều hướng tới `returnUrl` để test trọn vẹn màn hình kết quả (`/payment/result`).
   - Khi chuyển sang môi trường Staging/Prod với `PAYMENTS_USE_MOCK_GATEWAY=false`, `qrCodeUrl` cho `VNPay` / `Stripe` / `Paypal` sẽ là chuỗi rỗng `""` và FE sẽ tự động redirect sang `checkout.paymentUrl` thật mà không cần đổi code nghiệp vụ.
3. **Trang kết quả (`/payment/result`) luôn gọi lại API backend:**
   - Không tin tưởng hoàn toàn vào query params trên URL khi cổng thanh toán redirect về. Luôn đọc `paymentId` (từ state/localStorage hoặc query) và gọi `GET /api/payments/{paymentId}` hoặc `GET /api/payments/booking/{bookingId}` để xác nhận trạng thái thực tế trong database.
   - Riêng khi có query param `token` + `PayerID` từ PayPal Real redirect về, gọi `POST /api/payments/paypal/capture` trước khi hiển thị kết quả cuối cùng.
