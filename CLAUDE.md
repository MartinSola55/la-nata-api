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
  - `Export` — XLSX generation.
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

- Use custom `User` and `Role` entities. The table was renamed from `Rol` to `Role` by migration `04`.
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
- Current roles, matching the text already in the `Role` table:
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
- There is no paginated request type. Every listing request extends `SortRequest` and nothing else.

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
- Apply filtering before ordering.
- **No listing paginates. Never add `.Skip()` / `.Take()` to one.** Every `GetAll` returns the whole filtered set, the way the legacy screens showed it, and the client renders all of it. Users and the audit trail used to page and no longer do.
- Growth is bounded by **filters**, not by a page size: the audit trail is the only table that grows without an upper limit, and its screen defaults to the current month for exactly that reason. If a listing ever gets slow, tighten its default filter rather than reintroducing paging.

## Models And Data

- `<Library>/Models/*.cs` at the root holds **database tables only** — one class per table, plus the base classes their columns come from (`AuditableEntity`). If a class is not mapped to a table, it does not belong there.
- Every other model goes in `<Library>/Models/Internal/`, namespace `LaNata_Api.<Library>.Models.Internal`: service inputs and outputs, report row models, column definitions, formatting enums. "Internal" means the type only exists inside the process — it is neither a table nor an API contract. The classes are still `public`; the folder name is about scope of use, not the C# access modifier.
- So a library's `Models/` has four possible homes, and the pick is mechanical:
  - `Models/` — mapped to a table.
  - `Models/DTO/<Feature>/` — request/response for a controller.
  - `Models/Constants/` — reusable constants.
  - `Models/Internal/` — everything else.
- `Export/` and `Report/` own no tables, so their `Models/` root is empty by design. Do not add table-less classes back into it to make it look populated.
- Current tables: `Role`, `User`, `Category`, `Product`, `ProductVariant`, `ProductPriceHistory`, `Order`, `ProductOrder`, `Expense`, `_Migrations`, plus the three in the `audit` schema.
- **Entities carry `[Table]` but no `[Column]`.** Migration `04_Rename_Columns_To_Model_Names.sql` renamed every column to PascalCase and `Rol` to `Role`, including the legacy typo `event_adress` → `EventAddress`, so EF conventions now match the database. Do not add `[Column]` attributes back, and do not assume snake_case anywhere.
- **Money is `decimal` with `[Precision(18, 2)]` on the property.** Migration `05` converted the old `float` columns to `DECIMAL(18, 2)`; do not go back to `double`.
- Soft-deletable entities inherit from `AuditableEntity`.
- Two entities deliberately do not:
  - `ProductOrder`, an order line with no `deleted_at`, which the app hard deletes from.
  - `ProductPriceHistory`, an append-only log that is never edited or removed.
- `AuditableEntity` has:
  - `CreatedAt`
  - `UpdatedAt`
  - `DeletedAt`
  - `CreatedBy` / `UpdatedBy` / `DeletedBy` (nullable `User` ids)
  - `CreatedByUser` / `UpdatedByUser` / `DeletedByUser` navigations, mapped with `[ForeignKey]` to the matching `*By` column
- The `*At` / `*By` audit columns are written by `AuditInterceptor`, not by services.
- Use query filters in `APIContext` for soft-deleted entities.
- Entity `DbSet` names are singular, matching the reference style:
  - `Category`
  - `Product`
  - `ProductVariant`
  - `Order`
  - `ProductOrder`
  - `Expense`
- Use `[Key]` and `[ForeignKey]` attributes where the current style does.
- Always use `DateTime.UtcNow` dates for DateTime properties.

## Domain Rules

These are the rules the legacy app enforced. Keep them when touching products or orders.

### Stock is per event date, and there are two ceilings

- `Product.Stock` is the size of the catalogue, not what is free. Availability for a date is `Stock` minus everything the non-deleted orders of that same date reserve.
- **A variant has its own stock, and a line that picks one consumes both.** 100 chairs with 80 white covers means 80 lines can take the white cover and the remaining 20 must take another variant or none — never 101 chairs, never 81 white covers.
- `OrderService.GetReservations(productIds, variantIds, date, excludeOrderId)` is the single implementation. It answers in **two round trips whatever the order holds**, not one query per product, and `excludeOrderId` leaves the order being edited out of the count so its own reservation is not read as somebody else's. `BuildOrderProducts` then applies both ceilings in memory: the product one sums every line of it whatever variant they picked, the variant one only the lines on that variant.
- Re-check availability on every write path: creating an order, editing one, and moving an order to another date.
- `ProductService.GetStock` exposes the same calculation to the client, product and variants together. The legacy passed the available stock back from the browser as a query parameter and trusted it; never do that — the check belongs on the server.
- **The catalogue side is guarded too.** Every availability check reads `Stock` forward, so lowering it under what the upcoming events already book cancels nothing — it just leaves those orders quietly oversold and `GetStock` answering a negative availability. `ProductService.ValidateStockCoverage` runs inside `Product.Update` and refuses a reduction that an upcoming event day no longer fits, naming the day and the quantity. Product and variant are checked separately, product first: the whole product not fitting is not the variant's fault, and naming the variant sends the user to change the wrong number.
- Only a **reduction** is checked. A catalogue row that is already oversold — legacy data, or a stock lowered before this guard existed — must still be correctable upwards, and blocking every edit of it would make that impossible.
- **A product booked by an upcoming event cannot be retired.** `Product.Delete` refuses it, the way `ReconcileVariants` refuses to retire a variant in use: the lines keep pointing at the product, and every order write path resolves its products through the soft-delete filter, so retiring one leaves that order unable to be saved again. A **past** order holding a retired product is fine and is what `LoadLineProducts` exists for; what is forbidden is creating that situation for an order still ahead.

### Order lines are keyed by product, price and variant

- `ProductOrder` has a surrogate `Id` primary key plus a **unique index** on `(ProductId, OrderId, UnitPrice, VariantId)`. So one order can hold the same product twice at different prices — the price a client was quoted before a change, plus the current one — and also twice at the same price with different variants.
- `VariantId` is nullable, which is why it could not stay in a composite primary key: migration `09` rebuilt the key precisely for this. SQL Server treats NULLs as **equal** inside a unique index, and that is what still limits an order to one "sin variante" line per product and price. Do not "fix" that into a filtered index.
- There is a single write path: `Order.Create` and `Order.Update` take the whole line set. `Update` reconciles by `(ProductId, UnitPrice, VariantId)` — quantity-patched when matched, hard deleted when not, inserted for the leftovers.
- `BuildOrderProducts` resolves the unit price as `product.Price + (variant.PriceExtra ?? 0)` unless the caller supplies one explicitly (`Update` does, `Create` does not).
- Deleting an order only stamps `deleted_at` on the order. The lines stay, and the stock they reserved is released anyway because every availability query reaches them through `Order`, which carries the soft-delete filter.

### Moving a price follows through to the open orders

- `ProductService.RepriceOpenOrders` runs inside `Product.Update`, in the same transaction as the product save and the `ProductPriceHistory` rows, so the log can never claim a re-pricing that rolled back.
- It fires whenever the line's effective price **moves**, in **either direction**, and only touches lines whose order is not deleted, **not paid** (`Order.PaidAt == null`) and whose **event has not happened** (`Order.Date >= today`). A rise is the case that pays for the mechanism, but a correction downwards has to reach the same lines or the client ends up billed at a price the catalogue no longer holds. A line at any price other than the exact one this product used to charge is a quote somebody chose to hold and is left frozen.
- A variant line was quoted at `previousPrice + previousPriceExtra` and moves to `newPrice + newPriceExtra`. Both surcharge versions must be kept while the update runs; using the already-updated surcharge to identify the old line leaves it behind. Grouping and collision handling are by `(OrderId, VariantId)`.
- **Collisions merge**: when the order already holds a line at the target price, the quantities are summed and the stale row is hard deleted. Over `ValidationLimits.OrderQuantityMax` both rows are left untouched and `SkippedLines` is incremented — half a merge would lose quantity with the row it deletes.
- `ProductOrder` has no `deleted_at`, so this rewrites rows with no undo. The predicate is the whole safety of the operation; rehearse any change to it against a restored copy.
- **Every movement leaves a `ProductPriceHistory` row, and a variant surcharge is a movement of its own.** `BuildPriceHistory` writes one row for the product when its `Price` or `BreakPrice` changed, plus one per existing variant whose `PriceExtra` changed — a variant-only change re-prices real order lines, so leaving it out of the log made the history screen show nothing while the orders had already moved. A variant row carries `VariantId` and its own surcharge on both sides, which is what the screen shows for it; `PreviousPrice`/`NewPrice` keep the product price of the moment so the effective price stays reconstructible. `VariantId` null is the product's own change, which is what every row written before migration `11` is.
- `RepriceSummary` keeps the re-priced counts **split by variant**, so each row reports the lines its own change caused instead of every row repeating the same total. `Track` accumulates while re-pricing; `For` is the read-only lookup used when the rows are built.
- Reading the log resolves variant names through `LoadVariantNames`, with `IgnoreQueryFilters()`. It is a historical row pointing at a soft-deletable master — the same rule as the variants behind an order line.

### A draft reserves nothing until it is confirmed

- `Order.IsDraft` is a quote waiting for the client to accept it. The row holds everything a real order holds, but **every availability query skips it**: `OrderService.GetReservations`, `ProductService.GetStock` and `ProductService.ValidateStockCoverage` all filter `!x.Order.IsDraft`. Adding a new place that counts reservations means adding that predicate too.
- `Create` takes `IsDraft` from the request and passes `validateStock: !order.IsDraft` to `BuildOrderProducts`; `Update` derives the same flag from the stored order. **`Update` never changes `IsDraft`** — a quote becomes an order only through `Confirm`, which is the path that checks the stock.
- `OrderService.Confirm` is the single write path for the flag, and the only moment a draft's lines are measured. `GetMissingStock` reports **every** shortage at once — product ceiling and variant ceiling, each as its own row with requested vs available — because sending the user back one product per attempt is the re-quoting pain the flag exists to remove. The list travels in `Data` alongside the error, which `BaseResponse<T>.SetError` allows since it returns `this`.
- There is no un-confirm. A confirmed order that falls through is deleted like any other; adding a way back would silently release stock somebody else may already have booked.
- A draft is **excluded from the balance**, billed and collected alike, and its PDF prints as `PRESUPUESTO`. It is **not** excluded from `RepriceOpenOrders`: an unaccepted quote should follow the catalogue rather than hold a price nobody agreed to yet.
- Drafts still count for `Product.Delete` and `ReconcileVariants`. They reserve nothing, but retiring a product or variant a draft holds would leave that draft impossible to confirm — the same dead end by another route.

### Payment state freezes an order

- `Order.PaidAt` / `PaidBy` are written by `OrderService.MarkAsPaid` / `MarkAsUnpaid`, not by `AuditInterceptor`, which only stamps `AuditableEntity` columns.
- A paid order stays **editable** on purpose: fixing a typo should not require cancelling the charge.
- The balance reports `TotalIncome` (billed, unchanged in meaning) alongside `TotalIncomePaid` (collected). Profit still measures against the billed figure.

### A deleted product still belongs to its old orders

- 40 of the 274 products are soft deleted, and 170 lines across 164 orders point at them.
- The global soft-delete filter turns `Include(x => x.Products).ThenInclude(x => x.Product)` into a join that **silently drops those lines**, under-reporting the line count and the order total. This is not hypothetical — it was a live bug caught against production data.
- `OrderService.LoadLineProducts` resolves an order's products with `IgnoreQueryFilters()`, and `LoadLineVariants` does the same for variants. Anything that reads the products or variants behind order lines must go through them: `GetOne`, `Report` and the audit snapshot in `Update` all do. `BuildOrderProducts` loads the requested variants the same way, or re-saving an order that books a retired variant would be impossible.
- The rule generalises: **never navigate from a historical row to a soft-deletable master through an unfiltered `Include`.** Reading forward (from `Product` to its orders, or from `Order` to its lines) is fine and is exactly how the stock calculation excludes cancelled orders.

### Text normalization

- There is **no** `TextHelper`. Services trim the incoming strings when building the entity (`rq.Description?.Trim()`) and nothing else.
- If the legacy casing rules (title case for client names, upper-first for descriptions) are ever wanted back, add the helper under `Core/Helpers/` and apply it in the service, never in the controller.

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
- **Numeric bounds live in `ValidationLimits` and in the web, never in the database.** Migration `10_Drop_Numeric_Check_Constraints.sql` removed every numeric `CHECK`, so a new migration must not add one back. The reason is inflation: a ceiling in pesos ages, and moving one that lives in a constraint costs a drop-and-recreate against the live database, a rehearsal and a window — for what is really a typo guard. In code it is a one-line edit and a deploy.
- The two remaining places must carry the same number: `ValidationLimits` and the matching `*_MAX` constant in the web. The web clamps the input, the API rejects whatever still gets through.
- **Keep the money bounds generous.** They exist to catch a slipped keystroke, not to express what La Ñata charges — nothing in four years of real data comes within two orders of magnitude of any of them. Tightening one to today's prices is how it ends up needing a change every couple of years.
- Length constraints stay in the database: a column width is not affected by inflation and it backstops the `NVARCHAR` size. Add those in the same migration that creates the column, matching the `*MaxLength` constant.
- What was given up by dropping the numeric constraints is the **floor**, not the ceiling: those constraints combined `> 0` with `<= max`, and the `> 0` half is what kept a negative amount out of a column. A floor never ages, so if it is ever wanted back it belongs in a constraint of its own with no upper bound — that one would never need another migration.

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
- Migration names are ordered with numeric prefixes. The current sequence:
  - `01_Audit_Columns.sql` — audit columns and their FKs on `User`, `Product`, `Order`, `Expense`
  - `02_Audit_Schema.sql` — the `audit` schema, its three tables, and the lookup seeds
  - `03_Product_BreakPrice_Nullable.sql`
  - `04_Rename_Columns_To_Model_Names.sql` — snake_case → PascalCase, `Rol` → `Role`
  - `05_Data_Types_And_Validation_Constraints.sql` — `float` → `DECIMAL(18,2)`, CHECK constraints, first `PK_ProductOrder` rebuild
  - `06_Remove_Order_Product_Audit_Actions.sql`
  - `07_Category.sql` — the `Category` table and `Product.CategoryId`
  - `08_Order_Payment_And_Price_History.sql` — `Order.PaidAt`/`PaidBy`, `ProductPriceHistory`, three audit action seeds
  - `09_Product_Variants.sql` — `ProductVariant`, `ProductOrder.VariantId`, and the surrogate-key rebuild
  - `10_Drop_Numeric_Check_Constraints.sql` — removes every numeric `CHECK`; the bounds move to `ValidationLimits` alone
  - `11_Price_History_Variants.sql` — `ProductPriceHistory.VariantId`/`PreviousPriceExtra`/`NewPriceExtra`, so a variant surcharge change gets its own row
  - `12_Order_Draft.sql` — `Order.IsDraft` and the `ORDER_CONFIRMED` audit action seed
- **Migrations run against the live database, so every statement must be additive and idempotent.** Guard each one with `IF COL_LENGTH(...) IS NULL` or `IF NOT EXISTS`, so a rerun is a no-op. No `ALTER COLUMN`, no `DROP`, no rename, no `UPDATE` of existing rows.
- Migrations `04`, `05`, `09` and `10` break that rule on purpose, each after weighing the alternative. `10` is the cheap kind: a `CHECK` is metadata, so dropping one rewrites no data, takes only a brief schema lock, and can never invalidate an existing row. `09` is the one to be careful with: it rebuilds `PK_ProductOrder` on a table holding every order line ever written. If a change ever looks like it needs the same, raise it rather than writing it, and when it does go ahead: full backup, rehearsal on a restored copy, and preferably a manual run in a maintenance window with `Database:RunSqlMigrations` set to `false`.
- **`MigrationRunner` sends each file as a single batch** (`ExecuteSqlRawAsync`, no `GO`), and T-SQL resolves column names of an already existing table at compile time. A statement that names a column added earlier **in the same file** fails with "Invalid column name" and rolls the whole file back. Wrap those in `EXEC sys.sp_executesql N'...'` — `05`, `07`, `08` and `09` all do.
- **Comparing an identifier against a catalog view needs an explicit collation on both sides.** The database is `Modern_Spanish_CI_AS` but `sys.*` carries the catalog collation (`SQL_Latin1_General_CP1_CI_AS` on Azure SQL), so `sys.check_constraints.name = @variable` or `= t.SomeColumn` raises "Cannot resolve the collation conflict" and rolls the whole file back. Force `COLLATE Latin1_General_BIN2` on both sides, as `04` and `10` do — a binary collation is also the right semantics for an identifier. A comparison against a literal is fine and needs nothing.
- Rehearse anything non-trivial against a restored copy before it reaches `master`.
- SQL scripts are copied to output through the `.csproj` content rule.
- The `MigrationRunner` runs pending SQL scripts at startup when `Database:RunSqlMigrations` is `true`.
- Lookup seeding (audit categories and actions) is done via SQL scripts with `MERGE ... WHEN NOT MATCHED`. Do not seed data from C# code, and do not seed anything into the operational tables — they already hold live data.
- Executed SQL script filenames are stored in `_Migrations`.
- Use `_Migrations` to prevent rerunning scripts.
- Do not use `Database.Migrate()` for this project.
- Do not use EF migrations unless explicitly requested.

### One-off maintenance scripts

- `Core/DAL/Scripts/` holds SQL that must **not** run automatically: anything that `UPDATE`s existing business rows. `MigrationRunner` only reads `Core/DAL/Migrations/`, so a file here never fires on a deploy, and the `.csproj` does not copy it to the output either — it is run by a person, from the repo, against a database they just backed up.
- That is the line between the two folders: a migration changes the **shape** of the database and every environment needs it; a script changes **data** once, deliberately, and is a judgement call somebody has to make.
- Write them idempotent anyway, so a second run is a no-op, and have them `PRINT` the counts before and after. `Product_BreakPrice_Zero_To_Null.sql` is the reference.
- The exception already in the tree is `06_Remove_Order_Product_Audit_Actions.sql`, which does `UPDATE` audit rows inside a migration. That was allowed because the rows had to move before a foreign key could hold; do not read it as licence for data fixes in general.
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
- Current action codes: `CREATED`, `UPDATED`, `DELETED`, `ORDER_CONFIRMED`, `ORDER_PAID`, `ORDER_UNPAID`, `LOGIN`, `LOGIN_FAILED`, `PASSWORD_CHANGED`, `EXPORTED`, `REPORT_GENERATED`.
- `audit.AuditEntry.UserId` is deliberately **not** a foreign key to `dbo.User`, so audit history survives user deletion and audit writes never take locks on operational tables. `UserName` / `UserEmail` are denormalized copies taken from the token.

### Building the change summary

- `AuditChangeList` is the fluent builder. `For(previous, updated)` for updates, `ForCreated(entity)`, `ForDeleted(entity)`.
- `AddField` for scalars, `AddLookup` when the stored value is a code or foreign key and the resolver supplies the readable text, `AddCollection` to report added/removed items by key.
- `Build()` returns an `AuditChangeSet` carrying `Changes` (the diff, with Spanish descriptions), `State` (before snapshot) and `Payload` (after snapshot). Snapshots only contain registered fields — this is what keeps secrets and irrelevant columns out of the audit table.
- Follow the shape in `Core/Services/ProductService.cs`: an `#region Audit` with one private method declaring the audited fields, reused by create, update and delete, plus `entity.Clone()` before mutation so the update has something to compare against.
- Order lines are the deliberate exception: `BuildCreateAuditChanges` stores the complete initial line snapshot in `Payload`, while `BuildUpdateAuditChanges` stores full before/after line snapshots (product, variant, quantity, unit price, subtotal) in `State` / `Payload` and adds one synthetic change entry, rather than a collection diff. `HaveSameProductLines` compares `(ProductId, Quantity, UnitPrice, VariantId)` — dropping `VariantId` there would make a variant swap vanish from the trail.
- Product variants are the other exception, for the same reason: they are rows of their own table. `ProductService.AddVariantAudit` snapshots both sides into `State` / `Payload` under `Variants` and adds one change entry per variant added, removed or edited, keyed on the variant id and carrying the whole variant before and after. `Update` has to take the before snapshot right after `entity.Clone()` and before `ReconcileVariants`, which edits the tracked variants in place; `Delete` includes them, since nothing lazy-loads. A removed variant stays in `Product.Variants` with `DeletedAt` set, so both sides are filtered through `LiveVariants`.
- A product update always writes one `UPDATED` entry. That entry combines the product fields, variant changes and, under `Payload.Repricing`, the re-priced line counts and affected order ids (capped), rather than writing a second entry for the price propagation.
- Audit text lives in `Audit/Models/Constants/AuditMessages.cs`, not in the shared `Messages` catalog, so the library stays self-contained.
- `AuditFormatter` renders values with the invariant culture on purpose: the text is stored and compared, so it must not depend on the host's culture.

## Documents (Export And Report)

Both libraries follow the audit rule: nothing is included implicitly, the calling service declares its columns.

### Export — XLSX

- `ExportService` takes an in-memory collection and returns an `ExportFile` (name, content type, bytes). It never writes to disk; the caller decides what to do with the bytes.
- Declare columns with `ExportColumns.For<T>()`, then `.Add(x => x.Field, "Encabezado", ExportValueFormat.Currency)`. `AddIf` skips a column for roles that must not see it.
- `ExportValueFormat` picks the **cell type**, so numbers and dates stay sortable in the spreadsheet instead of arriving as text. Use `Text` for codes that must keep leading zeros — phone numbers use it.
- `ToExcelWorkbook` builds multi-sheet workbooks.
- There is no CSV writer: `ExportFileFormat` has a single member, `Excel`. If CSV is ever wanted back, it needs a UTF-8 BOM so Excel does not mangle accents, `;` as the delimiter for Spanish locales, and a quote prefix on values starting with `=`, `+`, `-` or `@` so exported user input cannot run as a formula.
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
- `ReportOptions.Note` prints a boxed remark under the table, for a commercial clause the document has to carry. The quote uses it for its validity: `CompanyInfo.BudgetValidityDays` (15) days from issue. The note spells out the deadline date rather than saying "desde su emisión" alone, because the detail documents override `FooterNote` with the address and so never print the generation stamp the client would count from.

### Controllers

- File endpoints are the one exception to "controllers return `BaseResponse`": they return `IActionResult`, with `File(...)` on success and `BadRequest(rs)` carrying the usual error shape on failure. See `Core/Controllers/ProductController.cs`.
- Documents must cover the whole filtered set. Reuse the listing's filter and order helpers, as `ProductService.GetProductsForDocument` does.
- The API must expose `Content-Disposition` through CORS (`CorsConfig`), otherwise a browser on another origin cannot read the file name.

### Auditing a generated file

- Every successful export or report is audited: knowing who took which data out of the system is the point of having an audit trail.
- Use `AuditActionCode.EXPORTED` for XLSX and `AuditActionCode.REPORT_GENERATED` for PDF. Both live in the `DOCUMENT` category.
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
