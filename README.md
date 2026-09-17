# MovieBooking Backend (.NET 10 Web API)

Hệ thống Backend cho nền tảng quản lý rạp chiếu phim và đặt vé xem phim trực tuyến (**MovieBooking**), được xây dựng theo kiến trúc **Clean Architecture** trên nền tảng **.NET 10** và cơ sở dữ liệu **PostgreSQL**.

---

## 1. Yêu cầu Hệ thống (Prerequisites)

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Cơ sở dữ liệu [PostgreSQL](https://www.postgresql.org/) (phiên bản 14 trở lên)
- Công cụ EF Core CLI:
  ```powershell
  dotnet tool install --global dotnet-ef
  ```

---

## 2. Cấu trúc Dự án (Clean Architecture)

```text
Backend/MovieBooking/
├── MovieBooking.Domain/            # Enterprise Layer: Entities, Enums, Constants (Không phụ thuộc layer khác)
├── MovieBooking.Application/       # Business Logic Layer: Interfaces, DTOs, Business Rules
├── MovieBooking.Infrastructure/    # Implementation Layer: EF Core AppDbContext, Dịch vụ bên ngoài (VNPay, Cloudinary, AI)
├── MovieBooking/                   # Presentation Layer (API): Controllers, SignalR Hubs, Middleware, DI Configuration
├── MovieBooking.Tests/             # Unit & Integration Tests (xUnit)
├── agent.md                        # Cẩm nang quy tắc phân tầng Clean Architecture
└── rule                            # Bộ quy tắc lập trình bắt buộc
```

---

## 3. Cấu hình Môi trường (Configuration)

Sao chép file mẫu cấu hình để thiết lập thông số kết nối:
```powershell
Copy-Item MovieBooking/appsettings.Example.json MovieBooking/appsettings.json
```

Cập nhật các thông số cần thiết trong `MovieBooking/appsettings.json`:
- **`ConnectionStrings:DefaultConnection`**: Chuỗi kết nối PostgreSQL.
- **`Jwt:SigningKey`**: Chuỗi bảo mật JWT tối thiểu 32 ký tự.
- **`VnPay`**: Cấu hình Merchant VNPay Sandbox (`TmnCode`, `HashSecret`, `PaymentBackReturnUrl`).
- **`AI`**: Cấu hình trợ lý ảo Google Gemini (`Model`: `"gemini-2.5-flash"`, `ApiKey`, `TimeoutSeconds`).
- **`Cloudinary`**: Cấu hình upload media (`CloudName`, `ApiKey`, `ApiSecret`).

---

## 4. Khởi tạo Cơ sở Dữ liệu & Seed Data

1. **Cập nhật Database schema (EF Core Migrations):**
   ```powershell
   dotnet ef database update --project MovieBooking.Infrastructure --startup-project MovieBooking
   ```

2. **Dữ liệu khởi tạo (Seed Data):**
   Khi `Database:SeedOnStartup = true`, ứng dụng sẽ tự động tạo tài khoản Quản trị viên (`admin@gmail.com` / `admin@123`), hệ thống phân quyền (Roles & Permissions), rạp chiếu, phòng chiếu, phim và giá vé mẫu.

---

## 5. Khởi chạy Ứng dụng & Kiểm thử

### Chạy ứng dụng:
```powershell
cd Backend/MovieBooking/MovieBooking
dotnet run
```
Mặc định ứng dụng lắng nghe tại `http://localhost:5275` (hoặc cấu hình trong `launchSettings.json`).

### Truy cập tài liệu Swagger UI:
- Môi trường Local: `http://localhost:5275/swagger`
- Môi trường Staging/Docker: Bật cờ `"Swagger:Enabled": true` trong file cấu hình để xem tài liệu API.

### Chạy Automated Tests:
```powershell
dotnet test Backend/MovieBooking/MovieBooking.Tests/MovieBooking.Tests.csproj
```

---

## 6. Các Phân Hệ Nghiệp Vụ Chính

- **Xác thực & Phân quyền (Auth & RBAC):** JWT Bearer token kèm quyền hạn chi tiết thông qua `[HasPermission("...")]`.
- **Giữ ghế Realtime (Realtime Seat Hold):** Quản lý phiên giữ ghế tạm thời (optimistic concurrency) kết hợp phát sự kiện thời gian thực qua SignalR (`/hubs/seats`) với phiên bản trạng thái `X-Seat-State-Version`.
- **Thanh toán VNPay (Payment Gateway):** Tích hợp cổng VNPay Sandbox, xác thực chữ ký HMAC-SHA512 cho cả luồng Return URL và Webhook IPN ngầm.
- **Trợ lý Ảo AI (Virtual Assistant):** Tích hợp Google Gemini Flash hỗ trợ tư vấn và gợi ý phim, cơ chế giới hạn tần suất gọi (Rate Limiting: 10 req/phút) và lọc ảo giác đối chiếu dữ liệu phim thực tế.
- **Dịch vụ chạy ngầm (Hosted Services):** Tự động dọn dẹp giữ ghế hết hạn (`ExpiredSeatHoldsCleanupService`), đơn vé quá hạn (`ExpiredBookingsCleanupService`) và cập nhật trạng thái phim (`MovieStatusUpdateService`).

---

## 7. Tài liệu Chi tiết cho Lập trình viên

Xem cẩm nang phát triển chi tiết tại:
👉 **[docs/backend-developer-guide.md](../../docs/backend-developer-guide.md)**
