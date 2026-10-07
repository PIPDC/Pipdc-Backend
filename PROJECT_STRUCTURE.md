# PIPDC Backend - Project Structure & Audit

Audit date: 2026-10-07 (state: after merging PR #41, the four-project layering refactor).
Reference graph and package layout are read from source; entities and patterns were verified by tracing two end-to-end examples.

## 0. One project or four?

**Four real projects, one repository folder.**

Before the refactor this was a **single** `csproj` (`PIPDC.csproj` at the repo root) whose four layer *folders* (`Domain / Application / Infrastructure / API`) were separated only by convention - nothing stopped a Domain file from referencing the API layer.

Today the root `csproj` is gone and the solution contains **four projects under `src/`** plus **one test project**, all wired through `PIPDC.sln`:

```
PIPDC/
  PIPDC.sln                 -> src/Domain, src/Application, src/Infrastructure, src/API, tests/PIPDC.ArchitectureTests
  Directory.Build.props     -> shared TargetFramework=net9.0, Nullable, ImplicitUsings
  src/
    Domain/         PIPDC.Domain.csproj          (innermost - references nothing)
    Application/    PIPDC.Application.csproj     (references Domain)
    Infrastructure/ PIPDC.Infrastructure.csproj  (references Application + Domain)
    API/            PIPDC.API.csproj             (references Application + Infrastructure)  <- the runnable entry point
  tests/
    PIPDC.ArchitectureTests/PIPDC.ArchitectureTests.csproj (references all four; enforces the graph)
```

The folder names look the same as before, but the boundaries are now **compiler-enforced**: a reference that violates the graph fails the build, and an xUnit architecture test (`tests/PIPDC.ArchitectureTests/LayeringTests.cs`) independently verifies the graph by scanning each assembly's type references.

**Reference graph (enforced):**

```
        Domain
          ^
          |
    Application        (EF Core, Cloudinary; seams: IAppDbContext, IMessageNotifier, IUniqueViolationDetector, IImageService...)
      ^        ^
      |        |
Infrastructure (Npgsql, Identity, JwtBearer, EF Design, Gmail, MimeKit, HealthChecks)
      ^        ^
      |        |
    ===== API =====   (Asp.Versioning, OpenApi, Scalar, SignalR - the composition root)
    tests/PIPDC.ArchitectureTests
```

**How to run it:** `dotnet run --project src/API` (the Web SDK project) or `dotnet build PIPDC.sln`. The old root `dotnet run` fails on purpose - there is no root project anymore.

---

## 1. Solution and repository root

```
PIPDC.sln                  5 projects (4 layers + tests); solution folders: src/Domain, src/Application, src/Infrastructure, src/API
Directory.Build.props      net9.0, Nullable=enable, ImplicitUsings=enable (shared by every csproj)
.editorconfig / .gitattributes / .gitignore
appsettings.json           + appsettings.Development.json   (repo-root copies; doubles of src/API)
PIPDC.http                 REST scratchpad
ARCHITECTURE.md            architecture and deviation history
log.txt                    dev scratch log (untracked/ignored)
```

`appsettings.Development.json` holds the real connection string via **User Secrets**; `src/API/PIPDC.API.csproj` keeps `UserSecretsId=e1fa52bf-603f-4f38-b75a-6670210bed0a`.

---

## 2. Directory tree by project

### 2.1 `src/Domain` - the innermost layer (28 entities, 2 auth entities, 18 enums, 4 common types)

```
src/Domain/
  Common/        BaseEntity (Id, CreatedAt) . AuditableEntity (+UpdatedAt) . Result / Result<T> . Error
  Auth/          RefreshToken . VerificationCode
  Entities/      28 POCO entities (see list)
  Enums/         18 enums (status + type enums for properties, agents, developments, transactions...)
  PIPDC.Domain.csproj   ONLY Microsoft.Extensions.Identity.Stores 9.0.17 (AppUser : IdentityUser)
```

Entities: `Agent, AgentApplication, AgentApplicationBlock, AgentRegistrationAppeal, AgentReport, AgentReview, AiChatSession, AppUser, BlogPost, BlogPostTag, Category, Conversation, DevelopmentProject, DevelopmentProjectImage, DevelopmentTracking, DevelopmentUnit, DevelopmentUpdate, Enquiry, IdempotencyRecord, LeaseRecord, Location, Message, Notification, Property, PropertyImage, SaleRecord, SavedProperty, Tag`

Enums: `AgentAppealStatus, AgentApplicationStatus, AgentReportReason, AgentReportStatus, BlogPostStatus, ConversationEscalationStatus, DevelopmentProjectStatus, DevelopmentTrackingStatus, DevelopmentUnitStatus, EnquiryStatus, ErrorType, IdempotencyStatus, ListingType, LocationType, PropertyStatus, PropertyType, TransactionStatus, VerificationPurpose`

### 2.2 `src/Application` - business rules and service contracts (17 feature folders)

```
src/Application/
  DependencyInjection.cs        AddApplication(): registers every service Scoped
  Data/IAppDbContext.cs         DbSet<T> facade + DatabaseFacade + SaveChangesAsync (Application never touches AppDbContext type)
  Data/IUniqueViolationDetector.cs   seam so TransactionService never names Npgsql
  Agents/       AgentApplicationService . AgentLicenseGenerator . AgentMappers . AgentQueryParameters . AgentReportService . AgentReviewService . AgentService . Dtos . IAgent{Application,Report,Review,Service}
  AiChat/       AiChatService . Dtos . GeminiAbstractions (IGeminiClient) . IAiChatService
  Auth/         Dtos . IAuthService . ITokenService . JwtSettings . Roles
  Blog/         BlogService . CategoryService . TagService . BlogPostQueryParameters . Dtos . I{Blog,Category,Tag}Service . *Dtos
  Common/       PaginatedResult<T> . PublicVisibility
  Contact/      ContactRequest . ContactService . IContactService
  Conversations/ConversationAuthorization . ConversationEscalationService . ConversationProjections . ConversationQueryParameters . ConversationService . Dtos . MessageMappers . MessageService
                 I{Conversation,ConversationEscalation,Message}Service . IMessageNotifier   <- realtime seam
  Dashboard/    DashboardService . Dtos . IDashboardService
  Developments/ DevelopmentProjectService . DevelopmentProjectPublicService . DevelopmentUnitService . DevelopmentUpdateService . DevelopmentTrackingService
                 DevelopmentListingPromoter . DevelopmentProjectQueryParameters . Dtos . IDevelopment{Project,ProjectPublic,Unit,Update,Tracking}Service . IDevelopmentListingPromoter
  Email/        EmailMessage . EmailQueueExtensions . EmailSettings . EmailTemplates . GmailApiSettings . IEmailQueue . IEmailService
  Enquiries/    EnquiryService . EnquiryMappers . EnquiryQueryParameters . Dtos . IEnquiryService
  Locations/    LocationService . Dtos . ILocationService
  Properties/   PropertyService . PropertyMappers . PropertyQueryParameters . PropertyStatusDisplay . PropertyTypeDisplay . Dtos . IPropertyService
  SavedProperties/ SavedPropertyService . SavedPropertyQueryParameters . Dtos . ISavedPropertyService
  Services/     IImageService . ImageService            (Cloudinary)
  Transactions/ TransactionService . TransactionQueryParameters . TransactionAnalyticsDto . Dtos . ITransactionService
  Users/        UserService . Dtos . IUserService
```

Counts: **24 service implementations + 29 `IService` interfaces** in Application (three of those interfaces are implemented in Infrastructure: `IAuthService -> AuthService`, `ITokenService -> TokenService`, `IEmailService -> GmailApiEmailService`). The `IAppDbContext` facade is what keeps `Application` compiling against EF Core types while Infrastructure owns the concrete `AppDbContext` and provider.

Packages: `Microsoft.EntityFrameworkCore 9.0.20` (for `IAppDbContext`, matches the Npgsql-required version), `CloudinaryDotNet 1.29.3`, `FrameworkReference Microsoft.AspNetCore.App` (for `IFormFile` on `IImageService`), re-declared Web-SDK global usings.

### 2.3 `src/Infrastructure` - persistence plus every external integration

```
src/Infrastructure/
  DependencyInjection.cs        AddInfrastructure(config): DbContext, Identity, JwtBearer, CORS, Email queue,
                                rate limiting, Turnstile, AI client, health checks
  Auth/         AuthService . TokenService                (JWT + ASP.NET Identity verification)
  Captcha/      TurnstileSettings . TurnstileVerifier . VerifyHumanAttribute
  Data/         AppDbContext . RoleSeeder . DevelopmentSeeder . PostgresUniqueViolationDetector
    Configurations/  27 IEntityTypeConfiguration<T> classes (one per entity: indexes, FK rules, enum-as-string)
    Migrations/      31 migrations + AppDbContextModelSnapshot   (20260716..20261004)
  Email/        EmailQueue . EmailQueueWorker . GmailApiEmailService   (singleton in-memory queue + background host)
  Gemini/       GeminiSettings . GeminiClient
  HealthChecks/ HealthCheckServiceExtensions . HealthCheckEndpointExtensions
  Idempotency/  IdempotentAttribute
  OpenRouter/   OpenRouterSettings . OpenRouterClient   (AI provider behind IGeminiClient)
  RateLimiting/ Global . Writes . Uploads . AuthStrict . RateLimitPartitioners . RateLimitPolicies . RateLimitServiceExtensions
```

Packages: `Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4`, `Microsoft.EntityFrameworkCore.Design 9.0.17` (PrivateAssets), `Microsoft.AspNetCore.Identity.EntityFrameworkCore 9.0.17`, `Microsoft.AspNetCore.Authentication.JwtBearer 9.0.17`, `Google.Apis.Gmail.v1 1.69.0.3742`, `MimeKit 4.17.0`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 9.*`, FrameworkReference.

### 2.4 `src/API` - composition root and delivery (29 controllers)

```
src/API/
  Program.cs                  everything composes here (see Section 4)
  Controllers/  29 controllers (list)
  Extensions/   GlobalExceptionHandler . ResultExtensions (Result -> HTTP status)
  Hubs/         MessagingHub . SignalRMessageNotifier . ConversationGroup . JwtSubUserIdProvider
  Properties/   launchSettings.json
  appsettings.*.json          tracked copies (production ship-set)
  PIPDC.API.csproj            Sdk="Web"; UserSecretsId; the only project that can `dotnet run`
```

Controllers: `AgentApplications, AgentReports, AgentReviews, Agents, AiChat, Auth, Blog, Categories, Contact, ConversationEscalations, Conversations, Dashboard, DevelopmentNotifications, DevelopmentProjects, DevelopmentProjectsPublic, DevelopmentTracking, DevelopmentTrackingAdmin, DevelopmentUnits, DevelopmentUpdates, Enquiries, Images, Locations, Messages, Properties, SavedProperties, Secured, Tags, Transactions, Users`.

Packages: `Asp.Versioning.Mvc 8.1.0` (+ ApiExplorer), `Microsoft.AspNetCore.OpenApi 9.0.18`, `Scalar.AspNetCore 2.16.13`, `Microsoft.EntityFrameworkCore 9.0.20`, `Microsoft.EntityFrameworkCore.Design 9.0.17` (startup-project copy for `dotnet ef`).

### 2.5 `tests/PIPDC.ArchitectureTests`

xUnit project referencing all four layers. Five `LayeringTests` verify the reference graph with positive controls (Domain refs nothing; Application not below; Infrastructure not above; controllers only see Application service interfaces in their constructor parameters). Uses an in-box `System.Reflection.Metadata` type-reference scanner - no NetArchTest (rejected because its exact-namespace matching would be vacuous).

---

## 3. Patterns - tracing two entities from Domain to API

### Pattern A - a full feature module: `DevelopmentProject` (project/units/listing promotion)

```
DOMAIN          DEVELOPMENTPROJECT ENTITY                     DEVELOPMENTUNIT ENTITY
src/Domain/Entities/DevelopmentProject.cs                     src/Domain/Entities/DevelopmentUnit.cs
  : AuditableEntity (Id, CreatedAt, UpdatedAt)                  : AuditableEntity
  Name, Description, Slug, Location, LocationRefId               UnitIdentifier, UnitType, Price, Currency, Period
  Status (enum), ProgressPercentage, Featured                    PropertyType, ListingType, Bedrooms..., Amenities
  PropertyId (nullable -> 1-to-1 listing link)                   PropertyId (nullable -> the created listing)
  HasMany: Units, Updates, Images, TrackedBy                     -> Project (parent), Property (listing)

INFRASTRUCTURE  EF CONFIGURATION (per-entity, in Infrastructure only)
src/Infrastructure/Data/Configurations/DevelopmentProjectConfiguration.cs
  Slug unique index; Status stored as string (HasConversion<string>); index on Status, Featured
  One-to-one: HasOne(Property).WithOne().HasForeignKey<DevelopmentProject>(PropertyId) + unique index + SET NULL
  xmin row version for optimistic concurrency
  Loaded by AppDbContext.OnModelCreating -> ApplyConfigurationsFromAssembly
AppDbContext extends IdentityDbContext<AppUser> -> 31 migrations on Npgsql -> DbSet<T> surfaced on IAppDbContext

APPLICATION     THE SERVICE (interface + implementation)
src/Application/Developments/IDevelopmentProjectService.cs   (GetAll/GetById/Create/Update/Delete/UpdateFeatured/notifications)
src/Application/Developments/DevelopmentProjectService.cs    -> uses IAppDbContext only
src/Application/Developments/DevelopmentProjectPublicService.cs -> browse-facing read path (visible statuses, slug routing)
src/Application/Developments/DevelopmentUnitService.cs       -> unit CRUD
src/Application/Developments/DevelopmentListingPromoter.cs   IDevelopmentListingPromoter
    Application-owned rule: promotes a finished project's units into real Property listings
    (IsListable, UniqueSlug generation, location/description inheritance, cover image copied from the estate)
    Depends on IAppDbContext + UserManager<AppUser> + ILogger - no Npgsql, no controller types
src/Application/Developments/Dtos.cs + DevelopmentProjectQueryParameters.cs  (Request/Response/filter/pagination)

API             THIN CONTROLLER -> ROUTE -> RESULT MAPPING
src/API/Controllers/DevelopmentProjectsPublicController.cs (public: GET browse, browse/{slug}, browse/{id})
src/API/Controllers/DevelopmentProjectsController.cs        (admin: CRUD + feature toggle + unit routes)
  [ApiController][ApiVersion(1.0)][Route("api/v{version:apiVersion}/development-projects")]
  Action -> await service.<Method>(args, ct) -> result.ToActionResult()   (Result/Error -> 200/201/400/404/409/403/500)
```

**Feature-flow:** controller action -> `IDevelopmentProjectService` -> `DevelopmentProjectService` -> `IAppDbContext.DbSet<DevelopmentProject>` (Npgsql via `AppDbContext`) -> entity changed property by property -> `SaveChangesAsync` -> `Result<T>` back to the controller. Enum-status values cross the wire as **strings** (`JsonStringEnumConverter`), DTOs are hand-rolled (not EF entities), and query parameters are explicit classes feeding a `PaginatedResult<T>`.

### Pattern B - cross-layer realtime without an inverted dependency: `Message` + SignalR

```
DOMAIN          src/Domain/Entities/Message.cs : BaseEntity
                ConversationId, SenderUserId, Content, ReadAt; nav to Conversation + AppUser Sender
INFRASTRUCTURE  src/Infrastructure/Data/Configurations/MessageConfiguration.cs
                composite index (ConversationId, CreatedAt); FK cascade to conversation, Restrict on sender
                + AppDbContext + migration AddConversationsAndMessages
APPLICATION     src/Application/Conversations/IMessageService.cs / MessageService.cs
                rules: validate content -> ConversationAuthorization.AuthorizeSendAsync ->
                create Message + touch Conversation.LastMessageAt in ONE SaveChanges ->
                best-effort email via IEmailQueue (never blocks the request)
                src/Application/Conversations/IMessageNotifier.cs   <-- Application OWNS the "what changed" decision
                src/Application/Conversations/MessageMappers.cs     entity->MessageDto
API             src/API/Hubs/SignalRMessageNotifier.cs : IMessageNotifier   <-- the ONLY type that knows the hub,
                group names (ConversationGroup.For(id)) and event names (NewMessage / ConversationEscalationChanged)
                src/API/Hubs/MessagingHub.cs   [Authorize]; JoinConversation/LeaveConversation only -
                no persistence and no business logic; authorization reuses ConversationAuthorization
                registered in Program.cs: AddSignalR + AddScoped<IMessageNotifier, SignalRMessageNotifier>
                src/API/Controllers/MessagesController.cs   POST .../messages [Idempotent] [EnableRateLimiting(Writes)]
```

**Why this matters:** `MessageService.SendAsync` publishes to a conversation **without ever referencing SignalR**. It calls `IMessageNotifier`; `Program.cs` supplies the SignalR implementation. Swap the transport tomorrow and not one line of Application code changes. The compiler enforces it: Application may not reference `PIPDC.API`.

The same "Application-owned interface, Infrastructure/API implementation" seam is used by `IAppDbContext`, `IUniqueViolationDetector` (Npgsql-specific unique-violation classification) and `IImageService` (Cloudinary).

---

## 4. Composition root (`src/API/Program.cs`) - how it all wires up

1. Kestrel 10 MB body cap; `JsonStringEnumConverter`; `AddSignalR` + `JwtSubUserIdProvider`.
2. `AddApiVersioning` (URL segment `v`), `AddApiExplorer` (per-version OpenAPI docs), `AddOpenApi` + **Scalar** UI, per-version Bearer security transformers.
3. `AddInfrastructure(config)` then `AddApplication()` then the API-only `IMessageNotifier` registration.
4. Model-state validation failures re-shape into the same `{ code, message, type }` error object everywhere.
5. Startup: migrate + seed roles outside Production; dev seeder only in Development.
6. Middleware order: exception handler -> forwarded headers -> HSTS + security headers (+dev CSP for Scalar) -> OpenAPI/Scalar (dev) -> HTTPS redirect -> CORS -> Authentication -> RateLimiting -> Authorization -> Controllers -> health-checks -> `MapHub<MessagingHub>("/hubs/messaging")`.

Cross-cutting (all configured in `Program.cs`/`Infrastructure`): HSTS 365d, security headers incl. strict CSP in production, CORS named policy `AllowFrontend`, JWT (`MapInboundClaims=false`, `name`/`role` claim mapping, `access_token` query-token accepted only on `/hubs/messaging`), per-controller rate-limit policies (`Writes`, `Uploads`, `AuthStrict`, `Global`), `[Idempotent]` write deduplication, DB health check, Turnstile `VerifyHumanAttribute`, AI chat via `IGeminiClient` (OpenRouter or Gemini by config).

---

## 5. Audit summary

| Area | Number |
|---|---|
| Projects in `PIPDC.sln` | 4 (Domain, Application, Infrastructure, API) + 1 test |
| Domain entities (+ auth entities) | 28 (+ `RefreshToken`, `VerificationCode`) |
| Domain enums | 18 |
| EF `IEntityTypeConfiguration` classes | 27 |
| EF migrations (non-designer) | 31 (+ snapshot; no pending model changes) |
| Application services (impls / interfaces) | 24 / 29 `IService` interfaces |
| API controllers | 29 |
| SignalR hub / notifier | 1 / 1 (+ 2 supporting Hub types) |
| Runtime | .NET 9 (net9.0 library + Sdk.Web entry project) |
| Entry point | `dotnet run --project src/API` (https on 7123) |

Build status (last verification on `master` @ `c26c9d2`): `dotnet build -warnaserror` 0/0; architecture tests 5/5; `dotnet ef migrations list` = 31 (pre-split baseline); `has-pending-model-changes` = none.