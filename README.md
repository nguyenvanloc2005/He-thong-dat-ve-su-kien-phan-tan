# He-thong-dat-ve-su-kien-phan-tan

Hệ thống đặt vé sự kiện phân tán bằng .NET 10, ASP.NET Core Minimal API, RabbitMQ và SQLite.

## Kiến trúc

```text
Client/Web UI -> Ticket API -> SQLite
                     |
                     v
                  RabbitMQ -> Booking Worker -> SQLite
```

- Ticket API cung cấp các endpoint REST tối thiểu và giao diện đặt vé.
- Booking Worker xử lý yêu cầu bất đồng bộ và cập nhật trạng thái.
- SQLite được chia sẻ qua Docker volume để API và worker dùng cùng dữ liệu.
- RabbitMQ dùng queue `booking.requests` với message persistent.

## API

| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/events` | Danh sách sự kiện |
| GET | `/events/{id}` | Chi tiết sự kiện |
| POST | `/bookings` | Tạo yêu cầu đặt vé |
| GET | `/bookings/{id}` | Trạng thái booking |
| GET | `/users/{userId}/bookings` | Danh sách vé của người dùng |

Ví dụ tạo booking:

```json
{
  "eventId": "11111111-1111-1111-1111-111111111111",
  "userId": "user01",
  "quantity": 2
}
```

## Chạy bằng Docker Compose

Yêu cầu: Docker Desktop.

```powershell
docker compose up --build
```

- Web UI/API: http://localhost:8080
- Swagger: http://localhost:8080/swagger
- RabbitMQ management: http://localhost:15672 (`guest` / `guest`)

Dừng hệ thống:

```powershell
docker compose down
```

Xóa cả dữ liệu SQLite:

```powershell
docker compose down -v
```

## Chạy local

Yêu cầu .NET 10 SDK và RabbitMQ trên `localhost:5672`.

Terminal 1:

```powershell
dotnet run --project backend/TicketApi/TicketApi.csproj
```

Terminal 2:

```powershell
dotnet run --project backend/BookingWorker/BookingWorker.csproj
```

API chạy tại địa chỉ được ghi trong console, mặc định URL cấu hình của ASP.NET Core khi chạy development.

## Build kiểm tra

```powershell
dotnet build backend/EventTicketing.slnx
```
