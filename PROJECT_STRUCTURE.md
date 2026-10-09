# PIPDC Backend - Project Structure & Audit

Audit date: 2026-10-07 (state: after the single-project consolidation, `refactor/single-project`; one application project + NetArchTest-enforced layers).
Reference graph and package layout are read from source; entities and patterns were verified by tracing two end-to-end examples.

## 0. One project or four?

**One real project, four layer folders, one test project.**

The codebase started as a single `csproj` with four *folders* (`Domain /
Application / Infrastructure / API`) separated only by convention, was briefly
split into **four projects** so a wrong-way `using` was a compile error, and is
now consolidated back to **one application project** whose layer boundaries are
inside the assembly and enforced by **architecture tests** instead.

Today `PIPDC.sln` contains exactly two projects:

```
PIPDC/
  PIPDC.sln                 -> src/PIPDC.csproj + tests/PIPDC.ArchitectureTests
  Directory.Build.props     -> shared TargetFramework=net9.0, Nullable, ImplicitUsings
  src/
    PIPDC.csproj            Sdk="Microsoft.NET.Sdk.Web"; RootNamespace=AssemblyName=PIPDC; UserSecretsId
    Program.cs              composition root (was src/API/Program.cs)
    appsettings.json        + appsettings.Development.json   (single copy, in src/)
    Properties/launchSettings.json
    Domain/         PIPDC.Domain.*          innermost layer - references nothing
    Application/    PIPDC.Application.*     business rules + service contracts
    Infrastructure/ PIPDC.Infrastructure.*  persistence + external integrations
    API/            PIPDC.API.*             controllers, SignalR hubs (delivery)
  tests/
    PIPDC.ArchitectureTests/PIPDC.ArchitectureTests.csproj (NetArchTest.Rules; enforces the graph)
```

The layer folders map to namespaces `PIPDC.<Layer>.*` exactly (the csproj lives
in `src/` precisely so the folder layout and namespace roots match). The graph
below is NOT compiler-enforced anymore — it is asserted by 18 NetArchTest facts
(`tests/PIPDC.ArchitectureTests/ArchitectureTests.cs`) with namespace *prefix*
semantics:

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

**How to run it:** `dotnet run --project src` (or `--project src/PIPDC.csproj`).
`dotnet build PIPDC.sln` builds application + tests; `dotnet test` runs the
architecture suite from the solution root.

---

## 1. Solution and repository root

```
PIPDC.sln                  2 projects (application + tests)
Directory.Build.props      net9.0, Nullable=enable, ImplicitUsings=enable (shared by every csproj)
.editorconfig / .gitattributes / .gitignore
src/appsettings.json       + src/appsettings.Development.json  (single copy; content root = src/)
PIPDC.http                 REST scratchpad
ARCHITECTURE.md            architecture and deviation history
PROJECT_STRUCTURE.md       this file
```

`appsettings.Development.json` holds the real connection string via **User
Secrets**; `src/PIPDC.csproj` keeps `UserSecretsId=e1fa52bf-603f-4f38-b75a-6670210bed0a`.

---

## 2. Directory tree (single project, four layer folders)

### 2.1 `src/Domain` — the innermost layer (28 entities, 2 auth entities, 18 enums, 4 common types)

```
src/Domain/
  Common/        BaseEntity (Id, CreatedAt) . AuditableEntity (+UpdatedAt) . Result / Result<T> . Error
  Auth/          RefreshToken . VerificationCode
  Entities/      28 POCO entities (see list)
  Enums/         18 enums (status + type enums for properties, agents, developments, transactions...)
```

Entities: `Agent, AgentApplication, AgentApplicationBlock, AgentRegistrationAppeal, AgentReport, AgentReview, AiChatSession, AppUser, BlogPost, BlogPostTag, Category, Conversation, DevelopmentProject, DevelopmentProjectImage, DevelopmentTracking, DevelopmentUnit, DevelopmentUpdate, Enquiry, IdempotencyRecord, LeaseRecord, Location, Message, Notification, Property, PropertyImage, SaleRecord, SavedProperty, Tag`

Enums: `AgentAppealStatus, AgentApplicationStatus, AgentReportReason, AgentReportStatus, BlogPostStatus, ConversationEscalationStatus, DevelopmentProjectStatus, DevelopmentTrackingStatus, DevelopmentUnitStatus, EnquiryStatus, ErrorType, IdempotencyStatus, ListingType, LocationType, PropertyStatus, PropertyType, TransactionStatus, VerificationPurpose`

### 2.2 `src/Application` — business rules and service contracts (17 feature folders)

```
src/Application/
  DependencyInjection.cs        AddApplication(): registers every service Scoped
  Data/IAppDbContext.cs         DbSet<T> facade + DatabaseFacade + SaveChangesAsync (Application never touches AppDbContext type)
  Data/IUniqueViolationDetector.cs   seam so TransactionService never names Npgsql
  Agents/       AgentApplicationService . AgentLicenseGenerator . AgentMappers . AgentQueryParameters . AgentReportService . AgentReviewService . AgentService . Dtos . IAgent{Application,Report,Review,Service}
  AiChat/       AiChatService . Dtos . GeminiAbstractions (IGeminiClient) . IAiChatService
  Auth/         Dtos . IAuthService . ITokenService . JwtSettings . Roles
  Blog/         BlogService . CategoryService . TagService . BlogPostQueryParameters . Dtos . I{Blog,Category,Tag}Service . *Dtos
  Captcha/      VerifyHumanAttribute    <- marker only (behaviour in Infrastructure)
  Common/       PaginatedResult<T> . PublicVisibility
  Contact/      ContactRequest . ContactService . IContactService
  Conversations/ConversationAuthorization . ConversationEscalationService . ConversationProjections . ConversationQueryParameters . ConversationService . Dtos . MessageMappers . MessageService
                 I{Conversation,ConversationEscalation,Message}Service . IMessageNotifier   <- realtime seam
  Dashboard/    DashboardService . Dtos . IDashboardService
  Developments/ DevelopmentProjectService . DevelopmentProjectPublicService . DevelopmentUnitService . DevelopmentUpdateService . DevelopmentTrackingService
                 DevelopmentListingPromoter . DevelopmentProjectQueryParameters . Dtos . IDevelopment{Project,ProjectPublic,Unit,Update,Tracking}Service . IDevelopmentListingPromoter
  Email/        EmailMessage . EmailQueueExtensions . EmailSettings . EmailTemplates . GmailApiSettings . IEmailQueue . IEmailService
  Enquiries/    EnquiryService . EnquiryMappers . EnquiryQueryParameters . Dtos . IEnquiryService
  Idempotency/  IdempotentAttribute    <- marker only (behaviour in Infrastructure)
  Locations/    LocationService . Dtos . ILocationService
  Properties/   PropertyService . PropertyMappers . PropertyQueryParameters . PropertyStatusDisplay . PropertyTypeDisplay . Dtos . IPropertyService
  SavedProperties/ SavedPropertyService . SavedPropertyQueryParameters . Dtos . ISavedPropertyService
  Services/     IImageService . ImageService            (Cloudinary; takes Stream, no IFormFile)
  Transactions/ TransactionService . TransactionQueryParameters . TransactionAnalyticsDto . Dtos . ITransactionService
  Users/        UserService . Dtos . IUserService
```

Counts: **24 service implementations + 29 `IService` interfaces** in Application
(three interfaces are implemented outside the Application folder of the same
assembly: `IAuthService -> AuthService`, `ITokenService -> TokenService`,
`IEmailService -> GmailApiEmailService`). The architecture rule "every `I*Service`
in Application has exactly one implementation" is asserted at test time.

The `IAppDbContext` facade is what keeps `Application` compiling against EF Core
types while Infrastructure owns the concrete `AppDbContext` and provider.
`IImageService` takes `Stream` (not `IFormFile`) so Application never touches
`Microsoft.AspNetCore.Http`.

### 2.3 `src/Infrastructure` — persistence plus every external integration

```
src/Infrastructure/
  DependencyInjection.cs        AddInfrastructure(config): DbContext, Identity, JwtBearer, CORS, Email queue,
                                rate limiting, Turnstile, AI client, health checks, global action filters
  Auth/         AuthService . TokenService                (JWT + ASP.NET Identity verification)
  Captcha/      TurnstileSettings . TurnstileVerifier . VerifyHumanActionFilter  (runs on marked actions)
  Data/         AppDbContext . RoleSeeder . DevelopmentSeeder . PostgresUniqueViolationDetector
    Configurations/  27 IEntityTypeConfiguration<T> classes (one per entity: indexes, FK rules, enum-as-string)
    Migrations/      31 migrations + AppDbContextModelSnapshot   (20260716..20261004)
  Email/        EmailQueue . EmailQueueWorker . GmailApiEmailService   (singleton in-memory queue + background host)
  Gemini/       GeminiSettings . GeminiClient
  HealthChecks/ HealthCheckServiceExtensions . HealthCheckEndpointExtensions
  Idempotency/  IdempotencyActionFilter   (EF-backed dedup; engages on [Idempotent]-marked actions)
  OpenRouter/   OpenRouterSettings . OpenRouterClient   (AI provider behind IGeminiClient)
  RateLimiting/ Global . Writes . Uploads . AuthStrict . RateLimitPartitioners . RateLimitPolicies . RateLimitServiceExtensions
```

`VerifyHumanAttribute` / `IdempotentAttribute` are **markers** in Application;
the actual MVC logic lives here as global action filters that only engage on
marked actions, so controllers never reference an Infrastructure type.

### 2.4 `src/API` — composition root and delivery (29 controllers)

```
src/API/
  Controllers/  29 controllers (list)
  Extensions/   GlobalExceptionHandler . ResultExtensions (Result -> HTTP status)
  Hubs/         MessagingHub . SignalRMessageNotifier . ConversationGroup . JwtSubUserIdProvider
```

Controllers: `AgentApplications, AgentReports, AgentReviews, Agents, AiChat, Auth, Blog, Categories, Contact, ConversationEscalations, Conversations, Dashboard, DevelopmentNotifications, DevelopmentProjects, DevelopmentProjectsPublic, DevelopmentTracking, DevelopmentTrackingAdmin, DevelopmentUnits, DevelopmentUpdates, Enquiries, Images, Locations, Messages, Properties, SavedProperties, Secured, Tags, Transactions, Users`.

### 2.5 one csproj to rule them all — `src/PIPDC.csproj`

Web SDK, `net9.0` (from `Directory.Build.props`), `RootNamespace`/`AssemblyName`
= `PIPDC`, `UserSecretsId` carried from the old csprojs. On a version conflict
between the old per-layer package sets the **highest version wins**:

`Asp.Versioning.Mvc 8.1.0` + `Asp.Versioning.Mvc.ApiExplorer 8.1.0` · `CloudinaryDotNet 1.29.3` · `Google.Apis.Gmail.v1 1.69.0.3742` · `Microsoft.AspNetCore.Authentication.JwtBearer 9.0.17` · `Microsoft.AspNetCore.Identity.EntityFrameworkCore 9.0.17` · `Microsoft.AspNetCore.OpenApi 9.0.18` · `Microsoft.EntityFrameworkCore 9.0.20` · `Microsoft.EntityFrameworkCore.Design 9.0.17` (PrivateAssets) · `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 9.*` · `Microsoft.Extensions.Identity.Stores 9.0.17` · `MimeKit 4.17.0` · `Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4` · `Scalar.AspNetCore 2.16.13`.

### 2.6 `tests/PIPDC.ArchitectureTests`

xUnit project referencing the single `src/PIPDC.csproj`. `ArchitectureTests.cs`
(NetArchTest.Rules, 18 facts) walks the whole assembly from
`typeof(PIPDC.Domain.Common.Result).Assembly` with namespace prefix semantics and
asserts the section 0 graph and technology boundaries (EF Core/Mvc/SignalR/
Npgsql/Http), that controllers never depend on `IAppDbContext` or Infrastructure,
and that every `I*Service` has exactly one implementation. Pipeline proven by a
negative test (temporary Domain→Infrastructure reference is caught, then reverted).

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
                registered in src/Program.cs: AddSignalR + AddScoped<IMessageNotifier, SignalRMessageNotifier>
                src/API/Controllers/MessagesController.cs   POST .../messages [Idempotent] [EnableRateLimiting(Writes)]
```

**Why this matters:** `MessageService.SendAsync` publishes to a conversation **without ever referencing SignalR**. It calls `IMessageNotifier`; `src/Program.cs` supplies the SignalR implementation. Swap the transport tomorrow and not one line of Application code changes. The architecture tests enforce it (Application may not reference `PIPDC.API` or `Microsoft.AspNetCore.SignalR`).

The same "Application-owned interface, Infrastructure/API implementation" seam is used by `IAppDbContext`, `IUniqueViolationDetector` (Npgsql-specific unique-violation classification) and `IImageService` (Cloudinary).

---

## 4. Composition root (`src/Program.cs`) - how it all wires up

1. Kestrel 10 MB body cap; `JsonStringEnumConverter`; `AddSignalR` + `JwtSubUserIdProvider`.
2. `AddApiVersioning` (URL segment `v`), `AddApiExplorer` (per-version OpenAPI docs), `AddOpenApi` + **Scalar** UI, per-version Bearer security transformers.
3. `AddInfrastructure(config)` then `AddApplication()` then the API-only `IMessageNotifier` registration.
4. Model-state validation failures re-shape into the same `{ code, message, type }` error object everywhere.
5. Startup: migrate + seed roles outside Production; dev seeder only in Development.
6. Middleware order: exception handler -> forwarded headers -> HSTS + security headers (+dev CSP for Scalar) -> OpenAPI/Scalar (dev) -> HTTPS redirect -> CORS -> Authentication -> RateLimiting -> Authorization -> Controllers -> health-checks -> `MapHub<MessagingHub>("/hubs/messaging")`.

Cross-cutting (configured in `Program.cs`/`Infrastructure`): HSTS 365d, security headers incl. strict CSP in production, CORS named policy `AllowFrontend`, JWT (`MapInboundClaims=false`, `name`/`role` claim mapping, `access_token` query-token accepted only on `/hubs/messaging`), per-controller rate-limit policies (`Writes`, `Uploads`, `AuthStrict`, `Global`) whose names come from `RateLimitPolicies` (Infrastructure), `[Idempotent]` write deduplication (marker in Application, `IdempotencyActionFilter` in Infrastructure), DB health check, Turnstile `VerifyHumanAttribute` (marker in Application, `VerifyHumanActionFilter` in Infrastructure), AI chat via `IGeminiClient` (OpenRouter or Gemini by config).

---

## 5. Audit summary

| Area | Number |
|---|---|
| Projects in `PIPDC.sln` | 1 (application) + 1 test |
| Layer folders inside `src/` | 4 (`Domain`, `Application`, `Infrastructure`, `API`) |
| Domain entities (+ auth entities) | 28 (+ `RefreshToken`, `VerificationCode`) |
| Domain enums | 18 |
| EF `IEntityTypeConfiguration` classes | 27 |
| EF migrations (non-designer) | 31 (+ snapshot; no pending model changes) |
| Application services (impls / interfaces) | 24 / 29 `IService` interfaces |
| API controllers | 29 |
| SignalR hub / notifier | 1 / 1 (+ 2 supporting Hub types) |
| Architecture tests | 18 NetArchTest facts (`ArchitectureTests.cs`) |
| Runtime | .NET 9 (single Web SDK project) |
| Entry point | `dotnet run --project src` (https on 7123) |
| Namespaces | 40 (`PIPDC.Domain.*`, `PIPDC.Application.*`, `PIPDC.Infrastructure.*`, `PIPDC.API.*`; all 38 pre-split roots retained + `Application.Idempotency`/`Application.Captcha` markers) |

Build status (last verification on `refactor/single-project` @ `5776477`):
`dotnet build -warnaserror` 0/0; architecture tests 18/18 (incl. the negative
proof); `dotnet ef --project src migrations list` = 31 (pre-split baseline);
`migrations has-pending-model-changes` = none.