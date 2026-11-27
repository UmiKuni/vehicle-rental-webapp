# Tài liệu Bàn giao cho Team Frontend: Cập nhật Cloudinary Storage & Hỗ trợ File PDF (KYC)

> **Nhánh Backend:** `fix/cloudinary-pdf-and-storage-quirks`  
> **Phạm vi ảnh hưởng tới FE:** `fe/` (luồng KYC khách hàng, trang Admin duyệt giấy tờ, trang Handover) và lưu ý chung nếu mở rộng sang `admin-portal/`.

---

## 1. Tóm tắt các thay đổi phía Backend (`be/`)

Backend vừa khắc phục triệt để lỗi xử lý file PDF trên Cloudinary và chuẩn hóa toàn bộ cơ chế lưu trữ/bảo mật file cho `IImageStorage` ([CloudinaryImageStorage.cs](../src/Host/CarRental.Api/Infrastructure/Storage/CloudinaryImageStorage.cs) & [StorageValidation.cs](../src/Shared/CarRental.BuildingBlocks/Storage/StorageValidation.cs)):

1. **`storageKey` luôn kèm đuôi định dạng chuẩn (`.jpg`, `.png`, `.webp`, `.pdf`):**
   - Ví dụ mới: `identity/0193.../01936f00aabbccddeeff001122334455.pdf` hoặc `handover/0193.../01936f00aabbccddeeff001122334455.jpg`.
   - Các bản ghi cũ trong DB chưa có đuôi mở rộng vẫn được hỗ trợ tương thích ngược (backward-compatible).
   - **Lợi ích cho FE:** FE có thể kiểm tra trực tiếp `storageKey.toLowerCase().endsWith('.pdf')` để phân biệt tài liệu PDF với ảnh tĩnh mà không cần đoán từ URL.

2. **Sửa lỗi 404 & hỗ trợ xem trước (inline) cho PDF trên Cloudinary:**
   - Trước đây PDF được upload dưới dạng `raw` nhưng khi tạo link đọc (`GetUrl`) và xóa (`DeleteAsync`) lại gọi nhầm sang endpoint `image`, dẫn tới lỗi HTTP `404` và rò rỉ file.
   - Hiện tại PDF được định tuyến đúng sang `raw` kèm đuôi `.pdf` trong `public_id` và `attachment=false`, giúp trình duyệt mở xem trực tiếp (`Content-Type: application/pdf`) kể cả trên gói Cloudinary Free.

3. **URL riêng tư (`Private`) có thời hạn hết hạn thực sự trên Server Cloudinary:**
   - Thay vì gắn `?exp=...` giả lập vào link CDN tĩnh, Backend chuyển sang tạo link qua `DownloadPrivate` (`https://api.cloudinary.com/v1_1/<cloud>/<image|raw>/download?...&expires_at=<unix_timestamp>&signature=...`).
   - Thời hạn hiệu lực của từng loại URL riêng tư:
     - **Khách hàng tự xem trạng thái KYC** (`GET /api/users/me/verification-status`, `POST /api/users/me/documents/{type}`): **5 phút**.
     - **Ảnh biên bản giao/nhận xe** (`GET /api/handovers/{id}`, `POST /api/handovers/{id}/photos`): **10 phút**.
     - **Admin duyệt giấy tờ chờ xác minh** (`GET /api/users/verifications/pending`): **15 phút**.
   - Ảnh xe công khai của chủ xe (`Fleet` — `StorageVisibility.Public`) vẫn dùng link CDN công khai tốc độ cao (`https://res.cloudinary.com/.../image/upload/...`) và không hết hạn.

4. **Kiểm tra chữ ký byte thực tế của file (Magic Bytes Validation):**
   - Backend không còn chỉ tin vào `Content-Type` do trình duyệt gửi lên mà đọc trực tiếp header nhị phân của file (`%PDF-`, JPEG `FF D8 FF`, PNG `89 50 4E 47`, WebP `RIFF....WEBP`).
   - Nếu file bị đổi tên giả mạo (ví dụ ảnh `.heic` trên iPhone hoặc file `.png` bị đổi đuôi thành `.jpg`, hoặc file rỗng/hỏng), API sẽ trả về **HTTP `400 Bad Request`** (`application/problem+json`) với mã lỗi:
     - `errorCode: "storage.invalid_file_signature"` — nội dung nhị phân không khớp với `Content-Type` khai báo hoặc không phải định dạng hợp lệ.
     - Các mã lỗi cũ giữ nguyên: `"storage.unsupported_content_type"`, `"storage.file_too_large"` (tối đa **10 MB**), `"storage.empty_stream"`.

5. **Tự động dọn file cũ khi upload đè:**
   - Khi khách hàng upload lại giấy tờ cùng loại (`CCCD` / `GPLX`) hoặc chụp lại cùng một góc xe (`FrontLeft`, `FrontRight`, `RearLeft`, `RearRight`, `Dash`) khi biên bản ở trạng thái `Draft`, Backend tự động xóa file cũ trên Cloudinary sau khi lưu DB thành công. Link `accessUrl` / `viewUrl` cũ của mục đó sẽ lập tức hết hiệu lực.

---

## 2. Phân quyền định dạng file theo từng API

- **Cho phép cả Ảnh (`JPG`, `PNG`, `WEBP`) VÀ `PDF` (`application/pdf`, tối đa 10 MB):**
  - `POST /api/users/me/documents/{type}` (`type`: `CCCD` | `GPLX` | `Passport`).
- **CHỈ cho phép Ảnh (`JPG`, `PNG`, `WEBP`, tối đa 10 MB) — TỪ CHỐI `PDF`:**
  - `POST /api/fleet/vehicles/{id}/images` (ảnh đăng xe của Owner).
  - `POST /api/handovers/{id}/photos` (ảnh chụp 5 góc khi nhận/trả xe).

---

## 3. Các việc Team Frontend cần thực hiện (dựa trên code `dev` mới nhất)

> Lưu ý: Hiện tại `admin-portal/` chỉ mới có quản lý danh mục xe, duyệt xe và khóa/mở khóa tài khoản ([UserAccess.tsx](../../admin-portal/src/UserAccess.tsx)), còn trang Admin duyệt giấy tờ KYC đang nằm trong `fe/` ([IdentityVerificationsPage.tsx](../../fe/src/features/admin/pages/IdentityVerificationsPage.tsx)). Nếu sau này team chuyển tính năng duyệt KYC sang `admin-portal/`, vui lòng áp dụng cùng logic bên dưới.

### 3.1. Sửa hiển thị PDF trên trang Admin duyệt giấy tờ (Ưu tiên cao)
- **File hiện tại:** [IdentityVerificationsPage.tsx](../../fe/src/features/admin/pages/IdentityVerificationsPage.tsx) (dòng 94–98).
- **Vấn đề:** Hiện tại mọi `document.accessUrl` đều được nhét vào thẻ `<img src={document.accessUrl} />`. Nếu khách hàng upload file PDF, thẻ `<img>` sẽ báo ảnh lỗi (broken image) dù link PDF hoàn toàn hợp lệ.
- **Hướng xử lý đề xuất:**
  - Kiểm tra `const isPdf = document.storageKey.toLowerCase().endsWith('.pdf')`.
  - Nếu `isPdf === true`: Render bằng `<iframe src={document.accessUrl} title={...} className="h-80 w-full rounded-lg border-0 bg-white" />` (hoặc thẻ `<object type="application/pdf" />`) kèm nút/link **"Mở PDF trong tab mới"** (`<a href={document.accessUrl} target="_blank" rel="noreferrer">`).
  - Nếu `isPdf === false`: Giữ nguyên thẻ `<img src={document.accessUrl} />` như hiện tại.

### 3.2. Mở hỗ trợ chọn & upload file PDF ở luồng KYC Khách hàng (Nếu sản phẩm muốn cho phép khách nộp PDF)
Dựa trên code mới nhất sau khi cập nhật `fetchCurrentUser()` trong [profile.api.ts](../../fe/src/features/profile/api/profile.api.ts), nếu FE muốn cho phép người dùng tải lên bản scan PDF cho CCCD / GPLX thì cần cập nhật 3 điểm sau:

1. **Component [ImageUploadZone.tsx](../../fe/src/features/profile/components/ImageUploadZone.tsx) (dòng 41–75):**
   - Bổ sung prop tùy chọn `acceptPdf?: boolean` (mặc định `false` để không ảnh hưởng tới các nơi chỉ nhận ảnh).
   - Khi `acceptPdf === true`:
     - Đổi `accept` của `<input type="file" />` thành `"image/jpeg,image/jpg,image/png,image/webp,application/pdf"`.
     - **Lưu ý mobile:** Bỏ thuộc tính cứng `capture="environment"` khi bật `acceptPdf`, vì trên nhiều trình duyệt Android/iOS, `capture="environment"` ép mở thẳng Camera và ẩn trình chọn tệp PDF trong máy.
     - Khi file được chọn là PDF (`file.type === 'application/pdf'`), hiển thị khung báo tên file PDF + icon tài liệu thay vì `<img src={preview} />` (vì `URL.createObjectURL(pdfFile)` không render được trong thẻ `<img>`).

2. **Trang [IdentityVerificationPage.tsx](../../fe/src/features/profile/pages/IdentityVerificationPage.tsx) (dòng 18–39) & [LicenseVerificationPage.tsx](../../fe/src/features/profile/pages/LicenseVerificationPage.tsx) (dòng 26–66):**
   - Bổ sung `'application/pdf'` vào mảng kiểm tra `file.type.toLowerCase()`.
   - Cho phép người dùng nộp **1 file PDF chung (chứa cả 2 mặt)** *hoặc* **2 file ảnh mặt trước + mặt sau** (hiện tại điều kiện `if (!frontFile || !backFile)` đang bắt buộc luôn phải có đủ 2 file).

3. **Hàm `combineImages` trong [profile.api.ts](../../fe/src/features/profile/api/profile.api.ts) (dòng 14–40, 115–136):**
   - Hiện tại nếu truyền 1 file (`files.length === 1`), hàm `combineImages` trả về nguyên gốc `files[0]` (chạy tốt với 1 file PDF đơn lẻ!).
   - **Tuy nhiên**, nếu người dùng chọn 2 file mà trong đó có file PDF (`files.some(f => f.type === 'application/pdf')`), hàm `createImageBitmap(file)` để ghép `<canvas>` ở dòng 17 sẽ **throw lỗi ngay lập tức**.
   - **Cách sửa:** Nếu người dùng chọn chế độ tải PDF thì chỉ nhận 1 file PDF duy nhất (bỏ qua bước ghép `<canvas>`), chỉ gọi `combineImages()` khi cả mặt trước và mặt sau đều là file ảnh (`image/*`).

### 3.3. Quản lý vòng đời Signed URL & Xử lý lỗi Upload
- **Không lưu cứng Signed URL:** Tuyệt đối không lưu `accessUrl` (KYC) hay `viewUrl` (Handover) vào `localStorage` / `sessionStorage`.
- **Cấu hình cache hợp lý (React Query):**
  - Với query `['admin', 'identity-verifications']` ([IdentityVerificationsPage.tsx](../../fe/src/features/admin/pages/IdentityVerificationsPage.tsx)), giữ `staleTime` nhỏ hơn **10–12 phút** (vì URL hết hạn sau 15 phút) hoặc nhắc Admin bấm nút **Refresh** đã có sẵn trên giao diện nếu treo tab quá lâu khiến ảnh/PDF trả về lỗi hết hạn (`401`/`403`/`404`).
  - Với query trạng thái KYC cá nhân (`useKycStatus`) và chi tiết biên bản bàn giao (`HandoverPage.tsx`), không đặt `staleTime` vượt quá **4 phút** (KYC) / **8 phút** (Handover).
- **Hiển thị thông báo lỗi rõ ràng khi bị chặn Magic Bytes:**
  - Khi API upload trả về `400` kèm `errorCode === "storage.invalid_file_signature"`, hiển thị thông báo thân thiện (ví dụ: *"Định dạng nội dung tệp không khớp hoặc bị hỏng (lưu ý không đổi tên ảnh HEIC sang JPG). Vui lòng chọn tệp JPG, PNG, WEBP hoặc PDF hợp lệ."*).
