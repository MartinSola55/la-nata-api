# CLAUDE.md

Guidance for future work in this repository.

## Source Of Truth

- This repository is a backend API template. The web/frontend project will be created separately.
- Prefer patterns already present in this template over inventing new structure.

## Project Shape

- Target framework: `.NET 10`, `net10.0`.
- Solution file: `Template-Api.sln`.
- Main project folder: `Template-Api/`.
- Root namespace: `Template_Api`.
- Single API project unless explicitly requested otherwise.
- Keep root-level documentation in `README.md`.

## Libraries

- The project is split into libraries by business area. They are folders inside the single `Template-Api` project, not separate `.csproj` files. Do not split them into separate projects unless explicitly requested.
- Current libraries:
  - `Core` — everything that is not one of the integrations below: users, roles, auth, the generic CRUD examples, the `APIContext`, security config, and shared DTOs/constants.
  - `Audit` — audit trail: `AuditableEntity`, `AuditInterceptor` and the `audit` schema.
  - `Export` — CSV and XLSX generation.
  - `Report` — PDF generation.
  - `Emailing` — SMTP sending and email templates.
  - `WhatsApp` — WhatsApp provider integration.
  - `MercadoPago` — Mercado Pago Checkout Pro integration.
- Every library repeats the same inner layout, and only creates the folders it actually needs:

```text
Template-Api/
  <Library>/
    Controllers/
    DAL/
      DB/
      Filters/
    Helpers/
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

- Namespaces mirror the folders: `Template_Api.<Library>.<Layer>[.<SubFolder>]` — for example `Template_Api.Core.Models.DTO.User`, `Template_Api.Emailing.Services`, `Template_Api.Audit.Helpers.Interceptors`.
- Cross-library references are allowed and go through normal `using` directives. `Core` is the composition point: `ServiceContainer` registers the services of every library, and `APIContext` owns the `DbSet`s of every library's entities.
- When adding a new integration, create a new library folder instead of growing `Core`.

## Folder Structure

- `Program.cs`, `appsettings.json`, and `Properties/` stay at the project root, outside the libraries.
- Keep classes in one file named after the class, inside the folder matching their layer.

## Startup Configuration

- `Program.cs` stays a thin composition root: it only wires modules together and declares the middleware pipeline. Do not put configuration bodies (option lambdas, validation, policy definitions) in it.
- Each cross-cutting concern lives in its own `*Config` class, following the existing `*Container` convention: a plain class with `public static void Add...(IServiceCollection services, ...)` methods (not extension methods).
  - `Core/Security/Config/CorsConfig.cs` — CORS policy; owns the `PolicyName` constant.
  - `Core/Security/Config/AuthenticationConfig.cs` — JWT bearer setup and `Jwt:*` validation.
  - `Core/Security/Config/AuthorizationConfig.cs` — the `Policies.Admin` / `Policies.User` policies.
  - `Core/Security/Config/RateLimitConfig.cs` — rate limiter policies and the `429` rejection response.
  - `Core/Security/Config/AwsConfig.cs` — AWS credentials, region and `IAmazonS3` registration; owns the nested `AwsOptions` class and `Aws:*` validation.
  - `Core/Security/Config/SecurityHeadersConfig.cs` — response security headers (`UseSecurityHeaders`, the one extension method here) and the Kestrel server-header opt-out.
  - `Core/DAL/DB/DatabaseConfig.cs` — connection string, `APIContext` registration, interceptors, and `RunMigrations`.
- `ServiceContainer.AddServices(services, config)` also binds options classes belonging to a service (`EmailOptions`, `WhatsAppOptions`, `MercadoPagoOptions`). `AwsOptions` is the exception: it belongs to `AwsConfig` because the same values configure the SDK client, so `ServiceContainer` only registers `AwsS3Service`.
- An options class that a `*Config` needs for its own wiring is declared nested inside it (`AwsConfig.AwsOptions`) instead of living under `Models/`, so the config class stays the single place to look.
- Config classes read `IConfiguration` themselves and throw `InvalidOperationException` on missing required keys, so a misconfigured environment fails at startup rather than at request time.
- When adding a new cross-cutting concern, add a `*Config` class and a single call in `Program.cs`.

## Formatting Style

- C# uses block-scoped namespaces:

```csharp
namespace Template_Api.Core.Services
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
- Prefer collection expressions instead of `.toList()` method when valid.
- Use `#region` sections where the service/controller already follows that style:
  - `CRUD`
  - `Methods`
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
- Use `[Authorize(Policy = Policies.Admin)]` for admin-only actions.

## Services

- Put business logic in services under `<Library>/Services/`.
- Register services in `ServiceContainer.AddServices`.
- Do not create or implement service interfaces.
- Keep service names concrete: `AuthService`, `UserService`, `GenericItemService`, `AwsS3Service`, etc.
- Use EF Core directly through `APIContext`.
- Use `try/catch` around persistence and external provider calls where failure should return `BaseResponse` errors.
- Return user-facing errors through `Messages`.
- Do not throw for expected validation failures.

## Interfaces

- No custom service interfaces should be created or implemented.
- Do not add `IUserService`, `IGenericItemService`, repository interfaces, or similar abstractions.
- Use framework-provided types only where required by packages/framework registration.

## Auth And Roles

- Use custom `User` and `Role` entities.
- Do not switch to ASP.NET Identity unless explicitly requested.
- Passwords are hashed with `BCrypt.Net-Next`.
- JWT auth uses:
  - `Jwt:Issuer`
  - `Jwt:Audience`
  - `Jwt:Key`
- JWT claims include:
  - `ClaimTypes.Email`
  - `ClaimTypes.Name`
  - `ClaimTypes.Surname`
  - custom `userId`
  - `ClaimTypes.Role`
- Role values live in `Core/Models/Constants/Roles.cs`.
- Policy names live in `Core/Models/Constants/Policies.cs`.
- Configure policies with `RequireRole`.
- Current roles:
  - `ADMIN`
  - `USER`

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
- Pagination uses `PaginateRequest`.
- Default page size is defined in `Pagination.DefaultPageSize`.

## Models And Data

- `<Library>/Models/*.cs` at the root holds **database tables only** — one class per table, plus the base classes their columns come from (`AuditableEntity`). If a class is not mapped to a table, it does not belong there.
- Every other model goes in `<Library>/Models/Internal/`, namespace `Template_Api.<Library>.Models.Internal`: options classes, service inputs and outputs, column definitions, formatting enums. "Internal" means the type only exists inside the process — it is neither a table nor an API contract. The classes are still `public`; the folder name is about scope of use, not the C# access modifier.
- So a library's `Models/` has four possible homes, and the pick is mechanical:
  - `Models/` — mapped to a table.
  - `Models/DTO/<Feature>/` — request/response for a controller.
  - `Models/Constants/` — reusable constants.
  - `Models/Internal/` — everything else.
- `Export/` and `Report/` own no tables, so their `Models/` root is empty by design. Do not add table-less classes back into it to make it look populated.
- Soft-deletable entities inherit from `AuditableEntity`.
- `AuditableEntity` has:
  - `CreatedAt`
  - `UpdatedAt`
  - `DeletedAt`
  - `CreatedBy` / `UpdatedBy` / `DeletedBy` (nullable `User` ids)
  - `CreatedByUser` / `UpdatedByUser` / `DeletedByUser` navigations, mapped with `[ForeignKey]` to the matching `*By` column
- The `*At` / `*By` audit columns are written by `AuditInterceptor`, not by services.
- Use query filters in `APIContext` for soft-deleted entities.
- Entity `DbSet` names are singular, matching the reference style:
  - `User`
  - `Role`
  - `GenericItem`
  - `WhatsAppMessage`
- Use `[Key]` and `[ForeignKey]` attributes where the current style does.
- Always use `DateTime.UtcNow` dates for DateTime properties.

## Helpers

- Helpers used by more than one library live in `Core/Helpers/`. Helpers specific to one library go in that library's `Helpers/` folder.
- `Core/Helpers/JsonHelper.cs` owns every JSON round-trip in the project, as extension methods:
  - `source.Clone()` — deep copy through serialization. Use it to detach a snapshot of a tracked entity before mutating it. References are preserved, so navigations and back-references survive the copy. Do not hand-roll per-entity copy constructors for this.
  - `source.Serialize()` / `json.Deserialize<T>()` — for persisting and reading JSON columns.
- Cloning and persisting use different serializer options on purpose: clones preserve references (`$id`/`$ref`) so graphs round-trip, while stored JSON drops nulls and collapses cycles so the output stays readable by the web and by SQL consumers. Do not merge the two option sets.
- The project uses System.Text.Json only. Do not add Newtonsoft.Json.

## Constants

- Put reusable constants in `<Library>/Models/Constants/`.
- Prefer static classes for constants.
- Do not hard-code repeated status strings in services.
- AWS folder names must use `FolderNames`.

## User-Facing Messages

- Keep reusable Spanish messages in `Core/Models/Constants/Messages.cs`.
- Use proper Spanish accents and punctuation.
- Do not replace Spanish punctuation with ASCII-only text unless explicitly requested.
- Keep message construction consistent with the reference API:

```csharp
return entityName + " cread" + (femine ? "a" : "o") + " correctamente.";
```

## Generic CRUD Pattern

- `GenericItem` is the template's example generic CRUD.
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
- Do not apply pagination, unless explicitly requested.
- If pagination is applied, use:

```csharp
.Skip((rq.Page - 1) * Pagination.DefaultPageSize)
.Take(Pagination.DefaultPageSize)
```

## Database And Migrations

- Keep all migrations as SQL scripts.
- Do not add EF generated migration `.cs` files.
- All SQL scripts live in `Template-Api/Core/DAL/Migrations/`, regardless of which library owns the tables they touch. Do not add a `DAL/Migrations` folder to the other libraries.
- Migration names are ordered with numeric prefixes:
  - `01_Initial.sql`
  - `02_Emailing.sql`
  - `03_Add_New_Feature.sql`
- SQL scripts are copied to output through the `.csproj` content rule.
- The `MigrationRunner` runs pending SQL scripts at startup when `Database:RunSqlMigrations` is `true`.
- All data seeding (roles, initial admin user, lookup tables, etc.) must be done via SQL scripts. Do not seed data from C# code.
- Executed SQL script filenames are stored in `_Migrations`.
- Use `_Migrations` to prevent rerunning scripts.
- Do not use `Database.Migrate()` for this project.
- Do not use EF migrations unless explicitly requested.

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
- Audit text lives in `Audit/Models/Constants/AuditMessages.cs`, not in the shared `Messages` catalog, so the library stays self-contained.
- `AuditFormatter` renders values with the invariant culture on purpose: the text is stored and compared, so it must not depend on the host's culture.

## Documents (Export And Report)

Both libraries follow the audit rule: nothing is included implicitly, the calling service declares its columns.

### Export — CSV and XLSX

- `ExportService` takes an in-memory collection and returns an `ExportFile` (name, content type, bytes). It never writes to disk or to S3; the caller decides what to do with the bytes.
- Declare columns with `ExportColumns.For<T>()`, then `.Add(x => x.Field, "Encabezado", ExportValueFormat.Currency)`. `AddIf` skips a column for roles that must not see it.
- `ExportValueFormat` drives both outputs: in CSV it picks the text, in XLSX it also picks the **cell type**, so numbers and dates stay sortable in the spreadsheet instead of arriving as text. Use `Text` for codes that must keep leading zeros.
- CSV is written as UTF-8 **with BOM** — without it Excel falls back to the system codepage and mangles accents. The default delimiter is `;`, which is what Excel expects in Spanish locales.
- CSV values starting with `=`, `+`, `-` or `@` are prefixed with a quote. This is a CSV-injection guard: without it, exported user input runs as a formula when the file is opened. Do not remove it.
- `ToExcelWorkbook` builds multi-sheet workbooks. CSV has no equivalent.
- Exports are capped at `ExportLimits.MaxRows`; past that the request is rejected instead of building a huge file in memory.

### Report — PDF

- Built on QuestPDF. `ReportConfig.ConfigureReporting` sets the license from `Reporting:License` and must run in `Program.cs` before the first render, or QuestPDF throws.
- **QuestPDF is dual-licensed.** The Community license is free only below the vendor's revenue threshold; above it a paid license is required. Check this before shipping a product built on this template.
- Two ways in:
  - `ReportService.GenerateTable(...)` for a list report — filters, summary cards, table and record count, with no QuestPDF code at the call site. Columns come from `ReportColumns.For<T>()`.
  - Inherit `ReportDocumentBase` and pass it to `ReportService.Generate(...)` when the layout needs more. The base owns page setup, header and footer; the subclass only implements `ComposeContent`.
- `ReportService.Merge` renders several documents into one PDF for batch downloads.
- Reuse the components in `Report/Helpers/Components/` rather than composing raw QuestPDF in a report. Colors and spacing come from `ReportColors` and `ReportDimensions`, never hardcoded.
- Reports are capped at `ReportLimits.MaxRows`, lower than the export cap because PDF layout is far more expensive. Past that, an export is the right tool.

### Controllers

- File endpoints are the one exception to "controllers return `BaseResponse`": they return `IActionResult`, with `File(...)` on success and `BadRequest(rs)` carrying the usual error shape on failure. See `Core/Controllers/ProductController.cs`.
- Documents must cover the whole filtered set, not the current page. Reuse the listing's filter and order helpers but skip pagination, as `ProductService.GetProductsForDocument` does.
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
- If no entities or database state are modified, no transaction is needed.

## Configuration

- Main config is `Template-Api/appsettings.json`.
- Development overrides go in `Template-Api/appsettings.Development.json`.
- Do not commit real secrets.
- Use placeholders for template defaults.
- Do not add Swagger/OpenAPI. The template intentionally exposes no API schema endpoint.
- CORS is restricted to the origins listed in `Cors:AllowedOrigins`; do not reintroduce a permissive `AllowAnyOrigin` policy.

## AWS S3 Files

- Use `AwsS3Service` for AWS S3 file operations.
- `AwsS3Service` supports:
  - upload one file
  - upload a file list
  - check file existence
  - read file metadata
  - download file bytes
  - generate signed URLs
  - delete one file
  - delete a file list
- `GetFileInfoAsync` returns `S3FileInfo` and `DownloadFileAsync` returns `S3File`, both plain models in `Core/Models/` following the `ExportFile` / `ReportFile` precedent. Do not reuse `ExportFile` for storage results: it belongs to the `Export` library and carries a `RowCount` that means nothing here.
- `FileExistsAsync` delegates to `GetFileInfoAsync` — one `HEAD` for both. A missing file is `Data == null` (or `false`), never an error; errors are reserved for real storage failures.
- Prefer `GetSignedUrlByFileKeyAsync` to hand a file to a browser. `DownloadFileAsync` is for when the bytes must come back into the process (re-serving through the API, or feeding `EmailAttachment`).
- `DownloadFileAsync` buffers into memory, so it checks the response `Content-Length` against `FileHelper.IsValidFileSize` **before** reading the body. Never buffer an S3 object without that guard.
- Every method returns `BaseResponse` / `BaseResponse<T>` and takes an optional `CancellationToken`. Failures come back as `Messages.Error.AwsS3Exception()`, never as a silent empty value.
- Options live in `AwsConfig.AwsOptions`, bound from the `Aws` section. `AwsS3Service` is a no-op unless `Aws:Enabled` is `true` and `Aws:BucketName` is set; `AwsConfig` throws at startup if the flag is on and the bucket or region is missing.
- Static AWS keys are only applied when both `Aws:AccessKey` and `Aws:SecretKey` are non-empty. Never set them unconditionally — doing so overrides the SDK credential chain and breaks IAM-role deployments.
- Do not call `DoesS3BucketExistV2Async` per operation. The bucket is validated once at startup.
- `folder` must come from `FolderNames` and `key` must be a plain file name. Both are validated by `ValidateLocation`, which is what keeps a caller from escaping the folder prefix or signing an arbitrary object.
- Object keys are built by the service: `<folder>/<yyyy-MM-dd>_<id>_<guid><extension>`. Never build a key from a client-supplied file name — the extension is derived from the validated content type through `FileHelper.GetExtension`, and `id` is sanitized to ASCII alphanumerics.
- Set `ContentType` on the `PutObjectRequest`. Do not put it in `Metadata`: the SDK prefixes metadata names with `x-amz-meta-`, so the object would keep a binary content type.
- Uploads are written with `ServerSideEncryptionMethod.AES256` and never with a public ACL.
- Signed URLs expire after `Aws:SignedUrlMinutes` (default 15, hard cap 60) and always set `Content-Disposition: attachment`, escaped per RFC 5987.
- `DeleteFileListAsync` uses `DeleteObjectsAsync` in batches of 1000. `UploadFileListAsync` rolls back the keys it already uploaded if one file fails, so a partial upload never leaves orphans.
- Use `FileHelper` to validate uploaded file type and size before upload. `AwsS3Service` validates again on its own; `FileHelper` owns the allowed content types, the size cap and the content-type to extension map.

## Emailing

- Email options live under `EmailOptions`.
- Email templates are stored in the `emailing.EmailTemplate` table.
- Template codes live in `EmailTemplateCode`.
- Use token replacement with `[KEY]` placeholders.
- Use `EmailingService.RenderBody` for email template rendering.

## Documentation

- Keep root `README.md` up to date when adding reusable template features.
- Document new config keys and new reusable services.
- Keep `CLAUDE.md` updated when project-wide conventions change.
