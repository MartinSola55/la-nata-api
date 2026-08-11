# CLAUDE.md

Guidance for future work in this repository.

## Source Of Truth

- This repository is the backend API for **La Ñata** (rental of chairs, tables, tableware, linen and furniture). It is the rewrite of the legacy ASP.NET MVC 5 app that lives in a separate `La_Nata` repository. The web/frontend project will be created separately.
- The legacy app is the reference for **behaviour and wording**, not for structure: same business rules, same Spanish messages, new architecture.
- **The production database schema is fixed and is not ours to redesign.** The API runs against the same live database the legacy app uses. Entities map onto it through `[Table]` and `[Column]` attributes; the code adapts to the columns, never the other way around.
- The only DDL this project is allowed to run is **additive**: new columns, new indexes, new tables in a separate schema. Never `ALTER COLUMN`, never `DROP`, never a rename. If a change seems to need one, raise it instead of writing it.
- Prefer patterns already present in this project over inventing new structure.

## Project Shape

- Target framework: `.NET 10`, `net10.0`.
- Solution file: `LaNata-Api.sln`.
- Main project folder: `LaNata-Api/`.
- Root namespace: `LaNata_Api`.
- Single API project unless explicitly requested otherwise.
- Keep root-level documentation in `README.md`.

## Libraries

- The project is split into libraries by business area. They are folders inside the single `LaNata-Api` project, not separate `.csproj` files. Do not split them into separate projects unless explicitly requested.
- Current libraries:
  - `Core` — the business itself (products, orders, expenses, dashboard) plus users, roles, auth, the `APIContext`, security config, and shared DTOs/constants.
  - `Audit` — audit trail: `AuditableEntity`, `AuditInterceptor` and the `audit` schema.
  - `Export` — CSV and XLSX generation.
  - `Report` — PDF generation.
- Every library repeats the same inner layout, and only creates the folders it actually needs:

```text
LaNata-Api/
  <Library>/
    Controllers/
    DAL/
      DB/
      Filters/
    Helpers/
      Documents/
      Interceptors/
    Models/
      Constants/
      DTO/
      Internal/
    Security/
      Config/
    Services/
  Program.cs
  appsettings.json
```

- `Core/DAL/Migrations/` is the exception to the per-library layout: every SQL migration lives there, whichever library owns the tables.

- Namespaces mirror the folders: `LaNata_Api.<Library>.<Layer>[.<SubFolder>]` — for example `LaNata_Api.Core.Models.DTO.Order`, `LaNata_Api.Report.Services`, `LaNata_Api.Audit.Helpers.Interceptors`.
- Cross-library references are allowed and go through normal `using` directives. `Core` is the composition point: `ServiceContainer` registers the services of every library, and `APIContext` owns the `DbSet`s of every library's entities.
- When adding an external integration (payments, messaging, storage), create a new library folder instead of growing `Core`. Business features of La Ñata itself belong in `Core`.
- This project carries no unused scaffolding. If a library, service or constant stops being used, delete it rather than leaving it around "just in case" — git history is the backup.

## Folder Structure

- `Program.cs`, `appsettings.json`, and `Properties/` stay at the project root, outside the libraries.
- Keep classes in one file named after the class, inside the folder matching their layer.

## Startup Configuration

- `Program.cs` stays a thin composition root: it only wires modules together and declares the middleware pipeline. Do not put configuration bodies (option lambdas, validation, policy definitions) in it.
- Each cross-cutting concern lives in its own `*Config` class, following the existing `*Container` convention: a plain class with `public static void Add...(IServiceCollection services, ...)` methods (not extension methods).
  - `Core/Security/Config/CorsConfig.cs` — CORS policy; owns the `PolicyName` constant.
  - `Core/Security/Config/AuthenticationConfig.cs` — JWT bearer setup and `Jwt:*` validation.
  - `Core/Security/Config/AuthorizationConfig.cs` — the `Policies.Admin` / `Policies.Employee` policies.
  - `Core/Security/Config/RateLimitConfig.cs` — rate limiter policies and the `429` rejection response.
  - `Core/Security/Config/SecurityHeadersConfig.cs` — response security headers (`UseSecurityHeaders`, the one extension method here) and the Kestrel server-header opt-out.
  - `Core/DAL/DB/DatabaseConfig.cs` — connection string, `APIContext` registration, interceptors, and `RunMigrations`.
  - `Report/Config/ReportConfig.cs` — QuestPDF license and debugging flags. Must run before the first render.
- `ServiceContainer.AddServices(services)` registers every service. It takes no `IConfiguration` today because no service binds an options class; add the parameter back if one ever does.
- An options class that a `*Config` needs for its own wiring is declared nested inside it instead of living under `Models/`, so the config class stays the single place to look.
- Config classes read `IConfiguration` themselves and throw `InvalidOperationException` on missing required keys, so a misconfigured environment fails at startup rather than at request time.
- When adding a new cross-cutting concern, add a `*Config` class and a single call in `Program.cs`.

## Formatting Style

- C# uses block-scoped namespaces:

```csharp
namespace LaNata_Api.Core.Services
{
    public class ExampleService
    {
    }
}
```

- Use 4 spaces for C# indentation.
- Keep explicit `using` directives at the top of files.
- Keep classes in one file named after the class.
- Use primary constructors where the project already uses them.
- Store constructor parameters in private readonly fields when used repeatedly.
- Prefer collection expressions instead of `.ToList()` when valid.
- Use `#region` sections where the service/controller already follows that style:
  - `Combos`
  - `CRUD`
  - `Methods`
  - `Documents`
  - `Audit`
  - `Validations`
  - `Helpers`
  - `Private`
- Project XML uses 2-space indentation.
- JSON config uses 2-space indentation.
- SQL scripts use uppercase SQL keywords and 4-space indentation inside `BEGIN` blocks.

## Controllers

- Controllers should only be a bypass/delegation layer to services.
- Do not put business logic in controllers.
- Controllers return `BaseResponse` or `BaseResponse<T>` directly.
- Use route pattern:

```csharp
[ApiController]
[Route("api/[controller]/[action]")]
```

- Protected controllers inherit from `BaseController`.
- `BaseController` has `[Authorize]`.
- `AuthController` does not inherit from `BaseController` because login must be public.
- Use `[Authorize(Policy = Policies.Admin)]` for admin-only actions. The business controllers (`Product`, `Order`, `Expense`, `Dashboard`) deliberately do **not** use it: in the legacy app every logged-in user could manage products, orders and expenses. Only user administration and the audit trail are admin-only.

## Services

- Put business logic in services under `<Library>/Services/`.
- Register services in `ServiceContainer.AddServices`.
- Do not create or implement service interfaces.
- Keep service names concrete: `AuthService`, `ProductService`, `OrderService`, `ExpenseService`, etc.
- Use EF Core directly through `APIContext`.
- Use `try/catch` around persistence calls where failure should return `BaseResponse` errors.
- Return user-facing errors through `Messages`.
- Do not throw for expected validation failures.

## Interfaces

- No custom service interfaces should be created or implemented.
- Do not add `IProductService`, `IOrderService`, repository interfaces, or similar abstractions.
- Use framework-provided types only where required by packages/framework registration.

## Auth And Roles

- Use custom `User` and `Role` entities. `Role` maps the legacy `Rol` table.
- Do not switch to ASP.NET Identity unless explicitly requested.
- **The `User` table has no name columns** — only `email`, `password` and `id_rol`. The email is the only human-readable identity anywhere: in the token, in the audit trail and in report authorship. Do not add `Name`/`LastName` back without the columns to store them.
- Passwords are hashed with `BCrypt.Net-Next`, and only BCrypt is accepted. The legacy unsalted SHA-256 hashes cannot be verified, so a carried-over user has to have their hash written once before they can log in — `dotnet run -- --hash "..."` prints one.
- The `password` column is `char(64)`, sized for the SHA-256 hex it used to hold. A BCrypt hash is 60 characters and SQL Server pads the read back to 64, so `AuthService.VerifyPassword` trims before verifying. Never compare the column directly.
- Login does not enforce password complexity; only `Create`, `Update` and `UpdatePassword` do. Validating format at login would reject valid existing passwords and leak the policy.
- JWT auth uses:
  - `Jwt:Issuer`
  - `Jwt:Audience`
  - `Jwt:Key`
- JWT claims include:
  - `ClaimTypes.Email`
  - custom `userId`
  - `ClaimTypes.Role`
- Role values live in `Core/Models/Constants/Roles.cs`.
- Policy names live in `Core/Models/Constants/Policies.cs`.
- Configure policies with `RequireRole`.
- Current roles, matching the text already in the `Rol` table:
  - `Administrador`
  - `Empleado`

## Responses And DTOs

- Use `BaseResponse` and `BaseResponse<T>` from `Core/Models/DTO/BaseResponse.cs`.
- Request/response DTOs live under `<Library>/Models/DTO/<Feature>/`.
- Name DTO files by operation:
  - `Create.cs`
  - `Update.cs`
  - `Delete.cs`
  - `GetAll.cs`
  - `GetOne.cs`
  - `UpdatePassword.cs`
- DTO class names follow the existing pattern:
  - `CreateRequest`
  - `CreateResponse`
  - `GetAllRequest`
  - `GetAllResponse`
- `GetAllResponse` contains an `Items` list with nested `Item` classes.
- Sorting uses `SortRequest` (`ColumnSort` + `SortDirection`) from `Core/Models/DTO/Sort.cs`. `ColumnSort` has **no default**: each service's `OrderQuery` owns the natural order of its listing through the `_ =>` branch, which is what keeps a listing sorted the way the legacy screen showed it.
- `PaginateRequest` extends `SortRequest` with `Page`, and page size comes from `Pagination.DefaultPageSize`.

## Generic CRUD Pattern

- `ProductService` is the reference implementation: CRUD + audit + export + report, in that file order.
- CRUD services should generally expose:
  - `GetCombo`
  - `GetAll`
  - `GetOne`
  - `Create`
  - `Update`
  - `Delete`
- Add feature-specific methods under a `Methods` region.
- Use soft delete by setting `DeletedAt = DateTime.UtcNow`.
- Set `UpdatedAt = DateTime.UtcNow` on updates.
- Use `AsNoTracking()` on read-only single-item projections.
- Apply filtering before ordering and pagination.
- **Do not apply pagination.** The business listings (products, orders, expenses) return the whole filtered set, matching the legacy screens, and extend `SortRequest`. Only the listings that already page — users and the audit trail — extend `PaginateRequest`.
- If pagination is ever requested for a listing, switch its request to `PaginateRequest` and use:

```csharp
.Skip((rq.Page - 1) * Pagination.DefaultPageSize)
.Take(Pagination.DefaultPageSize)
```

## Models And Data

- `<Library>/Models/*.cs` at the root holds **database tables only** — one class per table, plus the base classes their columns come from (`AuditableEntity`). If a class is not mapped to a table, it does not belong there.
- Every other model goes in `<Library>/Models/Internal/`, namespace `LaNata_Api.<Library>.Models.Internal`: service inputs and outputs, report row models, column definitions, formatting enums. "Internal" means the type only exists inside the process — it is neither a table nor an API contract. The classes are still `public`; the folder name is about scope of use, not the C# access modifier.
- So a library's `Models/` has four possible homes, and the pick is mechanical:
  - `Models/` — mapped to a table.
  - `Models/DTO/<Feature>/` — request/response for a controller.
  - `Models/Constants/` — reusable constants.
  - `Models/Internal/` — everything else.
- `Export/` and `Report/` own no tables, so their `Models/` root is empty by design. Do not add table-less classes back into it to make it look populated.
- Current tables: `Rol`, `User`, `Product`, `Order`, `ProductOrder`, `Expense`, `_Migrations`, plus the three in the `audit` schema.
- **Every entity carries explicit `[Table]` and `[Column]` attributes.** The database uses snake_case columns, Spanish table names (`Rol`) and one legacy typo (`Order.event_adress`); the C# side stays readable and the attribute is what bridges them. Never rely on EF conventions to guess a name here.
- **Money is `double`, not `decimal`.** Every price column is `float` in the production schema. `decimal` over `float` rounds silently on every write. This is the one place the code follows the database instead of good practice; fixing it means a column type migration nobody has authorised.
- Soft-deletable entities inherit from `AuditableEntity`, whose own properties carry `[Column]` attributes for the snake_case names.
- `ProductOrder` is the exception: it is a join table keyed by `(id_product, id_order, unit_price)` with no `deleted_at`, so it does **not** inherit `AuditableEntity` and the app hard deletes from it.
- `AuditableEntity` has:
  - `CreatedAt`
  - `UpdatedAt`
  - `DeletedAt`
  - `CreatedBy` / `UpdatedBy` / `DeletedBy` (nullable `User` ids)
  - `CreatedByUser` / `UpdatedByUser` / `DeletedByUser` navigations, mapped with `[ForeignKey]` to the matching `*By` column
- The `*At` / `*By` audit columns are written by `AuditInterceptor`, not by services.
- Use query filters in `APIContext` for soft-deleted entities.
- Entity `DbSet` names are singular, matching the reference style:
  - `Product`
  - `Order`
  - `ProductOrder`
  - `Expense`
- Use `[Key]` and `[ForeignKey]` attributes where the current style does.
- Money columns are `decimal` with `HasPrecision(18, 2)` configured in `APIContext`. The legacy used `float`; do not go back to it.
- Always use `DateTime.UtcNow` dates for DateTime properties.

## Domain Rules

These are the rules the legacy app enforced. Keep them when touching products or orders.

### Stock is per event date

- `Product.Stock` is the size of the catalogue, not what is free. Availability for a date is `Stock` minus everything the non-deleted orders of that same date reserve.
- `OrderService.GetAvailableStock(product, date, excludeOrderId)` is the single implementation. `excludeOrderId` leaves the order being edited out of the count so its own reservation is not read as somebody else's.
- Re-check availability on every write path: creating an order, adding a product to one, and moving an order to another date.
- `ProductService.GetStock` exposes the same calculation to the client. The legacy passed the available stock back from the browser as a query parameter and trusted it; never do that — the check belongs on the server.

### Order lines are keyed by price

- `ProductOrder`'s primary key is `(id_product, id_order, unit_price)`, so one order can hold the same product twice at different prices — the price a client was quoted before a change, plus the current one.
- `AddProduct` without `unitPrice` merges into (or opens) the line at the current catalogue price; with `unitPrice` it targets the line at that price.
- `RemoveProduct` takes the price as well: removing the whole reserved quantity **hard deletes** the row, anything less subtracts. There is no `deleted_at` on this table to soft delete into.
- Deleting an order only stamps `deleted_at` on the order. The lines stay, and the stock they reserved is released anyway because every availability query reaches them through `Order`, which carries the soft-delete filter.

### A deleted product still belongs to its old orders

- 40 of the 274 products are soft deleted, and 170 lines across 164 orders point at them.
- The global soft-delete filter turns `Include(x => x.Products).ThenInclude(x => x.Product)` into a join that **silently drops those lines**, under-reporting the line count and the order total. This is not hypothetical — it was a live bug caught against production data.
- `OrderService.LoadLineProducts` resolves an order's products with `IgnoreQueryFilters()`. Anything that reads the products behind order lines must go through it: `GetOne`, `Report`, `RemoveProduct` and `ValidateDateChange` all do.
- The rule generalises: **never navigate from a historical row to a soft-deletable master through an unfiltered `Include`.** Reading forward (from `Product` to its orders, or from `Order` to its lines) is fine and is exactly how the stock calculation excludes cancelled orders.

### Text normalization

- `Core/Helpers/TextHelper.cs` owns it, one method per legacy rule: `ToTitleCase` (client names), `ToUpperFirst` (descriptions, addresses, observations), `ToNumber` (phones).
- It strips anything outside letters/digits/spaces/apostrophes, exactly as the legacy regexes did, and uses the invariant culture because the stored text is compared and printed.
- Apply it in the service when building the entity, never in the controller.

### Dates carry a time component

- Every one of the 2200 existing orders stores its event day at **12:00** — the legacy added 12 hours to dodge a timezone problem in its date input. New rows keep the same time (`OrderService.NormalizeEventDate`) so the column stays homogeneous.
- **Every date filter is a half-open day range, never an equality check**: `x.Date >= day && x.Date < day.AddDays(1)`. An `x.Date == day` comparison matches nothing in production. The same applies to `DateTo` bounds, which use `< dateTo.AddDays(1)` rather than `<=`.
- Grouping by day goes through `x.Date.Date` so the time does not split a day into several buckets.
- New rows stamp `created_at` in UTC while the legacy wrote Argentina local time. The two eras differ by three hours on that column; it is not surfaced in the app, but do not mix them in a query without saying so.

## Helpers

- Helpers used by more than one library live in `Core/Helpers/`. Helpers specific to one library go in that library's `Helpers/` folder.
- `Core/Helpers/JsonHelper.cs` owns every JSON round-trip in the project, as extension methods:
  - `source.Clone()` — deep copy through serialization. Use it to detach a snapshot of a tracked entity before mutating it, so an update has something to compare against. References are preserved, so navigations and back-references survive the copy. Do not hand-roll per-entity copy constructors for this.
  - `source.Serialize()` / `json.Deserialize<T>()` — for persisting and reading JSON columns.
- Cloning and persisting use different serializer options on purpose: clones preserve references (`$id`/`$ref`) so graphs round-trip, while stored JSON drops nulls and collapses cycles so the output stays readable by the web and by SQL consumers. Do not merge the two option sets.
- The project uses System.Text.Json only. Do not add Newtonsoft.Json.

## Constants

- Put reusable constants in `<Library>/Models/Constants/`.
- Prefer static classes for constants.
- Do not hard-code repeated status strings in services.
- Company details printed on reports live in `Core/Models/Constants/CompanyInfo.cs`.

## User-Facing Messages

- Keep reusable Spanish messages in `Core/Models/Constants/Messages.cs`.
- Use proper Spanish accents and punctuation.
- Do not replace Spanish punctuation with ASCII-only text unless explicitly requested.
- Keep wording aligned with the legacy app where an equivalent message existed.
- Keep message construction consistent with the reference API:

```csharp
return entityName + " cread" + (femine ? "a" : "o") + " correctamente.";
```

## Database And Migrations

- Keep all migrations as SQL scripts.
- Do not add EF generated migration `.cs` files.
- All SQL scripts live in `LaNata-Api/Core/DAL/Migrations/`, regardless of which library owns the tables they touch. Do not add a `DAL/Migrations` folder to the other libraries.
- Migration names are ordered with numeric prefixes:
  - `01_Audit_Columns.sql`
  - `02_Audit_Schema.sql`
  - `03_Add_New_Feature.sql`
- **Migrations run against the live database, so every statement must be additive and idempotent.** Guard each one with `IF COL_LENGTH(...) IS NULL` or `IF NOT EXISTS`, so a rerun is a no-op. No `ALTER COLUMN`, no `DROP`, no rename, no `UPDATE` of existing rows.
- Rehearse anything non-trivial against a restored copy before it reaches `master`.
- SQL scripts are copied to output through the `.csproj` content rule.
- The `MigrationRunner` runs pending SQL scripts at startup when `Database:RunSqlMigrations` is `true`.
- Lookup seeding (audit categories and actions) is done via SQL scripts with `MERGE ... WHEN NOT MATCHED`. Do not seed data from C# code, and do not seed anything into the operational tables — they already hold live data.
- Executed SQL script filenames are stored in `_Migrations`.
- Use `_Migrations` to prevent rerunning scripts.
- Do not use `Database.Migrate()` for this project.
- Do not use EF migrations unless explicitly requested.
- **The API points at the live `LaNata` database**, the same one the legacy app uses. Back it up before running a new migration for the first time.

## Interceptors

- EF Core `SaveChangesInterceptor` implementations live under `<Library>/Helpers/Interceptors/`, in the library that owns the concern (`AuditInterceptor` lives in `Audit`).
- `Core/Helpers/Interceptors/InterceptorsContainer.cs` is the single registration point for every library's interceptors.
- Interceptors are resolved from DI so they can depend on services (`TokenService`, etc.):
  - `InterceptorsContainer.AddInterceptors(services)` registers them as scoped.
  - `InterceptorsContainer.ConfigureInterceptors(serviceProvider, optionsBuilder)` attaches them inside the `AddDbContext` configuration in `Core/DAL/DB/DatabaseConfig.cs`.
- Do not override `OnConfiguring` on the `DbContext` to register interceptors.
- When adding a new interceptor, register it in `AddInterceptors` and add it to the existing `optionsBuilder.AddInterceptors(...)` call in `ConfigureInterceptors`. `Program.cs` stays untouched.
- Interceptors keep the field names they touch as `private const string` constants declared at the top of the class.
- `AuditInterceptor` stamps `AuditableEntity` entries on save, taking the user id from `TokenService.TryGetToken()`:
  - `Added`: sets `CreatedBy`. `CreatedAt` comes from the property initializer.
  - `Modified` where `DeletedAt` went from `null` to a value (soft delete): sets `DeletedBy` only, leaving `UpdatedAt`/`UpdatedBy` untouched.
  - `Modified` where `DeletedAt` went back to `null` (restore): clears `DeletedBy` and then stamps as a normal update.
  - Any other `Modified`: sets `UpdatedAt = DateTime.UtcNow` and `UpdatedBy`.
  - `*By` columns are only written when there is an authenticated user; background/startup saves leave them as-is.

## Audit Trail

The `Audit` library has two independent halves. Do not mix them up.

### Column stamping (automatic)

- `AuditableEntity` + `AuditInterceptor`, described under [Interceptors](#interceptors). Every save stamps `CreatedBy` / `UpdatedAt` / `UpdatedBy` / `DeletedBy` with no code at the call site.
- This answers *who last touched the row*. It keeps no history.

### Audit entries (explicit)

- The `audit` schema keeps the history: `audit.AuditEntry`, plus the `audit.AuditAction` and `audit.AuditCategory` lookups.
- Nothing is recorded automatically. A service records an entry by calling `AuditService.Audit(...)`, and only the fields it registers are compared or stored.
- `AuditService.Audit` never throws: it validates the request, returns a `BaseResponse` error on failure, and must not be allowed to break the operation that triggered it.
- The write uses its own `APIContext` built from the injected `DbContextOptions<APIContext>`, so calling `Audit(...)` never flushes the caller's pending changes. The flip side is that it commits on its own connection: an audit entry written inside a transaction that later rolls back still persists. Call `Audit(...)` after the business `SaveChangesAsync`.
- Adding an action code means adding the constant in `AuditActionCode` **and** seeding the row in a SQL migration — `AuditEntry.ActionCode` has a foreign key to `audit.AuditAction`. Entity codes in `AuditEntityCode` are free strings and need no migration.
- `audit.AuditEntry.UserId` is deliberately **not** a foreign key to `dbo.User`, so audit history survives user deletion and audit writes never take locks on operational tables. `UserName` / `UserEmail` are denormalized copies taken from the token.

### Building the change summary

- `AuditChangeList` is the fluent builder. `For(previous, updated)` for updates, `ForCreated(entity)`, `ForDeleted(entity)`.
- `AddField` for scalars, `AddLookup` when the stored value is a code or foreign key and the resolver supplies the readable text, `AddCollection` to report added/removed items by key.
- `Build()` returns an `AuditChangeSet` carrying `Changes` (the diff, with Spanish descriptions), `State` (before snapshot) and `Payload` (after snapshot). Snapshots only contain registered fields — this is what keeps secrets and irrelevant columns out of the audit table.
- Follow the shape in `Core/Services/ProductService.cs`: an `#region Audit` with one private method declaring the audited fields, reused by create, update and delete, plus `entity.Clone()` before mutation so the update has something to compare against.
- Order lines are the deliberate exception: `AddProduct` and `RemoveProduct` write their own `PRODUCT_ADDED` / `PRODUCT_REMOVED` entries with an explicit `Payload` (product, quantity, unit price), which reads better than a collection diff on the order.
- Audit text lives in `Audit/Models/Constants/AuditMessages.cs`, not in the shared `Messages` catalog, so the library stays self-contained.
- `AuditFormatter` renders values with the invariant culture on purpose: the text is stored and compared, so it must not depend on the host's culture.

## Documents (Export And Report)

Both libraries follow the audit rule: nothing is included implicitly, the calling service declares its columns.

### Export — CSV and XLSX

- `ExportService` takes an in-memory collection and returns an `ExportFile` (name, content type, bytes). It never writes to disk; the caller decides what to do with the bytes.
- Declare columns with `ExportColumns.For<T>()`, then `.Add(x => x.Field, "Encabezado", ExportValueFormat.Currency)`. `AddIf` skips a column for roles that must not see it.
- `ExportValueFormat` drives both outputs: in CSV it picks the text, in XLSX it also picks the **cell type**, so numbers and dates stay sortable in the spreadsheet instead of arriving as text. Use `Text` for codes that must keep leading zeros — phone numbers use it.
- CSV is written as UTF-8 **with BOM** — without it Excel falls back to the system codepage and mangles accents. The default delimiter is `;`, which is what Excel expects in Spanish locales.
- CSV values starting with `=`, `+`, `-` or `@` are prefixed with a quote. This is a CSV-injection guard: without it, exported user input runs as a formula when the file is opened. Do not remove it.
- `ToExcelWorkbook` builds multi-sheet workbooks. CSV has no equivalent.
- Exports are capped at `ExportLimits.MaxRows`; past that the request is rejected instead of building a huge file in memory.

### Report — PDF

- Built on QuestPDF. `ReportConfig.ConfigureReporting` sets the license from `Reporting:License` and must run in `Program.cs` before the first render, or QuestPDF throws.
- **QuestPDF is dual-licensed.** The Community license is free only below the vendor's revenue threshold; above it a paid license is required. Check this before shipping.
- Two ways in:
  - `ReportService.GenerateTable(...)` for anything a table can express — filters, summary cards, table and record count, with no QuestPDF code at the call site. Columns come from `ReportColumns.For<T>()`. The product price list and both order details use this.
  - Inherit `ReportDocumentBase` and pass it to `ReportService.Generate(...)` when the layout needs more. The base owns page setup, header and footer; the subclass only implements `ComposeContent`. `Core/Helpers/Documents/BalanceReportDocument.cs` is the only one, because it stacks two tables in one document.
- Feature-specific documents live in `Core/Helpers/Documents/`, next to the library that owns the feature. The generic ones stay in `Report/Helpers/Documents/`.
- `ReportService.Merge` renders several documents into one PDF for batch downloads.
- Reuse the components in `Report/Helpers/Components/` rather than composing raw QuestPDF in a report. Colors and spacing come from `ReportColors` and `ReportDimensions`, never hardcoded.
- Reports are capped at `ReportLimits.MaxRows`, lower than the export cap because PDF layout is far more expensive. Past that, an export is the right tool.
- The order detail prints with or without prices from one column set: `ReportColumns.AddIf(withPrice, ...)` drops the price columns. The shipment is rendered as one more row through `OrderReportRow`, the way the legacy view did it. Do not fork the document into two.

### Controllers

- File endpoints are the one exception to "controllers return `BaseResponse`": they return `IActionResult`, with `File(...)` on success and `BadRequest(rs)` carrying the usual error shape on failure. See `Core/Controllers/ProductController.cs`.
- Documents must cover the whole filtered set. Reuse the listing's filter and order helpers, as `ProductService.GetProductsForDocument` does.
- The API must expose `Content-Disposition` through CORS (`CorsConfig`), otherwise a browser on another origin cannot read the file name.

### Auditing a generated file

- Every successful export or report is audited: knowing who took which data out of the system is the point of having an audit trail.
- Use `AuditActionCode.EXPORTED` for CSV/XLSX and `AuditActionCode.REPORT_GENERATED` for PDF. Both live in the `DOCUMENT` category.
- A file has no before/after to diff, so leave `Changes` empty and put the facts in `Payload`: file name, format, row count and the applied filters. `EntityKey` stays null because the entry covers a set, not one record. See `ProductService.AuditDocument`.
- Audit after the file is generated and only when it succeeded, so a failed generation leaves no misleading entry.

## Transactions

- Only use transactions when needed.
- Do not wrap read-only operations in transactions.
- Do not add transactions for simple single-entity saves unless there is a real consistency requirement.
- Creating an order with its lines is a single `SaveChangesAsync`, which EF already makes atomic. It needs no explicit transaction.
- If no entities or database state are modified, no transaction is needed.

## Configuration

- Main config is `LaNata-Api/appsettings.json`.
- Development overrides go in `LaNata-Api/appsettings.Development.json`.
- Do not commit real secrets.
- Use placeholders for defaults.
- Do not add Swagger/OpenAPI. The project intentionally exposes no API schema endpoint.
- CORS is restricted to the origins listed in `Cors:AllowedOrigins`; do not reintroduce a permissive `AllowAnyOrigin` policy.

## Documentation

- Keep root `README.md` up to date when adding features, endpoints or config keys.
- Keep `CLAUDE.md` updated when project-wide conventions change.
