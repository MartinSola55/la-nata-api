# La Ñata API

.NET 10 Web API for **La Ñata** — rental of chairs, tables, tableware, linen and furniture.

This is the backend rewrite of the legacy ASP.NET MVC 5 application (`La_Nata`). It keeps the same business rules and the same Spanish user-facing messages, but exposes them as a JSON API with JWT authentication, an audit trail, and generated PDF/XLSX documents instead of Razor print views. The web frontend lives in the separate `la-nata-web` repository.

**It runs against the existing production database.** The schema is not ours to redesign: the entities map onto the legacy tables through `[Table]` and `[Column]` attributes, and the only DDL the API ever runs is additive — the audit columns and a separate `audit` schema. Nothing existing is altered, renamed or dropped.

## Stack

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core with SQL Server
- JWT bearer authentication
- SQL files for migrations
- QuestPDF for reports, SpreadCheetah for spreadsheets

## Project Structure

The project is grouped into libraries by business area. They are folders inside the single `LaNata-Api` project, and namespaces mirror them (`LaNata_Api.<Library>.<Layer>`).

```text
LaNata-Api/
  Core/                 Products, orders, expenses, dashboard, users, roles, auth, DbContext, shared DTOs/constants.
  Audit/                Audit trail: AuditableEntity, AuditInterceptor and the audit schema.
  Export/               XLSX generation.
  Report/               PDF generation.
  Program.cs            Composition root and middleware pipeline.
  appsettings.json
```

Every library repeats the same inner layout, creating only the folders it needs:

```text
<Library>/
  Controllers/          HTTP endpoints. Controllers delegate to services.
  DAL/
    DB/                 EF Core DbContext and DatabaseConfig.
    Filters/            Global query filters.
  Helpers/              Generic helper classes.
    Documents/          Custom QuestPDF documents for this library's features.
    Interceptors/       EF Core SaveChanges interceptors.
  Models/               Database entities only — one class per table.
    Constants/          Reusable constants.
    DTO/                Controller request and response contracts.
    Internal/           Everything else: service inputs and outputs, report rows, enums.
  Security/
    Config/             Startup configuration modules (CORS, authentication, authorization, rate limiting, security headers).
  Services/             Business logic.
```

All SQL migrations live together in `Core/DAL/Migrations`, whichever library owns the tables they touch.

## Domain

Migration `04` renamed every column to PascalCase and `Rol` to `Role`, so the C# names and the columns now match and no `[Column]` attributes are needed:

| Entity | Table | Notes |
|---|---|---|
| `Category` | `Category` | `Description`; groups the catalogue so the price list can be printed per rubro |
| `Product` | `Product` | `Description`, `Stock`, `Price`, `BreakPrice`, `CategoryId` |
| `ProductVariant` | `ProductVariant` | `ProductId`, `Description`, `Stock`, `PriceExtra` — a variant with its own stock |
| `ProductPriceHistory` | `ProductPriceHistory` | append-only log of price changes, product and variant, and what each one did to the open orders |
| `Order` | `Order` | `ClientName`, `Phone`, `EventAddress`, `Date`, `ShipmentPrice`, `Observation`, `IsDraft`, `PaidAt`, `PaidBy` |
| `ProductOrder` | `ProductOrder` | order line; surrogate `Id`, unique on `(ProductId, OrderId, UnitPrice, VariantId)` |
| `Expense` | `Expense` | `Date`, `Description`, `Price` |
| `Role` | `Role` | two rows: `Administrador` and `Empleado` |
| `User` | `User` | `Email`, `PasswordHash`, `RoleId` — there are no name columns |

`Category`, `Product`, `ProductVariant`, `Order`, `Expense` and `User` inherit `AuditableEntity`, so every row is soft deleted and carries who created, edited and deleted it. Two do not: `ProductOrder`, which the app hard deletes from exactly as the legacy did, and `ProductPriceHistory`, which is never edited or removed.

### Money is `decimal(18, 2)`

The legacy columns were `float`. Migration `05` converted them to `DECIMAL(18, 2)` and the entities carry `[Precision(18, 2)]` on every money property. Do not go back to `double`.

### Event dates carry a time

The legacy stored every event day at **12:00**, an artefact of adding 12 hours to dodge a timezone problem in its date input. All 2200 existing rows have it, so new rows keep the same time and the column stays homogeneous.

The consequence matters more than the cause: **every date filter is a half-open day range**, never an equality check. `x.Date >= day && x.Date < day.AddDays(1)`.

### Deleted products still belong to old orders

40 of the 274 products are soft deleted, and 170 order lines across 164 orders point at them. The global soft-delete query filter turns a naive `Include(x => x.Products).ThenInclude(x => x.Product)` into a join that **drops those lines**, under-reporting both the line count and the order total.

`OrderService.LoadLineProducts` is the answer: it resolves the products behind an order's lines with `IgnoreQueryFilters()`, and `LoadLineVariants` does the same for variants. Anything that reads an order's lines has to go through them — `GetOne`, `Report` and the audit snapshot in `Update` all do. `BuildOrderProducts` loads the requested variants the same way, or re-saving an order that books a retired variant would be impossible.

### Stock is per event date, and there are two ceilings

`Product.Stock` is the size of the catalogue, not what is free. What matters is availability **on the day of an event**: the stock minus everything the orders of that same date already reserve.

A **variant** has its own stock on top of that, and a line that picks one consumes both. 100 chairs with 80 white covers means:

- 80 lines can take the white cover, and the 81st is refused on the *variant* ceiling;
- the remaining 20 can take another variant or none, and the 101st is refused on the *product* ceiling.

`POST /api/Product/GetStock` answers both at once:

```json
{ "date": "2026-09-15" }
```

```json
{ "id": 1, "description": "Silla plastica blanca",
  "stock": 100, "reservedStock": 50, "availableStock": 50,
  "variants": [
    { "id": 7, "description": "Funda blanca", "priceExtra": 200,
      "stock": 80, "reservedStock": 40, "availableStock": 40 }
  ] }
```

Pass `excludeOrderId` while editing an order so its own reservation is not counted against it.

`OrderService.GetReservations` resolves both dictionaries in **two round trips** whatever the order holds, and `BuildOrderProducts` applies the two ceilings in memory. Every write path re-checks availability server-side: creating an order, editing one, and moving an order to another date.

The catalogue side is guarded too, because every availability check reads `Stock` forward: lowering it under what the upcoming events already book would cancel nothing and just leave those orders oversold. `Product/Update` refuses a reduction that an upcoming event day no longer fits, naming the day and the quantity, and checks the product before the variants. Only reductions are checked, so a row that is already oversold can still be corrected upwards. For the same reason `Product/Delete` refuses to retire a product that an upcoming event still books — a past order holding a retired product is fine, creating that situation for an order still ahead is not.

### One product can appear several times in an order

`ProductOrder` has a surrogate `Id` primary key and a unique index on `(ProductId, OrderId, UnitPrice, VariantId)`. So the same product can be reserved twice at different prices — the price a client was quoted before a change, plus the current one — and also twice at the same price with different variants.

`VariantId` is nullable, which is why it could not live in a composite primary key; migration `09` rebuilt the key for exactly this. SQL Server treats NULLs as **equal** inside a unique index, so an order still holds only one "sin variante" line per product and price.

The whole line set travels in one call: `Order/Create` and `Order/Update`. `Update` reconciles by `(ProductId, UnitPrice, VariantId)` — matched lines get their quantity patched, unmatched ones are hard deleted, leftovers are inserted. The unit price is `Product.Price + (variant.PriceExtra ?? 0)` unless the request supplies one.

Deleting an order only stamps `deleted_at` on the order itself. Its lines stay, and the stock they reserved is released anyway because every availability query reaches them through `Order`.

### Moving a price follows through to the open orders

Moving a product's price — **up or down** — re-prices the lines of orders that are **not paid** and whose **event has not happened**. A paid order and a past event stay frozen at what was agreed, and a line at any other price is a quote somebody chose to hold. A variant moves with its own surcharge: what its lines were quoted at is `Price + PriceExtra`, and either half moving is enough.

It all happens inside `Product/Update`, in one transaction with the product save and the `ProductPriceHistory` rows, so the log can never claim a re-pricing that rolled back. When the order already holds a line at the target price the two are merged with their quantities summed; over the quantity cap both are left untouched and counted as skipped.

Every movement leaves a log row: one for the product when its price or break price changed, plus one per variant whose surcharge changed. A variant row carries `VariantId` and its own surcharge on both sides, which is what the screen shows for it; the product price of the moment rides along so the effective price stays reconstructible. Each row reports only the lines its own change re-priced.

`GET /api/Product/GetPriceHistory?id=1` returns every change with how many orders and lines it moved.

`ProductOrder` has no `deleted_at`, so this rewrites rows with no undo. The predicate is the whole safety of the operation — rehearse any change to it against a restored copy.

### A draft holds no stock

`Order.IsDraft` marks a quote that is waiting for the client to say yes. The row is a complete order — client, event date, the whole line set — but **every availability query skips it**, so nothing it lists is taken away from whoever books the same day first. `Order/Create` accepts `isDraft`, and while an order is a draft neither `Create` nor `Update` measures its lines against the date.

`Order/Confirm` is what turns it into a real order, and the only moment its lines are checked. It reports the **whole** shortage at once rather than the first one it hits:

```json
{ "error": { "message": "No se puede confirmar el pedido: no hay stock suficiente en la fecha del evento.", "code": 400 },
  "data": { "id": 42, "missing": [
    { "productId": 1, "description": "Silla plastica blanca", "variantId": null, "variantDescription": null,
      "requestedQuantity": 60, "availableStock": 45 }
  ] } }
```

Sending the client back one product per attempt is exactly what makes re-quoting painful, which is the problem the flag exists to solve: the quote is created once, sent, and days later confirmed with a single call instead of being retyped.

There is no way back. Confirming is audited as `ORDER_CONFIRMED`; a confirmed order that falls through is deleted like any other.

The listing filters on `isDraft` and returns it on every row. A draft is excluded from the balance — billed and collected alike — because nobody has accepted it yet, and its PDF prints as **PRESUPUESTO**. It is *not* excluded from re-pricing: an unaccepted quote follows the catalogue rather than holding a price nobody agreed to. `Product/Delete` and variant retirement still count drafts, since retiring something a draft holds would leave it impossible to confirm.

### Payment freezes an order

`Order/MarkAsPaid` and `Order/MarkAsUnpaid` stamp `PaidAt` / `PaidBy`. A paid order stays editable on purpose — fixing a typo should not require cancelling the charge. The order listing filters on `paid`, and the balance reports billed and collected income side by side.

### Text normalization

There is **no** `TextHelper`. Services trim the incoming strings when building the entity and nothing else. The legacy casing rules (title case for client names, upper-first for descriptions) are not implemented; if they are ever wanted back, the helper belongs in `Core/Helpers/` and is applied in the service, never in the controller.

## Endpoints

All endpoints require a bearer token; `POST /api/Auth/Login` is the only public one. This matches the legacy `[Security]` filter, which protected every controller except `Login`.

```http
POST /api/Auth/Login
POST /api/Auth/Logout

GET  /api/Dashboard/Get                 Product count

GET  /api/Category/GetCombo
POST /api/Category/GetAll
GET  /api/Category/GetOne
POST /api/Category/Create
POST /api/Category/Update
POST /api/Category/Delete               Refused while the category has products

GET  /api/Product/GetCombo
POST /api/Product/GetAll                Search by description, filter by category
GET  /api/Product/GetOne                Detail with its variants
POST /api/Product/Create                Product + variants in one call
POST /api/Product/Update                Product + variants; a rise re-prices the open orders
POST /api/Product/Delete
POST /api/Product/GetStock              Availability for an event date, product and variants
GET  /api/Product/GetPriceHistory       Every price change and what it moved
POST /api/Product/Report                PDF price list; filter by category for a partial list
POST /api/Product/ExportExcel

POST /api/Order/GetAll                  By day or date range, filter by payment state
GET  /api/Order/GetOne                  Detail with lines, variants and totals
POST /api/Order/Create                  Header + line list in one call
POST /api/Order/Update                  Header + the whole line set, reconciled
POST /api/Order/Delete
POST /api/Order/GetDates                Days holding orders, for the calendar
POST /api/Order/Confirm                 Draft -> order; the one stock check, reports every shortage
POST /api/Order/MarkAsPaid              Freezes the order against price changes
POST /api/Order/MarkAsUnpaid
POST /api/Order/Report                  PDF detail, with or without prices
POST /api/Order/Budget                  PDF quote for an order not yet created, valid 15 days
POST /api/Order/ExportExcel

POST /api/Expense/GetAll                By date range
GET  /api/Expense/GetOne
POST /api/Expense/Create
POST /api/Expense/Update
POST /api/Expense/Delete
POST /api/Expense/GetBalance            Expenses, billed and collected income, profit
POST /api/Expense/Report                PDF balance
POST /api/Expense/ExportExcel

POST /api/User/...                      User management (admin)
POST /api/Audit/GetAll                  Audit trail (admin)
```

Listings return the **whole filtered set**, exactly as the legacy screens did — nothing paginates, on either side. `GetAllRequest` extends `SortRequest` (`ColumnSort` + `SortDirection`); leaving `ColumnSort` unset keeps each listing's natural order. What bounds a response is its filters: the audit trail is the only table with unbounded growth, and its screen defaults to the current month.

### How the legacy maps onto it

| Legacy | Now |
|---|---|
| `Orders/Index?date=` | `POST /Order/GetAll` with `date` |
| `Orders/OrdersDates` | `POST /Order/GetDates` |
| `Orders/Create` + `AddProducts` + session basket | `POST /Order/Create` — the whole order in one call |
| `Orders/AddToExistingOrder` | `POST /Order/Update` — the whole line set, reconciled |
| `Orders/RemoveFromExistingOrder` | `POST /Order/Update` — a line left out of the set is removed |
| `Orders/ReportWithPrice` / `ReportWithoutPrice` | `POST /Order/Report` with `withPrice` |
| `Orders/ReportBudget` | `POST /Order/Budget` |
| `Products/ProductsStock` | `POST /Product/GetStock` |
| `Products/Print` | `POST /Product/Report` |
| `Expenses/Print` (balance) | `POST /Expense/Report` and `POST /Expense/GetBalance` |
| `Home/Index` product count | `GET /Dashboard/Get` |

The session-based basket is gone: an order arrives complete, which also removes the legacy failure mode where a lost session dropped a half-built order. Stock checks that the legacy trusted the client for (`real_stock`, `stock` query parameters) are now computed on the server.

## Configuration

Update [appsettings.json](LaNata-Api/appsettings.json) per environment.

```json
{
  "Cors": {
    "AllowedOrigins": "http://localhost:3000;https://localhost:3000"
  },
  "ConnectionStrings": {
    "APIContextConnection": "Server=localhost;Database=LaNata;Integrated Security=True;Trusted_Connection=True;Encrypt=false;MultipleActiveResultSets=true"
  },
  "Database": {
    "RunSqlMigrations": true
  },
  "Reporting": {
    "License": "Community",
    "EnableDebugging": false
  },
  "Jwt": {
    "Issuer": "LaNata",
    "Audience": "LaNata",
    "Key": "CHANGE_ME_AT_LEAST_32_CHARS_AND_MORE"
  }
}
```

- `Cors:AllowedOrigins` — semicolon-separated list of allowed browser origins. **Required**: the app does not start if it is empty. Only `GET` and `POST` are allowed, and `Content-Disposition` is exposed so a browser can read the file name of a download.
- `Jwt:Key` must be supplied per environment through environment variables or user-secrets — never committed. Use the `__` separator for nested keys, e.g. `Jwt__Key`.

This points at the **live** database. Take a backup before the first run: startup applies the audit-column migration.

## First Run

From the repository root:

```powershell
dotnet build LaNata-Api.sln
```

```powershell
dotnet run --project LaNata-Api/LaNata-Api.csproj
```

On startup, the app will:

- Create the `_Migrations` table if it is missing.
- Run pending SQL scripts from `LaNata-Api/Core/DAL/Migrations`.

Both migrations are additive and idempotent: every statement is guarded by `IF COL_LENGTH(...) IS NULL` or `IF NOT EXISTS`, so a second run is a no-op. No existing table, column or row is altered.

To rehearse it first, restore a copy and run against that:

```powershell
sqlcmd -S localhost -E -C -Q "BACKUP DATABASE [LaNata] TO DISK = N'C:\temp\LaNata.bak' WITH INIT, COPY_ONLY, COMPRESSION"
```

## Auth

```http
POST /api/Auth/Login
Content-Type: application/json

{
  "email": "nicolamagni@hotmail.com",
  "password": "..."
}
```

Use the returned JWT as a bearer token:

```http
Authorization: Bearer <token>
```

Roles are `Administrador` and `Empleado` — the text already sitting in the `Role` table, which is what travels in the JWT role claim. The business endpoints only require authentication, matching the legacy behaviour where every logged-in user could manage products, orders and expenses; user administration and the audit trail are admin-only.

### Existing passwords must be reset once

Passwords are hashed with BCrypt. The legacy stored **unsalted SHA-256 hex**, which BCrypt cannot verify, so every pre-existing user needs a new hash before they can log in. `AuthService.VerifyPassword` treats a legacy hash as a failed login rather than throwing.

The `password` column stays `char(64)`. A BCrypt hash is 60 characters, so it fits, but SQL Server pads every read back to 64 — `VerifyPassword` trims before verifying. Do not compare the column directly.

Generate a BCrypt hash with any tool that produces the standard `$2a$`/`$2b$` format, then apply it:

```sql
UPDATE [dbo].[User] SET [password] = '<hash>' WHERE [email] = 'nicolamagni@hotmail.com';
```

After that first reset, `POST /api/User/UpdatePassword` handles rotations normally, and new users created through `POST /api/User/Create` are hashed by the API.

## Security

Startup configuration is split into modules under [Core/Security/Config](LaNata-Api/Core/Security/Config) (`CorsConfig`, `AuthenticationConfig`, `AuthorizationConfig`, `RateLimitConfig`, `SecurityHeadersConfig`) plus [DatabaseConfig](LaNata-Api/Core/DAL/DB/DatabaseConfig.cs); `Program.cs` only wires them together and declares the middleware pipeline.

- CORS is restricted to the origins in `Cors:AllowedOrigins`, methods `GET` and `POST`.
- Security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy`, `Cache-Control: no-store`, …) are applied by `Core/Security/Config/SecurityHeadersConfig.cs`.
- HSTS and HTTPS metadata validation for JWT bearer are enabled outside Development.
- The Kestrel `Server` response header is disabled.
- Rate limiting (partitioned by client IP): a 300 req/min global limit, and a 10 req/5 min limit on `POST /api/Auth/Login` (`RateLimitPolicies.Login`). Rejections return `429` with the standard `BaseResponse` error shape.
  - The partition key is `HttpContext.Connection.RemoteIpAddress`. This is the real client IP when Kestrel is exposed directly and when hosting on Azure App Service / IIS. If a CDN or reverse proxy is ever put in front, add `UseForwardedHeaders` first — otherwise every request shares the proxy address and one limit is spent by all users at once.

## Migrations

All migrations are plain SQL scripts under a single folder:

```text
LaNata-Api/Core/DAL/Migrations
  01_Audit_Columns.sql                        Audit columns and FKs on User, Product, Order, Expense
  02_Audit_Schema.sql                         The audit schema, its two catalogs and the entry table
  03_Product_BreakPrice_Nullable.sql
  04_Rename_Columns_To_Model_Names.sql        snake_case -> PascalCase, Rol -> Role
  05_Data_Types_And_Validation_Constraints.sql  float -> DECIMAL(18,2), CHECK constraints
  06_Remove_Order_Product_Audit_Actions.sql
  07_Category.sql                             Category table and Product.CategoryId
  08_Order_Payment_And_Price_History.sql      Order.PaidAt/PaidBy and ProductPriceHistory
  09_Product_Variants.sql                     ProductVariant and the ProductOrder key rebuild
  10_Drop_Numeric_Check_Constraints.sql       Numeric bounds move out of the database into the API
  11_Price_History_Variants.sql               ProductPriceHistory rows for a variant surcharge change
  12_Order_Draft.sql                          Order.IsDraft, so a quote can be saved without reserving
```

Scripts are executed in filename order. Each executed filename is stored in `_Migrations`, so each script runs once. New scripts keep the numeric prefix (`10_Add_New_Feature.sql`).

Two things to know before writing one:

- **Each file is sent as a single batch**, with no `GO`. T-SQL resolves the column names of an already existing table at compile time, so a statement naming a column added earlier *in the same file* fails with "Invalid column name" and rolls the whole file back. Wrap those in `EXEC sys.sp_executesql N'...'`, as `05`, `07`, `08` and `09` do.
- **`09` is not additive**: it rebuilds `PK_ProductOrder` on a table holding every order line ever written. Full backup, a rehearsal on a restored copy, and preferably a manual run in a maintenance window with `Database:RunSqlMigrations` set to `false`.

What `01_Audit_Columns.sql` adds, and nothing else:

| Table | Added |
|---|---|
| `User` | `created_at`, `updated_at`, `deleted_at`, `created_by`, `updated_by`, `deleted_by` |
| `Product` | `updated_at`, `created_by`, `updated_by`, `deleted_by` |
| `Order` | `updated_at`, `created_by`, `updated_by`, `deleted_by` |
| `Expense` | `created_at`, `updated_at`, `created_by`, `updated_by`, `deleted_by` |

`Product.created_at`, `Product.deleted_at`, `Order.created_at`, `Order.deleted_at` and `Expense.deleted_at` were already there and are left alone. `ProductOrder` and `Rol` are not touched by `01`; migration `04` later renamed every one of these columns to PascalCase, and `Rol` to `Role`.

The three tables in `02_Audit_Schema.sql` live in their own `audit` schema, so they add no columns, indexes or triggers to anything operational. Dropping the schema reverts them completely.

> New rows stamp `created_at` in **UTC**, while the legacy wrote Argentina local time (`UtcNow.AddHours(-3)`). Rows written before and after the cutover differ by three hours on that column. It is not surfaced anywhere in the app, but it matters if you ever query `created_at` directly.

## Audit

The `Audit` library covers two separate needs.

**Who last touched a row** is automatic: entities inheriting `AuditableEntity` get `CreatedBy`, `UpdatedAt`/`UpdatedBy` and `DeletedBy` stamped by `AuditInterceptor` on every save. No history is kept.

**What changed and when** is explicit and lives in the `audit` schema:

```text
audit.AuditCategory   Groups actions (Seguridad, Datos, Documentos).
audit.AuditAction     Action catalog (CREATED, UPDATED, DELETED, ORDER_PAID, EXPORTED, …).
audit.AuditEntry      One row per audited operation, with before/after snapshots.
```

Nothing is recorded automatically. A service records an entry by calling `AuditService.Audit(...)`, and **only the fields it registers** are compared and stored:

```csharp
private static AuditChangeSet BuildAuditChanges(AuditChangeList<Product> changeList)
{
    return changeList
        .AddField(x => x.Description, "Descripción")
        .AddField(x => x.Stock, "Stock")
        .AddField(x => x.Price, "Precio", AuditFormat.Currency)
        .AddField(x => x.BreakPrice, "Precio por rotura", AuditFormat.Currency)
        .Build();
}
```

Call sites pick the entry point that matches the operation:

```csharp
await Audit(AuditActionCode.CREATED, product, BuildAuditChanges(AuditChangeList.ForCreated(product)));
await Audit(AuditActionCode.UPDATED, product, BuildAuditChanges(AuditChangeList.For(previous, product)));
await Audit(AuditActionCode.DELETED, product, BuildAuditChanges(AuditChangeList.ForDeleted(product)));
```

`Build()` produces the change list plus the two snapshots:

```json
{
  "Summary": [
    { "Field": "Price", "FieldName": "Precio", "PreviousValue": "$500.00", "UpdatedValue": "$550.00",
      "Description": "Se modificó Precio de «$500.00» a «$550.00»." }
  ],
  "State":   { "Description": "Silla plastica blanca", "Stock": "100", "Price": "$500.00" },
  "Payload": { "Description": "Silla plastica blanca", "Stock": "100", "Price": "$550.00" }
}
```

Builder methods:

| Method | Use for |
|---|---|
| `AddField(x => x.Prop, "Label", format)` | Scalar properties. `AuditFormat` handles dates, currency, percentages, yes/no, active/inactive. |
| `AddLookup(x => x.RoleId, resolver, "Rol")` | Foreign keys and codes; the resolver returns the text the user should read. |
| `AddCollection<TItem>(x => x.Items, x => x.Id, x => x.Name, "Productos")` | Collections; reports items added and removed, compared by key. |

Order lines are not diffed through the change list either: creation stores the complete initial line snapshot in `Payload`, while `BuildUpdateAuditChanges` stores the full before/after line snapshots — product, variant, quantity, unit price and subtotal — in `State` and `Payload`, plus one synthetic change entry, which reads better than a collection diff. `HaveSameProductLines` compares `(ProductId, Quantity, UnitPrice, VariantId)`; dropping `VariantId` there would make a variant swap vanish from the trail.

A product update writes a single `UPDATED` entry containing the product fields, variant changes and, when applicable, the affected order ids and re-priced line counts under `Payload.Repricing`.

Reading the trail (admin only): `POST /api/Audit/GetAll`, `GET /api/Audit/GetOne`, `GET /api/Audit/GetFilters`.

Notes:

- `AuditService.Audit` never throws. On failure it returns a `BaseResponse` error so a broken audit cannot break the business operation.
- The write runs on its own `APIContext`, so it never flushes the caller's pending changes. Call it **after** the business `SaveChangesAsync`.
- A new action code needs the constant in `AuditActionCode` *and* a seed row in a SQL migration (`AuditEntry.ActionCode` is a foreign key). New entity codes need neither — `Entity` is a free string.

## Export

`ExportService` turns any in-memory collection into an XLSX file. Nothing is exported implicitly — the calling service declares the columns, the same rule the audit change list follows:

```csharp
var columns = ExportColumns
    .For<Product>()
    .Add(x => x.Id, "Id", ExportValueFormat.Integer)
    .Add(x => x.Description, "Producto", width: 45)
    .Add(x => x.Stock, "Stock", ExportValueFormat.Integer)
    .Add(x => x.Price, "Precio", ExportValueFormat.Currency)
    .Build();

var rs = await _exportService.Export(ExportFileFormat.Excel, new ExportRequest<Product>
{
    FileName = "productos",
    SheetName = "Productos",
    Columns = columns,
    Rows = products
});
```

| Method | Output |
|---|---|
| `ToExcel(rq)` | Single-sheet XLSX, frozen header, autofilter, sized columns |
| `Export(format, rq)` | Dispatches on `ExportFileFormat`, which today has only `Excel` |
| `ToExcelWorkbook(rq)` | Multi-sheet XLSX |

`ExportValueFormat` picks the **cell type**, so numbers and dates arrive as real numbers and dates and stay sortable. Use `Text` for codes that must keep their leading zeros — phone numbers use it.

There is no CSV writer. If one is ever wanted back it needs a UTF-8 BOM (or Excel opens the file in the system codepage and every accent breaks), `;` as the delimiter for Spanish locales, and a quote prefix on values starting with `=`, `+`, `-` or `@` so exported user input cannot run as a formula when the file is opened.

## Report

`ReportService` generates PDFs with [QuestPDF](https://www.questpdf.com/), replacing the legacy Razor print views. For a listing, no PDF code is needed:

```csharp
var columns = ReportColumns
    .For<Product>()
    .Add(x => x.Description, "Producto", width: 4)
    .AddCurrency(x => x.Price, "Precio")
    .AddCurrency(x => x.BreakPrice, "Precio por rotura")
    .Build();

var rs = _reportService.GenerateTable(new ReportTableRequest<Product>
{
    FileName = "listado_de_productos",
    Columns = columns,
    Rows = products,
    Options = new ReportOptions
    {
        Title = CompanyInfo.Name,
        Subtitle = "Listado de productos",
        FooterNote = CompanyInfo.FooterNote
    }
});
```

The generated document opens with a header carrying the logo and the contact lines, then the applied filters, the summary cards, the table with a repeating header, and a footer with the generation stamp and page numbers. The header prints **only on the first page** (`ShowOnce`), so on a multi-page listing that height goes to the table; the footer repeats, because it carries the page numbers. Company details printed on every report live in `Core/Models/Constants/CompanyInfo.cs`.

### Reports are printed, not read on screen

Every report exists to come out of a printer, always in black and white, and nobody keeps the PDF. That decides two things about the styling, and both live in `Report/Models/Constants/`:

- **No background fills anywhere.** `ReportColors` has no fill colors at all — no header band, no zebra striping, no tinted cards or note boxes. A fill prints as a gray smear and only burns toner. Hierarchy comes from size, weight and rules: the table header is bold over a thick bottom line, rows are separated by a hairline, and a summary card is a box with a heavier border. Do not reintroduce `.Background(...)` in a report.
- **The type scale is sized for paper**, not for a screen: the table body (`FontSm`) is 11pt and nothing goes below 9pt.
- **The block of data above the table is kept tight.** In a filter or summary card the label and the value share one paragraph instead of being stacked, and a filter value prints at `FontSm`. Stacked at `FontMd` the cards took a third of the first page before a single row of the table appeared.

Bigger type costs width, which is why `ReportDimensions` also owns the relative widths of the columns whose content has a known length — `ColumnWidthMoney` and `ColumnWidthDate`, the defaults of `AddCurrency` and `AddDate`. At print size `$ 1.234.567,00` no longer fits in one relative unit and would break in two lines, so a money column added through plain `Add` should pass `ReportDimensions.ColumnWidthMoney` too.

The order detail uses the same table report for both variants: `ReportColumns.AddIf` drops the price columns when `withPrice` is `false`, and the shipment is rendered as one more row (`OrderReportRow`), exactly as the legacy view did.

The balance is the one report a table cannot express — it stacks the expenses table and the income table in one document — so it inherits `ReportDocumentBase` in `Core/Helpers/Documents/BalanceReportDocument.cs` and is passed to `ReportService.Generate(document, fileName)`. `ReportService.Merge` renders several documents into one PDF.

> **Licensing:** QuestPDF is dual-licensed. The Community license is free only below the vendor's revenue threshold; above it, a paid license is required. Review this before shipping.

## Downloading Documents

File endpoints are the one place where controllers do not return `BaseResponse`, because a download is not JSON:

```csharp
[HttpPost]
public async Task<IActionResult> ExportExcel([FromBody] GetAllRequest rq)
{
    var rs = await _productService.Export(ExportFileFormat.Excel, rq);

    return rs.Success
        ? File(rs.Data.Content, rs.Data.ContentType, rs.Data.FileName)
        : BadRequest(rs);
}
```

Documents cover the whole filtered set rather than the current page.

Every successful generation is audited under the `DOCUMENT` category — `EXPORTED` for CSV/XLSX, `REPORT_GENERATED` for PDF — because knowing who took which data out of the system is exactly what an audit trail is for. There is no before/after to compare, so the entry records the file name, the format, the row count and the filters that produced it:

```json
{
  "Archivo": "pedidos_20260810_1831.csv",
  "Formato": "Csv",
  "Registros": 1,
  "Fecha": "15/09/2026"
}
```

## Development Notes

- Controllers should remain thin and delegate to services.
- No service interfaces are used.
- Use SQL scripts for database migrations.
- Use transactions only when multiple database operations must succeed or fail together. Creating an order with its lines is a single `SaveChangesAsync`, which EF already makes atomic.
- Keep reusable constants under `Models/Constants`.
- Keep request and response DTOs under `Models/DTO`.
- Keep `Models/` for database tables only. Anything not mapped to a table goes under `Models/Internal`.
- User-facing messages are Spanish and live in `Core/Models/Constants/Messages.cs`.

## Build Verification

The project should compile from the root with:

```powershell
dotnet build LaNata-Api.sln
```

## Deployment (GitHub Actions to Azure App Service)

The workflow lives at `.github/workflows/deploy.yml`, already wired to this project: it triggers on pushes to `master` (plus manual `workflow_dispatch`), builds `LaNata-Api.sln`, publishes `LaNata-Api/LaNata-Api.csproj` and deploys to the `lanata-api` App Service. The steps below document how it is set up, and are what to follow when pointing it at another App Service or branch.

The result: every push to the trigger branch builds the project and deploys it to Azure. SQL migrations run automatically at app startup via `MigrationRunner.Run()`, so no extra step is required for the database.

### 1. Create the workflow file

The file already exists at `.github/workflows/deploy.yml`. For another environment, copy the content below and replace `<APP_NAME>` with the Azure App Service name and `<BRANCH>` with the branch to deploy from.

```yaml
name: Build and deploy ASP.NET Core app to Azure Web App - <APP_NAME>

on:
  push:
    branches:
      - <BRANCH>
  workflow_dispatch:

jobs:
  build:
    runs-on: windows-latest
    permissions:
      contents: read

    steps:
      - name: Checkout source
        uses: actions/checkout@v4

      - name: Set up .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore LaNata-Api.sln

      - name: Build (Release)
        run: dotnet build LaNata-Api.sln --configuration Release --no-restore

      - name: Publish
        run: dotnet publish LaNata-Api/LaNata-Api.csproj --configuration Release --no-build --output "${{ github.workspace }}/publish"

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: app
          path: ${{ github.workspace }}/publish

  deploy:
    runs-on: windows-latest
    needs: build
    environment:
      name: 'Production'
      url: ${{ steps.deploy-to-webapp.outputs.webapp-url }}
    permissions:
      contents: read

    steps:
      - name: Download artifact
        uses: actions/download-artifact@v4
        with:
          name: app
          path: ./publish

      - name: Deploy to Azure Web App
        id: deploy-to-webapp
        uses: azure/webapps-deploy@v3
        with:
          app-name: '<APP_NAME>'
          slot-name: 'Production'
          package: ./publish
          publish-profile: ${{ secrets.AZUREAPPSERVICE_PUBLISHPROFILE }}
```

### 2. Enable Basic Auth on the App Service

The publish profile method requires SCM Basic Auth, which Azure now disables by default on new App Services.

1. Azure Portal, open the target App Service.
2. Left sidebar, **Settings** -> **Configuration**.
3. Open the **General settings** tab.
4. Find **SCM Basic Auth Publishing Credentials** and set it to **On**.
5. Click **Save** at the top, confirm the prompt, and wait about 30 seconds.

If the toggle is greyed out, an Azure Policy at the subscription or management group level is blocking it. In that case, switch to a Service Principal with OIDC instead of a publish profile.

### 3. Download the publish profile

1. App Service **Overview** tab.
2. Click **Get publish profile** in the top bar. A `.PublishSettings` file downloads.
3. Open the file in a text editor and copy the entire XML.

### 4. Add the publish profile as a GitHub secret

1. In the GitHub repo, **Settings** -> **Secrets and variables** -> **Actions** -> **New repository secret**.
2. Name: `AZUREAPPSERVICE_PUBLISHPROFILE`.
3. Value: paste the full XML from the publish profile.
4. Save.

### 5. Disable any existing CD source in Azure

If Deployment Center was previously configured for the App Service (Local Git, Bitbucket, an older GitHub Action, etc.), disconnect it so it does not conflict with the new workflow.

1. App Service -> **Deployment Center**.
2. **Disconnect** any existing source.

### 6. First run

Push to the trigger branch, or launch it by hand from the **Actions** tab (`Run workflow`). After the first run completes, every subsequent push to that branch redeploys the app.

### Notes

- Migrations run at app startup, so any new SQL files under `LaNata-Api/Core/DAL/Migrations` will be applied when the new build is restarted by Azure after deployment.
- For staging environments, duplicate the workflow with a different trigger branch, a deployment slot name other than `Production`, and a separate publish profile secret.
