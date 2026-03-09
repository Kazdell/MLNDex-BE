# Kế hoạch bổ sung Seeding Admin

## Vấn đề hiện tại
1. **Auth**: Code `AuthService` đã hoàn hiện các chức năng cơ bản (Register, Login, OTP, Logout). Tuy nhiên cần kiểm tra thực tế trên FE.
2. **Admin Seeding**: `appsettings.json` có cấu hình Admin nhưng Backend chưa có logic để đọc cấu hình này và tạo user vào Database.

## Giải pháp đề xuất
1. **Tạo `DataSeeder` service**:
   - Tự động tạo các Role (ADMIN, MODERATOR, CREATOR, etc.) nếu chưa có.
   - Đọc config `Admin` từ `appsettings.json`.
   - Kiểm tra nếu User Admin chưa tồn tại thì tạo mới và gán role `ADMIN`.
2. **Kích hoạt Seeder trong `Program.cs`**: Đảm bảo admin luôn sẵn sàng khi chạy app.

## Các bước thực hiện
1. [ ] Tạo file `c:\Users\ACER\Downloads\MLNDex\MLNDex-BE\Infrastructure\Persistence\Data\DataSeeder.cs`
2. [ ] Sửa `Program.cs` để gọi `DataSeeder` lúc khởi chạy.
3. [ ] Hướng dẫn User cách test Register/Login trên FE.
