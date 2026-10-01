# Zamzam Water Delivery

A web application for a drinking-water delivery company, built with ASP.NET Core 8, EF Core, SQL Server, ASP.NET Core Identity and Bootstrap 5.

- **Customers** register, browse water products, use a cart, check out (Cash on Delivery) and track their order on a progress bar.
- **Admins** manage products, categories, orders, customers, drivers, deliveries and payments. They also get a dashboard with charts and reports that export to PDF, Excel and CSV.
- **Drivers** see their assigned deliveries and can call or WhatsApp the customer, accept a delivery, mark it Out for Delivery and then Delivered, and set their availability.
- A **REST API** (JWT, Swagger docs) covers products, orders, customers, drivers, deliveries and payments.

---

## 1. Requirements

| Tool | Version |
|------|---------|
| .NET SDK | 8.0 or newer (the projects target `net8.0`; `RollForward=Major` lets them run on the .NET 9/10 runtime too) |
| SQL Server | 2016+ (Express, Developer or LocalDB) |
| EF Core CLI (optional) | `dotnet tool install --global dotnet-ef` |

## 2. Quick start

```bash
cd ZamzamWaterDelivery
dotnet restore
dotnet run --project ZamzamWaterDelivery.Web --launch-profile https
```

Open **https://localhost:7180**. On first run the app:

1. applies the EF Core migrations, which create the `ZamzamWaterDeliveryDb` database, and
2. seeds the roles, the users below, categories, products and about 40 sample orders so the dashboard has data.

### Demo accounts (Development only)

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@zamzam.so | `Zamzam@2026` |
| Driver | driver@zamzam.so | `Zamzam@2026` |
| Driver | driver2@zamzam.so | `Zamzam@2026` |
| Customer | customer@zamzam.so | `Zamzam@2026` |
| Customer | mohamed@zamzam.so | `Zamzam@2026` |

The demo password lives only in `appsettings.Development.json`. Override it with User Secrets:

```bash
cd ZamzamWaterDelivery.Web
dotnet user-secrets set "Seed:DefaultPassword" "Your$trongPassw0rd"
```

## 3. Configuration

All settings are in `ZamzamWaterDelivery.Web/appsettings.json`. Secrets are **not** stored in the repository.

| Setting | Purpose |
|---------|---------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection. The default is `Server=.` (the local default instance). For LocalDB use `Server=(localdb)\\MSSQLLocalDB`. |
| `Jwt:Key` | API token signing key (32+ chars). **Required in Production.** In Development a random key is generated at startup. |
| `Seed:DefaultPassword` | Password for the seeded demo users. If empty, no users are created. |
| `Delivery:BaseFee`, `Delivery:FreeDeliveryThreshold`, `Delivery:TimeSlots` | Delivery pricing and time slots. |
| `Company:*` | Company name, phone, address and currency shown in the UI. |
| `Notifications:Sms/WhatsApp:Enabled` | Turns the (log-only) SMS/WhatsApp channels on or off. |
| `Database:ApplyMigrationsOnStartup` | Apply migrations automatically (default `true`). |
| `App:EnableSwagger` | Enable Swagger outside Development. |

Set secrets as environment variables in production (double underscore = section separator):

```bash
ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"
Jwt__Key="a-long-random-secret-of-at-least-32-characters"
Seed__DefaultPassword="..."
```

## 4. Database

- Migrations: `ZamzamWaterDelivery.Infrastructure/Migrations`
- Full SQL script (idempotent): `Database/ZamzamWaterDelivery_Schema.sql`

Manual commands:

```bash
# apply migrations
dotnet ef database update -p ZamzamWaterDelivery.Infrastructure -s ZamzamWaterDelivery.Infrastructure
# add a new migration after changing entities
dotnet ef migrations add <Name> -p ZamzamWaterDelivery.Infrastructure -s ZamzamWaterDelivery.Infrastructure -o Migrations
```

**Tables:** AspNetUsers (ApplicationUser) and the Identity tables, Customers, Addresses, Drivers, Categories, Products, Carts, CartItems, Orders, OrderItems, Deliveries, Payments, Notifications.

Key rules:
- Order ↔ Delivery and Order ↔ Payment are one-to-one.
- **Add / Edit / Delete:** every admin section (Products, Categories, Customers, Drivers) has an **Add** button and **Edit** and **Delete** buttons on each row.
- **Delete rules:** an item with no history is deleted permanently. One that appears in past orders or deliveries is *archived* (`IsDeleted`). It disappears from every list, but old orders and reports stay correct, and its name, email or license number can be reused. Three cases are refused:
  - a category that still has products;
  - a driver with active deliveries;
  - a customer with open orders.
- Check constraints stop stock and prices from going negative.
- Order number, product name and driver license number are unique.

## 5. Architecture

```
ZamzamWaterDelivery/
├── ZamzamWaterDelivery.Domain/          Entities, enums, repository/unit-of-work interfaces, order status rules
├── ZamzamWaterDelivery.Application/     DTOs, service interfaces + implementations, validators, mappings
├── ZamzamWaterDelivery.Infrastructure/  DbContext, entity configurations, repositories, migrations,
│                                        JWT tokens, report exporters (PDF/Excel/CSV), notification channels, seed data
├── ZamzamWaterDelivery.Web/             MVC controllers & views, Admin + Driver areas, REST API, wwwroot
└── Database/                            Generated SQL schema script
```

- **Clean / layered architecture**: Web → Infrastructure → Application → Domain.
- **Repository + Unit of Work** over EF Core. Services handle all business rules, and controllers stay thin.
- **DTOs** everywhere. Entities are never returned from the API.
- **Order workflow**: Pending → Confirmed → Preparing → Out for Delivery → Delivered, or Cancelled. Transitions are checked in `OrderStatusRules`. Stock is reserved when an order is placed and restored when it is cancelled. Cash-on-delivery payments become *Paid* when the order is delivered.
- **Extensibility**
  - Payments: implement `IPaymentGateway` (e.g. EVC Plus, Zaad, card) and register it in `Application/DependencyInjection.cs`. It then becomes selectable at checkout automatically.
  - Notifications: implement `INotificationChannel` (SMS, WhatsApp, e-mail). The included channels only log messages. Replace `SendAsync` with a provider call.

## 6. REST API

Swagger UI: **https://localhost:7180/swagger** (Development).

1. `POST /api/auth/login` with `{ "email": "...", "password": "..." }` returns `data.token`.
2. Click **Authorize** in Swagger and paste the token.

| Method | Endpoint | Role |
|--------|----------|------|
| POST | `/api/auth/register`, `/api/auth/login` | anonymous |
| GET | `/api/products`, `/api/products/{id}` | anonymous |
| POST/PUT/DELETE | `/api/products`, `/api/products/{id}` | Admin |
| GET | `/api/orders`, `/api/orders/{id}` | Admin (all) / Customer (own) |
| POST | `/api/orders` | Customer |
| PUT | `/api/orders/{id}` (change status) | Admin |
| POST | `/api/orders/{id}/assign-driver`, `/api/orders/{id}/cancel` | Admin / Customer (cancel own pending) |
| GET | `/api/customers`, `/api/customers/{id}` | Admin |
| GET/POST/PUT | `/api/drivers` … | Admin |
| GET/PUT | `/api/deliveries`, `/api/deliveries/{id}` | Admin |
| GET/POST | `/api/deliveries/mine`, `/{id}/accept`, `/{id}/start`, `/{id}/complete` | Driver |
| GET/POST | `/api/payments` | Admin |

Every response uses the same envelope:

```json
{ "success": true, "message": "Order placed.", "data": { ... }, "errors": null }
```

Errors return proper status codes: 400 validation or business rule, 401, 403, 404, and 500 with a generic message.

## 7. Security

- ASP.NET Core Identity password hashing, a strong password policy and lockout after 5 failed logins.
- Role-based authorization (Admin, Customer, Driver) on the MVC areas and API endpoints.
- Antiforgery tokens on every MVC form and AJAX call. The API accepts only JWT bearer tokens, so it has no cookie/CSRF exposure.
- HTTPS redirection, HSTS, secure/HttpOnly cookies and security headers.
- EF Core parameterised queries protect against SQL injection. Server-side and client-side validation are both applied.
- Uploaded product images are type- and size-checked and stored with random file names.
- Deactivated accounts are locked and signed out through a security-stamp rotation.

## 8. Troubleshooting

- **Cannot connect to SQL Server**: check the service is running and adjust `ConnectionStrings:DefaultConnection` (e.g. `Server=.\\SQLEXPRESS`).
- **HTTPS certificate warning**: run `dotnet dev-certs https --trust`.
- **No users after first run**: set `Seed:DefaultPassword` (see section 2) and restart.
