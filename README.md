# StockManager

ASP.NET Core 10 Web API · PostgreSQL · EF Core (code-first) · JWT + Identity · MediatR (CQRS) · AutoMapper · FluentValidation · Polly · Swagger

## Quick start

```bash
# 1. PostgreSQL (example)
docker run -d --name stockmanager-db -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=stockmanager -p 5432:5432 postgres:17

# 2. Secrets (do NOT keep real secrets in appsettings.json)
cd StockManager
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<random string, at least 32 characters>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=stockmanager;Username=postgres;Password=postgres"

# 3. Create the first migration (once) — the repo ships without one
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update      # optional: the app also migrates itself on startup (Database:ApplyMigrationsOnStartup)

# 4. Run -> Swagger opens at http://localhost:5225/swagger
dotnet run
```

In **Development** an admin is seeded from `appsettings.Development.json`: `admin@stockmanager.local` / `Admin123!`.
In other environments set `Seed__AdminEmail` / `Seed__AdminPassword` env variables (or leave empty for no seed admin).

Tests: `dotnet test` (unit tests use Moq + EF InMemory; integration tests boot the whole app with `WebApplicationFactory`, no PostgreSQL needed).

## Roles (RBAC)

| Role | Rights |
|---|---|
| Viewer (default after `/register`) | read everything except audit log |
| Storekeeper | Viewer + register receipts / write-offs |
| Manager | Storekeeper + create/update products, categories, suppliers, warehouses, upload images |
| Admin | everything: deletes, audit log, role assignment (`PUT /api/auth/users/{id}/role`) |

## Endpoints

| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `PUT /api/auth/users/{id}/role` |
| Products (CQRS) | `GET /api/products` (search, categoryId, supplierId, minPrice, maxPrice, inStock, sortBy, sortDirection, page, pageSize), `GET/PUT/DELETE /api/products/{id}`, `POST /api/products`, `POST /api/products/{id}/image`, `GET /api/products/{id}/price?currency=EUR` |
| Categories / Suppliers / Warehouses | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`; `GET /api/warehouses/{id}/stock` |
| Stock | `POST /api/stock-movements/receipts`, `POST /api/stock-movements/write-offs`, `GET /api/stock-movements` |
| Audit | `GET /api/audit-logs` (Admin) |

Errors are always `application/problem+json`; validation errors list every field with a readable message.

## Where each course requirement lives

| # | Requirement | Where |
|---|---|---|
| 1 | Controllers / Services / Data access / DI | `Controllers/`, `Service/Services/`, `Service/Data/`, `Abstract/` (interfaces), `Service/Extensions/ServiceCollectionExtensions.cs` |
| 2 | REST, status codes | 200/201/204/400/401/403/404/409/429/503, `CreatedAtAction` |
| 3 | DTO + AutoMapper | `Models/DTOs/`, `Service/Mappings/MappingProfile.cs` (`ProjectTo` for reads) |
| 4 | EF Core + PostgreSQL | `ApplicationDbContext` (relations, indexes, check constraint, concurrency token) |
| 5 | Pagination/filter/sort | `QueryableExtensions`, `*FilterParameters`, whitelisted sort fields |
| 6 | Validation | `Service/Validators/` + `ValidationActionFilter` |
| 7 | Identity + JWT + roles | `AuthService`, `AddJwtAuthentication`, `[Authorize(Roles=...)]` |
| 8 | CQRS + MediatR | `Models/CQRS/`, `Service/Handlers/` (products, stock movements) |
| 9 | Middleware | `Service/Middlewares/` (exception handling, request logging + correlation id) |
| 10 | File upload | `FileService`: size, extension, content-type and magic-byte checks, random file name |
| 11 | Tests | `Tests/UnitTests` (Moq), `Tests/IntegrationTests` |
| 12 | Swagger + JWT | `AddSwaggerWithJwt` (Authorize button) |
| 13 | External API + Polly | `CurrencyService` (open.er-api.com): retry + circuit breaker + timeout, cache and stale-rate fallback |
| 14 | Config, CORS, rate limiting | `appsettings*.json`, `AddCorsPolicy`, `AddRateLimiting` (global per IP + strict `auth` policy) |

## Design notes

* **Stock balance** is stored in `StockLevels` (product × warehouse). A write-off first checks the balance and throws `InsufficientStockException` (409). Movement + balance are saved in one `SaveChanges` (one transaction), a `ConcurrencyStamp` token rejects lost updates, and a DB check constraint forbids negative balances.
* **Audit log**: any change to Product / Category / Supplier / Warehouse / StockMovement is journaled automatically by `ApplicationDbContext.SaveChangesAsync` (user, IP, old/new values as JSON); register / login / role changes are journaled by `AuthService`.
* A product that has movements cannot be deleted (history is preserved).
* `AutoMapper 14` is pinned deliberately (v15+ needs a license key); NuGet may show an advisory warning for it.
