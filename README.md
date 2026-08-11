# La Ñata API

.NET 10 Web API for **La Ñata** — rental of chairs, tables, tableware, linen and furniture.

This is the backend rewrite of the legacy ASP.NET MVC 5 application (`La_Nata`). It keeps the same business rules and the same Spanish user-facing messages, but exposes them as a JSON API with JWT authentication, an audit trail, and generated PDF/CSV/XLSX documents instead of Razor print views. The web frontend is a separate project and does not exist yet.

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
  Export/               CSV and XLSX generation.
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

The C# names are readable; the columns underneath are the legacy ones:

| Entity | Table | Notes |
|---|---|---|
| `Product` | `Product` | `description`, `stock` (bigint), `price`, `break_price` |
| `Order` | `Order` | `client_name`, `phone`, `event_adress` (legacy typo kept in the column), `date`, `shipment_price`, `observation` |
| `ProductOrder` | `ProductOrder` | join table, keyed by `(id_product, id_order, unit_price)` |
| `Expense` | `Expense` | `date`, `description`, `price` |
| `Role` | `Rol` | two rows: `Administrador` and `Empleado` |
| `User` | `User` | `email`, `password`, `id_rol` — there are no name columns |

`Product`, `Order`, `Expense` and `User` inherit `AuditableEntity`, so every row is soft deleted and carries who created, edited and deleted it. `ProductOrder` does not: it is a join table with no `deleted_at`, and the app hard deletes from it exactly as the legacy did.

### Money is `double`, not `decimal`

Every price column in the production schema is `float`. Mapping `decimal` onto it would round silently on every write, so the entities and DTOs use `double`. This is the one place where the code follows the database instead of good practice — changing the columns to `decimal(18,2)` is the fix, and it is a schema migration nobody has authorised.

### Event dates carry a time

The legacy stored every event day at **12:00**, an artefact of adding 12 hours to dodge a timezone problem in its date input. All 2200 existing rows have it, so new rows keep the same time and the column stays homogeneous.

The consequence matters more than the cause: **every date filter is a half-open day range**, never an equality check. `x.Date >= day && x.Date < day.AddDays(1)`.

### Deleted products still belong to old orders

40 of the 274 products are soft deleted, and 170 order lines across 164 orders point at them. The global soft-delete query filter turns a naive `Include(x => x.Products).ThenInclude(x => x.Product)` into a join that **drops those lines**, under-reporting both the line count and the order total.

`OrderService.LoadLineProducts` is the answer: it resolves the products behind an order's lines with `IgnoreQueryFilters()`. Anything that reads an order's lines has to go through it — `GetOne`, `Report`, `RemoveProduct` and the date-change validation all do.

### Stock is per event date, not global

`Product.Stock` is the size of the catalogue, not what is free. What matters is availability **on the day of an event**: the stock minus everything the orders of that same date already reserve. This is the single rule the whole ordering flow is built on, and it is what `POST /api/Product/GetStock` answers:

```json
{ "date": "2026-09-15" }
```

```json
{ "id": 1, "description": "Silla plastica blanca",
  "stock": 100, "reservedStock": 50, "availableStock": 50 }
```

Pass `excludeOrderId` while editing an order so its own reservation is not counted against it.

Every write path re-checks availability server-side: creating an order, adding a product to one, and moving an order to another date.

### One product can appear twice in an order

`ProductOrder` is keyed by `(id_product, id_order, unit_price)`, so the same product can be reserved twice at different prices — the price a client was quoted before a price change, plus the current one.

**The price identifies the line**: `AddProduct` without `unitPrice` merges into (or opens) the line at the current catalogue price, and with `unitPrice` it targets the line at that price. `RemoveProduct` takes the price too — removing the whole reserved quantity hard deletes the row, anything less subtracts. There is no `deleted_at` on this table, so a removal is physical, exactly as the legacy did it.

Deleting an order only stamps `deleted_at` on the order itself. Its lines stay, and the stock they reserved is released anyway because every availability query reaches them through `Order`.

### Text normalization

The legacy app normalized every free text field on the way in, and so does this one (`Core/Helpers/TextHelper.cs`), because product descriptions are unique by text and reports must print consistently:

| Field | Rule | Example |
|---|---|---|
| Client name | letters only, title case | `juan PEREZ` → `Juan Perez` |
| Description, address, observation | letters and digits, first letter upper | `silla PLASTICA blanca` → `Silla plastica blanca` |
| Phone | digits only | `(03404) 42-0949` → `03404420949` |

## Endpoints

All endpoints require a bearer token; `POST /api/Auth/Login` is the only public one. This matches the legacy `[Security]` filter, which protected every controller except `Login`.

```http
POST /api/Auth/Login
POST /api/Auth/Logout

GET  /api/Dashboard/Get                 Product count

GET  /api/Product/GetCombo
POST /api/Product/GetAll                Search by description
GET  /api/Product/GetOne
POST /api/Product/Create
POST /api/Product/Update
POST /api/Product/Delete
POST /api/Product/GetStock              Availability for an event date
POST /api/Product/Report                PDF price list
POST /api/Product/ExportExcel

POST /api/Order/GetAll                  By day, or by date range
GET  /api/Order/GetOne                  Detail with lines and totals
POST /api/Order/Create                  Header + product list in one call
POST /api/Order/Update                  Header only
POST /api/Order/Delete
POST /api/Order/GetDates                Days holding orders, for the calendar
POST /api/Order/AddProduct
POST /api/Order/RemoveProduct
POST /api/Order/Report                  PDF detail, with or without prices
POST /api/Order/Budget                  PDF quote for an order not yet created
POST /api/Order/ExportExcel

POST /api/Expense/GetAll                By date range
GET  /api/Expense/GetOne
POST /api/Expense/Create
POST /api/Expense/Update
POST /api/Expense/Delete
POST /api/Expense/GetBalance            Expenses, income and profit between two dates
POST /api/Expense/Report                PDF balance
POST /api/Expense/ExportExcel

POST /api/User/...                      User management (admin)
POST /api/Audit/GetAll                  Audit trail (admin)
```

Listings return the **whole filtered set**, unsorted pagination aside, exactly as the legacy screens did. `GetAllRequest` extends `SortRequest` (`ColumnSort` + `SortDirection`); leaving `ColumnSort` unset keeps each listing's natural order. Users and the audit trail do paginate and extend `PaginateRequest` instead.

### How the legacy maps onto it

| Legacy | Now |
|---|---|
| `Orders/Index?date=` | `POST /Order/GetAll` with `date` |
| `Orders/OrdersDates` | `POST /Order/GetDates` |
| `Orders/Create` + `AddProducts` + session basket | `POST /Order/Create` — the whole order in one call |
| `Orders/AddToExistingOrder` | `POST /Order/AddProduct` |
| `Orders/RemoveFromExistingOrder` | `POST /Order/RemoveProduct` |
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

Roles are `Administrador` and `Empleado` — the text already sitting in the `Rol` table, which is what travels in the JWT role claim. The business endpoints only require authentication, matching the legacy behaviour where every logged-in user could manage products, orders and expenses; user administration and the audit trail are admin-only.

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
  01_Audit_Columns.sql        ALTER TABLE ... ADD on User, Product, Order, Expense
  02_Audit_Schema.sql         The audit schema, its two catalogs and the entry table
```

Scripts are executed in filename order. Each executed filename is stored in `_Migrations`, so each script runs once. New scripts keep the numeric prefix (`03_Add_New_Feature.sql`).

What `01_Audit_Columns.sql` adds, and nothing else:

| Table | Added |
|---|---|
| `User` | `created_at`, `updated_at`, `deleted_at`, `created_by`, `updated_by`, `deleted_by` |
| `Product` | `updated_at`, `created_by`, `updated_by`, `deleted_by` |
| `Order` | `updated_at`, `created_by`, `updated_by`, `deleted_by` |
| `Expense` | `created_at`, `updated_at`, `created_by`, `updated_by`, `deleted_by` |

`Product.created_at`, `Product.deleted_at`, `Order.created_at`, `Order.deleted_at` and `Expense.deleted_at` were already there and are left alone. `ProductOrder` and `Rol` are not touched at all.

The three tables in `02_Audit_Schema.sql` live in their own `audit` schema, so they add no columns, indexes or triggers to anything operational. Dropping the schema reverts them completely.

> New rows stamp `created_at` in **UTC**, while the legacy wrote Argentina local time (`UtcNow.AddHours(-3)`). Rows written before and after the cutover differ by three hours on that column. It is not surfaced anywhere in the app, but it matters if you ever query `created_at` directly.

## Audit

The `Audit` library covers two separate needs.

**Who last touched a row** is automatic: entities inheriting `AuditableEntity` get `CreatedBy`, `UpdatedAt`/`UpdatedBy` and `DeletedBy` stamped by `AuditInterceptor` on every save. No history is kept.

**What changed and when** is explicit and lives in the `audit` schema:

```text
audit.AuditCategory   Groups actions (Seguridad, Datos, Documentos).
audit.AuditAction     Action catalog (CREATED, UPDATED, DELETED, PRODUCT_ADDED, EXPORTED, …).
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

Order lines are not diffed through the change list: `AddProduct` and `RemoveProduct` write their own `PRODUCT_ADDED` / `PRODUCT_REMOVED` entries carrying the product, quantity and unit price, which reads better than a collection diff.

Reading the trail (admin only): `POST /api/Audit/GetAll`, `GET /api/Audit/GetOne`, `GET /api/Audit/GetFilters`.

Notes:

- `AuditService.Audit` never throws. On failure it returns a `BaseResponse` error so a broken audit cannot break the business operation.
- The write runs on its own `APIContext`, so it never flushes the caller's pending changes. Call it **after** the business `SaveChangesAsync`.
- A new action code needs the constant in `AuditActionCode` *and* a seed row in a SQL migration (`AuditEntry.ActionCode` is a foreign key). New entity codes need neither — `Entity` is a free string.

## Export

`ExportService` turns any in-memory collection into a CSV or XLSX file. Nothing is exported implicitly — the calling service declares the columns, the same rule the audit change list follows:

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
| `ToCsv(rq)` | UTF-8 CSV with BOM, `;` delimiter by default |
| `ToExcel(rq)` | Single-sheet XLSX, frozen header, autofilter, sized columns |
| `Export(format, rq)` | Either of the above, format chosen at runtime |
| `ToExcelWorkbook(rq)` | Multi-sheet XLSX |

`ExportValueFormat` drives both outputs: in CSV it picks the text, in XLSX it also picks the **cell type**, so numbers and dates arrive as real numbers and dates and stay sortable. Use `Text` for codes that must keep their leading zeros — phone numbers use it.

Two details worth knowing:

- **CSV is written with a UTF-8 BOM.** Without it Excel opens the file in the system codepage and every accent breaks.
- **Values starting with `=`, `+`, `-` or `@` are prefixed with a quote.** This blocks CSV injection, where exported user input is executed as a formula when the file is opened.

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

The generated page carries a header with the title, the applied filters, summary cards, the table with a repeating header, and a footer with the generation stamp and page numbers. Company details printed on every report live in `Core/Models/Constants/CompanyInfo.cs`.

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

This repository ships with no deploy workflow by default. To set up auto-deploy from a branch (e.g. `master` or `main`) to an Azure App Service, follow the steps below.

The result: every push to the trigger branch builds the project and deploys it to Azure. SQL migrations run automatically at app startup via `MigrationRunner.Run()`, so no extra step is required for the database.

### 1. Create the workflow file

Add a file at `.github/workflows/deploy.yml` with the following content. Replace `<APP_NAME>` with your Azure App Service name and `<BRANCH>` with the branch you want to deploy from.

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

Commit and push the workflow file to the trigger branch. The first run starts automatically. After it completes, every subsequent push to that branch will redeploy the app.

### Notes

- Migrations run at app startup, so any new SQL files under `LaNata-Api/Core/DAL/Migrations` will be applied when the new build is restarted by Azure after deployment.
- For staging environments, duplicate the workflow with a different trigger branch, a deployment slot name other than `Production`, and a separate publish profile secret.
