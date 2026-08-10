# Template API

.NET 10 Web API template for business microservices.

This project is intended to be copied as the starting point for a new business API. It includes the common backend pieces used across the reference APIs: authentication, role authorization, SQL script migrations, startup seed data, thin controllers, service-based business logic, and a small generic CRUD module.

## Stack

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core with SQL Server
- JWT bearer authentication
- SQL files for migrations

## Project Structure

The project is grouped into libraries by business area. They are folders inside the single `Template-Api` project, and namespaces mirror them (`Template_Api.<Library>.<Layer>`).

```text
Template-Api/
  Core/                 Users, roles, auth, generic CRUD examples, DbContext, and shared DTOs/constants.
  Audit/                Audit trail: AuditableEntity, AuditInterceptor and the audit schema.
  Export/               CSV and XLSX generation.
  Report/               PDF generation.
  Emailing/             SMTP sending and email templates.
  WhatsApp/             WhatsApp provider integration.
  MercadoPago/          Mercado Pago Checkout Pro integration.
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
    Interceptors/       EF Core SaveChanges interceptors.
  Models/               Database entities only — one class per table.
    Constants/          Reusable constants.
    DTO/                Controller request and response contracts.
    Internal/           Everything else: options, service inputs and outputs, enums.
  Security/
    Config/             Startup configuration modules (CORS, authentication, authorization, rate limiting, security headers, AWS).
  Services/             Business logic and integrations.
```

All SQL migrations live together in `Core/DAL/Migrations`, whichever library owns the tables they touch.

## Included Features

- Custom `User` and `Role` models.
- JWT login and authenticated logout.
- Role-based policies: `Admin` and `User`.
- Default roles and admin user seeded via SQL migration.
- SQL migration runner using the `_Migrations` table.
- `AuditInterceptor` EF Core interceptor that stamps `CreatedBy`, `UpdatedAt`/`UpdatedBy`, and `DeletedBy` on every `AuditableEntity`, taking the user from the JWT.
- Explicit audit trail in the `audit` schema, with a generic change-list builder (see [Audit](#audit)).
- Generic CSV/XLSX export and PDF reporting, consumable from any service (see [Export](#export) and [Report](#report)).
- Generic CRUD example with `GenericItem`.
- Product CRUD example with price and stock.
- Cart and cart product checkout example with item price snapshots.
- AWS S3 file upload, signed URL, and delete service.
- WhatsApp text sending service with message status constants.
- Email template rendering service.
- Mercado Pago Checkout Pro preference creation and webhook processing.
- Configurable CORS, response security headers, HSTS, and IP-based rate limiting (see [Security](#security)).

## Configuration

Update [appsettings.json](Template-Api/appsettings.json) before using the template in a real project.

Important settings:

```json
{
  "Cors": {
    "AllowedOrigins": "https://localhost:3000"
  },
  "ConnectionStrings": {
    "APIContextConnection": "Server=localhost;Database=TemplateApi;Integrated Security=True;Trusted_Connection=True;Encrypt=false;MultipleActiveResultSets=true"
  },
  "Database": {
    "RunSqlMigrations": true
  },
  "Jwt": {
    "Issuer": "TemplateApi",
    "Audience": "TemplateApi",
    "Key": "CHANGE_ME_TemplateApi_JwtKey_AtLeast32Chars"
  },
  "Aws": {
    "Enabled": false,
    "BucketName": "",
    "AccessKey": "",
    "SecretKey": "",
    "Region": "",
    "SignedUrlMinutes": 15
  }
}
```

- `Cors:AllowedOrigins` — semicolon-separated list of allowed browser origins. **Required**: the app does not start if it is empty. Only `GET` and `POST` are allowed.
- `Aws:Enabled` — turns S3 on. While it is `false` the file service is a no-op, so local development needs no bucket. When it is `true`, `Aws:BucketName` and `Aws:Region` are **required** and the app does not start without them.
- `Aws:AccessKey` / `Aws:SecretKey` — leave them empty in any environment that provides an IAM role (App Service, EC2, ECS). Empty keys let the SDK use its credential chain; setting them overrides it.
- Secrets (`Jwt:Key`, `Aws:AccessKey`, `Aws:SecretKey`, `EmailOptions:Password`, `MercadoPagoOptions:AccessToken`, `WhatsAppOptions:ApiKey`) must be supplied per environment through environment variables or user-secrets — never committed. Use the `__` separator for nested keys, e.g. `MercadoPagoOptions__AccessToken`.

Do not keep the default JWT key in production. The first admin user is seeded by `03_Seed_Initial_Data.sql` with email `admin@localhost` and password `Password1!` &mdash; rotate the password (and ideally the email) before deploying anywhere non-local.

## Security

Startup configuration is split into modules under [Core/Security/Config](Template-Api/Core/Security/Config) (`CorsConfig`, `AuthenticationConfig`, `AuthorizationConfig`, `RateLimitConfig`, `SecurityHeadersConfig`) plus [DatabaseConfig](Template-Api/Core/DAL/DB/DatabaseConfig.cs); `Program.cs` only wires them together and declares the middleware pipeline.

- CORS is restricted to the origins in `Cors:AllowedOrigins`, methods `GET` and `POST`.
- Security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy`, `Cache-Control: no-store`, …) are applied by `Core/Security/Config/SecurityHeadersConfig.cs`.
- HSTS and HTTPS metadata validation for JWT bearer are enabled outside Development.
- The Kestrel `Server` response header is disabled.
- Rate limiting (`Microsoft.AspNetCore.RateLimiting`, partitioned by client IP): a 300 req/min global limit, and a 10 req/5 min limit on `POST /api/Auth/Login` (`RateLimitPolicies.Login`). Rejections return `429` with the standard `BaseResponse` error shape.
  - The partition key is `HttpContext.Connection.RemoteIpAddress`. This is the real client IP when Kestrel is exposed directly and when hosting on Azure App Service / IIS (the ASP.NET Core Module propagates it). If a CDN or reverse proxy is ever put in front (Cloudflare, nginx), add `UseForwardedHeaders` first — otherwise every request shares the proxy address and one limit is spent by all users at once.

## First Run

From the repository root:

```powershell
dotnet build Template-Api.sln
dotnet run --project Template-Api/Template-Api.csproj
```

Before running, create the SQL Server database named in `APIContextConnection`.

On startup, the app will:

- Ensure the `_Migrations` table exists.
- Run pending SQL scripts from `Template-Api/Core/DAL/Migrations` (this includes role and admin user seeding).

## Auth

Login endpoint:

```http
POST /api/Auth/Login
Content-Type: application/json

{
  "email": "admin@localhost",
  "password": "Password1!"
}
```

Use the returned JWT as a bearer token:

```http
Authorization: Bearer <token>
```

## Migrations

All migrations are plain SQL scripts under a single folder, whichever library owns the tables they touch:

```text
Template-Api/Core/DAL/Migrations
```

Naming convention:

```text
01_Initial.sql
02_Emailing.sql
03_Add_New_Feature.sql
```

Scripts are executed in filename order. Each executed filename is stored in `_Migrations`, so each script runs once.

## Audit

The `Audit` library covers two separate needs.

**Who last touched a row** is automatic: entities inheriting `AuditableEntity` get `CreatedBy`, `UpdatedAt`/`UpdatedBy` and `DeletedBy` stamped by `AuditInterceptor` on every save. No history is kept.

**What changed and when** is explicit and lives in the `audit` schema:

```text
audit.AuditCategory   Groups actions (Seguridad, Datos, Pagos, Notificaciones).
audit.AuditAction     Action catalog (CREATED, UPDATED, LOGIN, PAYMENT_CREATED, …).
audit.AuditEntry      One row per audited operation, with before/after snapshots.
```

Nothing is recorded automatically. A service records an entry by calling `AuditService.Audit(...)`, and **only the fields it registers** are compared and stored:

```csharp
public class ProductService(APIContext context, AuditService auditService)
{
    private static AuditChangeSet BuildAuditChanges(AuditChangeList<Product> changeList)
    {
        return changeList
            .AddField(x => x.Name, "Nombre")
            .AddField(x => x.Price, "Precio", AuditFormat.Currency)
            .AddField(x => x.IsActive, "Estado", AuditFormat.ActiveInactive)
            .Build();
    }

    private async Task Audit(string actionCode, Product product, AuditChangeSet changes)
    {
        await _auditService.Audit(new AuditRequest
        {
            ActionCode = actionCode,
            Entity = AuditEntityCode.PRODUCT,
            EntityKey = product.Id.ToString(),
            Name = product.Name,
            Changes = changes
        });
    }
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
    { "Field": "Price", "FieldName": "Precio", "PreviousValue": "$1,000.00", "UpdatedValue": "$1,250.50",
      "Description": "Se modificó Precio de «$1,000.00» a «$1,250.50»." }
  ],
  "State":   { "Name": "Agua 500ml",  "Price": "$1,000.00", "IsActive": "Activo" },
  "Payload": { "Name": "Agua 500 ml", "Price": "$1,250.50", "IsActive": "Activo" }
}
```

Builder methods:

| Method | Use for |
|---|---|
| `AddField(x => x.Prop, "Label", format)` | Scalar properties. `AuditFormat` handles dates, currency, percentages, yes/no, active/inactive. |
| `AddLookup(x => x.RoleId, resolver, "Rol")` | Foreign keys and codes; the resolver returns the text the user should read. |
| `AddCollection<TItem>(x => x.Items, x => x.Id, x => x.Name, "Productos")` | Collections; reports items added and removed, compared by key. |

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
    .Add(x => x.Name, "Nombre")
    .Add(x => x.Price, "Precio", ExportValueFormat.Currency)
    .Add("Estado", x => x.IsActive, ExportValueFormat.YesNo)
    .Add(x => x.CreatedAt, "Creado", ExportValueFormat.DateTime)
    .AddIf(App.IsAdmin, x => x.Cost, "Costo", ExportValueFormat.Currency)
    .Build();

var rs = await _exportService.Export(ExportFileFormat.Excel, new ExportRequest<Product>
{
    FileName = "productos",
    SheetName = "Productos",
    Columns = columns,
    Rows = products
});
```

`ExportFile` carries `FileName`, `ContentType` and `Content` (bytes), so it can be returned as a download, attached to an email, or uploaded through `AwsS3Service`.

| Method | Output |
|---|---|
| `ToCsv(rq)` | UTF-8 CSV with BOM, `;` delimiter by default |
| `ToExcel(rq)` | Single-sheet XLSX, frozen header, autofilter, sized columns |
| `Export(format, rq)` | Either of the above, format chosen at runtime |
| `ToExcelWorkbook(rq)` | Multi-sheet XLSX |

`ExportValueFormat` drives both outputs: in CSV it picks the text, in XLSX it also picks the **cell type**, so numbers and dates arrive as real numbers and dates and stay sortable. Use `Text` for codes that must keep their leading zeros.

Two details worth knowing:

- **CSV is written with a UTF-8 BOM.** Without it Excel opens the file in the system codepage and every accent breaks.
- **Values starting with `=`, `+`, `-` or `@` are prefixed with a quote.** This blocks CSV injection, where exported user input is executed as a formula when the file is opened.

## Report

`ReportService` generates PDFs with [QuestPDF](https://www.questpdf.com/). For a listing, no PDF code is needed:

```csharp
var columns = ReportColumns
    .For<Product>()
    .AddFixed(x => x.Id.ToString(), "Id", 30, ReportAlignment.Center)
    .Add(x => x.Name, "Producto", width: 3)
    .AddCurrency(x => x.Price, "Precio")
    .AddYesNo(x => x.IsActive, "Activo")
    .Build();

var rs = _reportService.GenerateTable(new ReportTableRequest<Product>
{
    FileName = "productos",
    Columns = columns,
    Rows = products,
    Options = new ReportOptions
    {
        Title = "Listado de productos",
        Subtitle = "Catálogo con precios y stock vigentes",
        Filters = [new("Búsqueda", "agua"), new("Estado", "Activos")],
        Summary = [new("Productos", "3"), new("Sin stock", "1")],
    }
});
```

The generated page carries a header with the title and optional logo, the applied filters, summary cards, the table with a repeating header, and a footer with the generation stamp and page numbers. `ReportOptions` covers orientation, page size (A4/Letter/Legal), margins, author and footer note.

For a layout the table report cannot express, inherit `ReportDocumentBase` — it owns the page setup, header and footer, leaving only `ComposeContent` to implement — and pass it to `ReportService.Generate(document, fileName)`. `ReportService.Merge` renders several documents into one PDF.

Configuration:

```json
{
  "Reporting": {
    "License": "Community",
    "EnableDebugging": false
  }
}
```

> **Licensing:** QuestPDF is dual-licensed. The Community license is free only below the vendor's revenue threshold; above it, a paid license is required. Review this before shipping a product built on this template.

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

Worked examples live in `ProductController`: `ExportCsv`, `ExportExcel` and `Report`. All three cover the whole filtered set rather than the current page.

Every successful generation is audited under the `DOCUMENT` category — `EXPORTED` for CSV/XLSX, `REPORT_GENERATED` for PDF — because knowing who took which data out of the system is exactly what an audit trail is for. There is no before/after to compare, so the entry records the file name, the format, the row count and the filters that produced it:

```json
{
  "Archivo": "productos_20260809_0154.xlsx",
  "Formato": "Excel",
  "Registros": 15,
  "Búsqueda": "agua",
  "Estado": "Activos"
}
```

## WhatsApp

WhatsApp settings live under `WhatsAppOptions`.

```json
{
  "WhatsAppOptions": {
    "Enabled": false,
    "BaseUrl": "",
    "ApiKey": "",
    "InstanceName": ""
  }
}
```

The current implementation targets the common Evolution API route:

```text
/message/sendText/{InstanceName}
```

Message statuses are defined in:

```text
Template-Api/WhatsApp/Models/Constants/WhatsAppMessageStatus.cs
```

## Mercado Pago

Mercado Pago settings live under `MercadoPagoOptions`.

```json
{
  "MercadoPagoOptions": {
    "Enabled": false,
    "BaseUrl": "https://api.mercadopago.com",
    "AccessToken": "",
    "PublicKey": "",
    "WebhookSecret": "",
    "CurrencyId": "ARS",
    "SuccessUrl": "",
    "PendingUrl": "",
    "FailureUrl": "",
    "NotificationUrl": "",
    "AutoReturn": "approved",
    "StatementDescriptor": "",
    "WebhookToleranceSeconds": 300
  }
}
```

The backend exposes:

```http
POST /api/Product/GetAll
GET /api/Product/GetOne
POST /api/Product/Create
POST /api/Product/Update
POST /api/Product/Delete
POST /api/Cart/Create
POST /api/Cart/Update
GET /api/Cart/GetOne
POST /api/MercadoPago/CreatePreference
POST /api/MercadoPago/CreateCartPreference
GET /api/MercadoPago/GetPayment
POST /api/MercadoPago/Webhook
```

`CreatePreference` is protected by JWT auth. It creates a Checkout Pro preference, stores the local `MercadoPagoPayment` log, and returns `InitPoint` and `SandboxInitPoint`.

`Cart/Create` receives product ids and quantities, loads current product data from SQL, validates active products and stock, and stores each line with `CartProduct.SettedPrice`. This means later product price changes do not change an already-created cart total.

`CreateCartPreference` is the safer checkout flow. It receives only `CartId` and optional payer data; the service loads the cart from SQL, validates ownership, validates stock, uses the cart's stored line prices, creates the Mercado Pago preference, and stores the payment with `CartId`.

`Webhook` is public because Mercado Pago calls it without the API JWT. The service validates the `x-signature` and `x-request-id` headers, fetches the payment from Mercado Pago, validates amount/currency against the local log, and updates the local payment status idempotently.

When Mercado Pago confirms an `approved` payment for a cart preference, the webhook decrements stock for each cart line and marks the cart as paid. Duplicate approved webhooks do not decrement stock again because `PaidAt` is used as the idempotency marker.

For real products, do not trust item prices sent by a frontend. Prefer the cart flow or replace it with product-specific order logic that loads all purchasable items from the database, checks the current user owns the order, calculates totals server-side, and then creates the Mercado Pago preference.

## AWS S3 Files

The AWS file service is registered as `AwsS3Service` and lives under:

```text
Template-Api/Core/Services/AwsS3Service.cs
```

It supports:

- Uploading one file.
- Uploading a list of files.
- Checking if a file exists.
- Reading a file's metadata (`GetFileInfoAsync` → `S3FileInfo`: content type, size, last modified, ETag).
- Downloading a file's bytes (`DownloadFileAsync` → `S3File`: key, file name, content type, content, size).
- Generating signed download URLs.
- Deleting one file.
- Deleting a list of files (batched, up to 1000 keys per request).

Every method returns `BaseResponse` / `BaseResponse<T>`, so a storage failure is a real error instead of an empty string the caller cannot interpret.

Two ways to hand a file back to the client, and they are not interchangeable:

- **`GetSignedUrlByFileKeyAsync`** — the browser downloads straight from S3. Cheaper and the default choice: no bytes go through the API.
- **`DownloadFileAsync`** — the bytes come back into the process. Use it when the file has to be re-served by the API without exposing the bucket, or when another service needs the content — `S3File.Content` plugs directly into `EmailAttachment(FileName, Content, ContentType)`. The download is capped by the same `FileHelper` size limit as uploads, checked against the response `Content-Length` **before** the body is buffered, so an oversized object is rejected instead of being read into memory.

`FileExistsAsync` delegates to `GetFileInfoAsync`, so both cost a single `HEAD`. `GetFileInfoAsync` returns `Data = null` for a missing file and an error only when the storage call actually failed.

Folder constants are defined in:

```text
Template-Api/Core/Models/Constants/FolderNames.cs
```

`folder` must be one of the `FolderNames` values and `key` must be a plain file name — both are validated, so a caller cannot escape the folder prefix or sign an arbitrary object of the bucket.

Uploads run only when `Aws:Enabled` is `true`. With the flag off, `UploadFileAsync` and the delete methods succeed without touching S3 (returning an empty key), and `GetSignedUrlByFileKeyAsync` returns a "not configured" error. Validation through `FileHelper` runs either way, so a request that fails locally fails in production too.

Stored objects:

- The key is `<folder>/<yyyy-MM-dd>_<id>_<guid><extension>`. The extension comes from the validated content type, never from the client-supplied file name.
- `ContentType` is set as the object's own header, and objects are written with `AES256` server-side encryption.
- Signed URLs expire after `Aws:SignedUrlMinutes` (default 15, capped at 60) and force a download through `Content-Disposition`. Pass `downloadName` to `GetSignedUrlByFileKeyAsync` to give the user a readable file name instead of the storage key.

Allowed types, the size cap and the content-type to extension map all live in `Core/Helpers/FileHelper.cs`. Add a new type there and both the validation and the generated extension follow.

## Development Notes

- Controllers should remain thin and delegate to services.
- No service interfaces are used.
- Use SQL scripts for database migrations.
- Use transactions only when multiple database operations must succeed or fail together.
- Keep reusable constants under `Models/Constants`.
- Keep request and response DTOs under `Models/DTO`.
- Keep `Models/` for database tables only. Anything not mapped to a table goes under `Models/Internal`.

## Build Verification

The project should compile from the root with:

```powershell
dotnet build Template-Api.sln
```

## Deployment (GitHub Actions to Azure App Service)

This template ships with no deploy workflow by default. To set up auto-deploy from a branch (e.g. `master` or `main`) to an Azure App Service, follow the steps below.

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
        run: dotnet restore Template-Api.sln

      - name: Build (Release)
        run: dotnet build Template-Api.sln --configuration Release --no-restore

      - name: Publish
        run: dotnet publish Template-Api/Template-Api.csproj --configuration Release --no-build --output "${{ github.workspace }}/publish"

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

If the project is renamed when copied from this template, also update the `.sln` and `.csproj` paths in the workflow.

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

- Migrations run at app startup, so any new SQL files under `Template-Api/Core/DAL/Migrations` will be applied when the new build is restarted by Azure after deployment.
- For staging environments, duplicate the workflow with a different trigger branch, a deployment slot name other than `Production`, and a separate publish profile secret.
