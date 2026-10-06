# PIPDC — Backend Architecture Notes

How the API is wired: entity → controller, the DbContext approach, dependency
injection, folder layout, and the interface/service contract. Two entities are
traced end to end (`DevelopmentUnit` and `Property`); the pattern is the same
everywhere else.

---

## 1. The one structural fact to know first

**There are four projects**, all `net9.0`, gathered in `PIPDC.sln` and sharing
the root `Directory.Build.props` (`Nullable`, `ImplicitUsings`):

| Project | Layer | Referenced by | References |
|---|---|---|---|
| `src/Domain/PIPDC.Domain.csproj` | Domain | every other project | none |
| `src/Application/PIPDC.Application.csproj` | Application | Infrastructure, API | Domain |
| `src/Infrastructure/PIPDC.Infrastructure.csproj` | Infrastructure | API | Application, Domain |
| `src/API/PIPDC.API.csproj` | API (Web SDK) | tests | Application, Infrastructure |

```
src/
├── Domain/          PIPDC.Domain.*          entities, enums, Result/Error
├── Application/     PIPDC.Application.*     interfaces, services, DTOs, query params
├── Infrastructure/  PIPDC.Infrastructure.*  EF Core, auth, email, AI clients, rate limiting
└── API/             PIPDC.API.*             controllers, SignalR hubs, Program.cs
```

The dependency direction is **compiler-enforced** by the `ProjectReference`
graph — a wrong-way `using` is now a build error, not a convention:

```
API ──────────► Application ──────► Domain
  │                                   ▲
  └──────► Infrastructure ────────────┘
               (Infrastructure also implements Application's IAppDbContext)
```

| From / To | Domain | Application | Infrastructure | API |
|---|---|---|---|---|
| Domain | — | none | none | none |
| Application | yes | — | none | none |
| Infrastructure | yes | yes | — | none |
| API | yes | yes | yes | — |

When the four layers were folders inside one project, this table had to be
verified by reading the `using` statements, and it was violated in two spots
(see section 11). The split turned every forbidden cell above into a compile
error. `tests/PIPDC.ArchitectureTests` restates the graph as executable rules
(section 12) so a careless `ProjectReference` fails loudly.

---

## 2. Folder structure and how files are grouped

Layers are split by folder; **inside `Application` the split is by feature**,
and each feature folder is self-contained:

```
src/Application/
├── DependencyInjection.cs        ← one file: every I*Service → *Service
├── Data/IAppDbContext.cs         ← the EF contract the Application layer sees
├── Common/                       ← PaginatedResult, PublicVisibility, …
├── Auth/                         ← Roles constants
├── Properties/                   ← feature example 2
│   ├── IPropertyService.cs           the contract
│   ├── PropertyService.cs            the implementation
│   ├── PropertyQueryParameters.cs    binding model for [FromQuery]
│   ├── Dtos.cs                       request/response records
│   ├── PropertyMappers.cs            entity → DTO
│   └── PropertyStatusDisplay.cs      display helpers
└── Developments/                 ← feature example 1
    ├── IDevelopmentUnitService.cs
    ├── DevelopmentUnitService.cs
    ├── IDevelopmentProjectService.cs / DevelopmentProjectService.cs
    ├── DevelopmentListingPromoter.cs ← shared collaborator, injected into 2 services
    ├── DevelopmentProjectQueryParameters.cs
    ├── Dtos.cs
    └── …
```

**Rules of thumb:**

- One interface + one implementation per service, both in the same feature folder.
- `Dtos.cs` per feature holds the request and response records.
- Query-binding classes live next to the services, not in `API`.
- Anything shared across features moves to `Application/Common` or
  `Application/Services`.
- Controllers are **only** in `src/API/Controllers`, one per aggregate.
- EF entity configurations are **only** in `src/Infrastructure/Data/Configurations`.

Infrastructure is grouped by technical concern, not feature:

```
src/Infrastructure/
├── DependencyInjection.cs     ← DbContext, Identity, JWT, CORS, email, AI, rate limits
├── Data/
│   ├── AppDbContext.cs
│   ├── Configurations/*.Configuration.cs    (27 files, auto-discovered)
│   ├── Migrations/*.cs
│   └── *Seeder.cs
├── Auth/  Captcha/  Email/  Gemini/  OpenRouter/  Idempotency/  RateLimiting/  HealthChecks/
```

---

## 3. The chain: how an entity reaches a controller

Seven hops, always in this order:

```
1. Domain/Entities/X.cs                              the POCO, no EF attributes
2. Infrastructure/Data/Configurations/XConfiguration.cs   IEntityTypeConfiguration<X>
3. Application/Data/IAppDbContext.cs                 DbSet<X> { get; }
4. Infrastructure/Data/AppDbContext.cs               DbSet<X> => Set<X>();
5. Application/<Feature>/IXService.cs                the contract, returns Result<T>
6. Application/DependencyInjection.cs                AddScoped<IXService, XService>()
7. API/Controllers/XController.cs                    [Route] + action → service → ToActionResult()
```

Adding a new entity means touching steps 1-6 plus a controller; step 3 and step 4
must both be updated or the property simply does not exist on the interface.

---

## 4. DbContext approach

**The contract lives in Application; the implementation lives in Infrastructure.**

`src/Application/Data/IAppDbContext.cs` — this is what every service injects:

```csharp
public interface IAppDbContext
{
    DbSet<Property> Properties { get; }
    DbSet<DevelopmentUnit> DevelopmentUnits { get; }
    // … every entity, one line each …

    /// Exposed so a service that must make several writes land together can open
    /// a real transaction (e.g. a sale record + the property status change).
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

`src/Infrastructure/Data/AppDbContext.cs`:

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options), IAppDbContext
{
    public DbSet<DevelopmentUnit> DevelopmentUnits => Set<DevelopmentUnit>();
    // …
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);  // ← auto-discovery
        // … seed data …
        // … loop applying UTC ValueConverter to every DateTime property …
    }
}
```

Three things worth noticing:

1. **Expression-bodied `Set<T>()`** — no manual mapping, and it satisfies the
   interface member directly.
2. **`ApplyConfigurationsFromAssembly`** — the 27 files in
   `Data/Configurations/` are picked up by convention; none is registered by hand.
3. **Global UTC converter** applied by reflection over `builder.Model`, so
   `DateTime` is forced to `Kind = Utc` on both read and write.

Registration, `src/Infrastructure/DependencyInjection.cs:28-31`:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
```

Both resolve to the **same scoped instance** within a request. Note that
`IAppDbContext` is a *narrow* interface: no `ChangeTracker`, no `Add`/`Remove`,
no `Set<T>()`. Services add through `dbContext.Properties.Add(...)` because the
property is already a `DbSet<T>`, but they can never reach the rest of the
DbContext surface.

**Entity configuration convention** (`DevelopmentUnitConfiguration`):

```csharp
builder.Property(u => u.Status).HasConversion<string>();          // enums stored as text
builder.Property(u => u.Price).HasColumnType("decimal(18,2)");
builder.Property(u => u.Amenities).HasColumnType("text[]");        // PostgreSQL array
builder.HasIndex(u => new { u.DevelopmentProjectId, u.UnitIdentifier }).IsUnique();
builder.HasOne(u => u.Project).WithMany(p => p.Units)
       .HasForeignKey(u => u.DevelopmentProjectId).OnDelete(DeleteBehavior.Cascade);
```

Migration behaviour: `Program.cs` runs `dbContext.Database.MigrateAsync()` on
startup **only outside Production**, then `RoleSeeder.SeedAsync`, then (in
Development) `DevelopmentSeeder.SeedAsync`.

---

## 5. DI approach

Two extension methods, both in a static class named `DependencyInjection`:

| File | Method | Registers |
|---|---|---|
| `src/Application/DependencyInjection.cs` | `AddApplication()` | every `I*Service → *Service`, all `Scoped` |
| `src/Infrastructure/DependencyInjection.cs` | `AddInfrastructure(IConfiguration)` | DbContext, Identity, JWT, CORS, email, AI clients, rate limiting, health checks, the `IUniqueViolationDetector` seam (`PostgresUniqueViolationDetector`) |

Called from `src/API/Program.cs:74-75`, infrastructure first:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
```

SignalR is wired in Program.cs too, next to that call:

```csharp
builder.Services.AddSignalR();                                          // the hub framework, API-only
builder.Services.AddScoped<IMessageNotifier, SignalRMessageNotifier>(); // the push seam Application uses
```

`AddApplication()` registers the feature services; it deliberately does **not**
know about SignalR. The push path is one interface, registered above: the
*interface* is Application-owned (`src/Application/Conversations/IMessageNotifier.cs`),
the *implementation* is API-owned (`src/API/Hubs/SignalRMessageNotifier.cs`), so
Application services can notify the hub without ever naming
`Microsoft.AspNetCore.SignalR`.

`AddApplication()` in full is just a flat list of 27 registrations:

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddScoped<IPropertyService, PropertyService>();
    services.AddScoped<IDevelopmentUnitService, DevelopmentUnitService>();
    services.AddScoped<IDevelopmentListingPromoter, DevelopmentListingPromoter>();
    // … 26 more, one line each …
    return services;
}
```

**Lifetime rules used in this codebase:**

- `Scoped` — everything that touches `IAppDbContext` (all feature services).
- `Scoped` — `IAgentLicenseGenerator`, with an inline comment explaining why it
  must *not* be a singleton (it queries the DbContext; a singleton would be a
  captive dependency).
- `Singleton` — `EmailQueue` plus a forwarding `IEmailQueue` registration, so the
  queue survives across requests.
- `Singleton` (hosted) — `EmailQueueWorker` drains that queue off-request.
- Typed `HttpClient` — `TurnstileVerifier`, and `IGeminiClient` (either
  `GeminiClient` or `OpenRouterClient`, chosen by `AiChat:Provider`).
- `AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>())` —
  forwards to the already-registered concrete context rather than creating a second.
- `AddScoped<IUniqueViolationDetector, PostgresUniqueViolationDetector>()` —
  lets Application classify a unique-constraint race without naming Npgsql; the
  SQLSTATE test stays in Infrastructure.

**Injection style: C# primary constructors everywhere.** No `private readonly`
fields, no constructor bodies:

```csharp
public class DevelopmentUnitService(
    IAppDbContext dbContext,
    IDevelopmentListingPromoter listingPromoter) : IDevelopmentUnitService

public class PropertyService(IAppDbContext dbContext, IImageService imageService) : IPropertyService

public class DevelopmentUnitsController(IDevelopmentUnitService unitService) : ControllerBase

public class MessagingHub(IAppDbContext dbContext) : Hub
```

---

## 6. Interface contract

One interface per feature service, sitting in the same folder as its
implementation, e.g. `src/Application/Developments/IDevelopmentUnitService.cs`:

```csharp
public interface IDevelopmentUnitService
{
    Task<Result<IReadOnlyList<DevelopmentUnitDto>>> GetByProjectAsync(int projectId, CancellationToken ct);
    Task<Result<DevelopmentUnitDto>> CreateAsync(int projectId, CreateDevelopmentUnitRequest request, CancellationToken ct);
    Task<Result<DevelopmentUnitDto>> UpdateAsync(int projectId, int unitId, UpdateDevelopmentUnitRequest request, CancellationToken ct);
    Task<Result<DevelopmentUnitDto>> PromoteAsync(int projectId, int unitId, CancellationToken ct);
    Task<Result> DeleteAsync(int projectId, int unitId, CancellationToken ct);
}
```

Contract rules:

1. **Every method returns `Result` or `Result<T>`** (`Domain/Common/Result.cs`).
   Business failure is a value, never an exception. Accessing `.Value` on a
   failure throws by design.
2. **Every method takes `CancellationToken ct` last.**
3. **The parent id is an explicit parameter** — nesting lives in the signature,
   not in a navigation property lookup.
4. **Identity is passed in, never resolved inside the service.** Where a service
   acts on behalf of the caller, `currentUserId` / `currentUserRoles` are
   parameters supplied by the controller from JWT claims.

`Error` (`Domain/Common/Error.cs`) is a record with static factories, each
carrying a machine-readable code:

```csharp
Error.NotFound("unit.notfound", $"Development unit with id {unitId} was not found …")
Error.Validation("property.transactionrequired", "Record the sale for this property before …")
Error.Conflict("unit.conflict", $"A unit with identifier '{x}' already exists …")
Error.Concurrency()
```

`ErrorType` is mapped to an HTTP status in one place —
`src/API/Extensions/ResultExtensions.cs`:

| `ErrorType` | Status |
|---|---|
| NotFound | 404 |
| Validation | 400 |
| Conflict | 409 |
| Unauthorized | 401 |
| Forbidden | 403 |
| Failure | 500 |

`ToActionResult()` on a non-generic `Result` returns **204 No Content** on
success; the generic overload returns **200 with the value**.

---

## 7. Service

The implementation holds all business rules and knows nothing about HTTP.

```csharp
public class DevelopmentUnitService(
    IAppDbContext dbContext,
    IDevelopmentListingPromoter listingPromoter) : IDevelopmentUnitService
{
    public async Task<Result<DevelopmentUnitDto>> UpdateAsync(
        int projectId, int unitId, UpdateDevelopmentUnitRequest request, CancellationToken ct)
    {
        var unit = await dbContext.DevelopmentUnits
            .FirstOrDefaultAsync(u => u.Id == unitId && u.DevelopmentProjectId == projectId, ct);

        if (unit is null)
            return Result<DevelopmentUnitDto>.Failure(
                Error.NotFound("unit.notfound", $"Development unit with id {unitId} was not found in project {projectId}."));

        if (await dbContext.DevelopmentUnits.AnyAsync(
                u => u.DevelopmentProjectId == projectId
                  && u.UnitIdentifier == request.UnitIdentifier
                  && u.Id != unitId, ct))
            return Result<DevelopmentUnitDto>.Failure(
                Error.Conflict("unit.conflict", $"A unit with identifier '{request.UnitIdentifier}' already exists in this project."));

        unit.UnitIdentifier = request.UnitIdentifier;
        // … mutate …
        await dbContext.SaveChangesAsync(ct);

        return Result<DevelopmentUnitDto>.Success(ToDto(unit));
    }
}
```

Characteristics:

- Query, guard, mutate, `SaveChangesAsync`, map to DTO, return `Result`.
- **Ownership is part of the query** (`u.DevelopmentProjectId == projectId`), so
  a cross-project id is a 404 rather than a 403.
- DTO mapping is a `private static ToDto(...)` inside the service (or a separate
  `*Mappers.cs` for the bigger features).
- Cross-cutting rules are extracted into shared collaborators rather than copied:
  `DevelopmentListingPromoter` is injected into both the project and unit
  services so "what makes a unit listable" is defined once.
- Public-visibility rules are extracted into `Application/Common/PublicVisibility.cs`
  extension methods (`VisibleAgents()`, `VisibleProperties()`) applied explicitly
  per query — deliberately **not** as EF global query filters, because a global
  filter would silently change every admin-facing read.

---

## 8. Controller

```csharp
[Authorize(Roles = "Admin")]                 // omitted for public endpoints
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/development-projects/{projectId:int}/units")]
public class DevelopmentUnitsController(IDevelopmentUnitService unitService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByProject(int projectId, CancellationToken ct)
    {
        var result = await unitService.GetByProjectAsync(projectId, ct);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create(int projectId, [FromBody] CreateDevelopmentUnitRequest request, CancellationToken ct)
    {
        var result = await unitService.CreateAsync(projectId, request, ct);
        if (result.IsFailure) return result.ToActionResult();
        return CreatedAtAction(nameof(GetByProject), new { projectId }, result.Value);
    }

    [HttpPut("{unitId:int}")]
    public async Task<IActionResult> Update(int projectId, int unitId, [FromBody] UpdateDevelopmentUnitRequest request, CancellationToken ct)
        => (await unitService.UpdateAsync(projectId, unitId, request, ct)).ToActionResult();

    [HttpPost("{unitId:int}/promote")]   // custom action after the {id} segment
    public async Task<IActionResult> Promote(int projectId, int unitId, CancellationToken ct)
        => (await unitService.PromoteAsync(projectId, unitId, ct)).ToActionResult();
}
```

Controller rules:

- Thin: bind, call service, `ToActionResult()`. No business logic, no DbContext.
- Route version comes from the URL segment (`UrlSegmentApiVersionReader`);
  bumping a version means adding `[ApiVersion]` and a new route segment.
- `CancellationToken` is passed straight through to the service.
- `201` uses `CreatedAtAction` so the `Location` header points at the read endpoint.

**Identity from claims.** `PropertiesController` shows the pattern for endpoints
that need the caller's identity:

```csharp
private string? CurrentUserId => User.Identity?.IsAuthenticated == true
    ? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
    : null;

private IList<string> CurrentUserRoles => User.FindAll("role").Select(c => c.Value).ToList();

private bool IncludeSuspendedAgents => CurrentUserRoles.Contains(Roles.Admin);
```

Never bound from a query string. The neighbouring comment states the rule
explicitly: "Derived from the caller's own role claim, never from a query
parameter."

---

## 9. Two worked examples

### Example A — `DevelopmentUnit` (admin-only, nested aggregate)

| Hop | File |
|---|---|
| Entity | `src/Domain/Entities/DevelopmentUnit.cs` — `: AuditableEntity`, adds `PropertyId` link, listing-descriptor fields, `DevelopmentProject Project` navigation |
| Configuration | `src/Infrastructure/Data/Configurations/DevelopmentUnitConfiguration.cs` — enums to string, `text[]` amenities, unique `(DevelopmentProjectId, UnitIdentifier)`, unique `PropertyId`, cascade from project, `SetNull` to property |
| Contract | `src/Application/Data/IAppDbContext.cs` → `DbSet<DevelopmentUnit>` |
| Context | `src/Infrastructure/Data/AppDbContext.cs` → `Set<DevelopmentUnit>()` |
| Interface | `src/Application/Developments/IDevelopmentUnitService.cs` |
| Service | `src/Application/Developments/DevelopmentUnitService.cs` (also gets `IDevelopmentListingPromoter`) |
| DI | `AddScoped<IDevelopmentUnitService, DevelopmentUnitService>()` plus `AddScoped<IDevelopmentListingPromoter, DevelopmentListingPromoter>()` |
| DTOs | `src/Application/Developments/Dtos.cs` → `DevelopmentUnitDto` |
| Controller | `src/API/Controllers/DevelopmentUnitsController.cs` |
| HTTP | `GET/POST /api/v1/development-projects/{projectId}/units`<br>`PUT/DELETE /api/v1/development-projects/{projectId}/units/{unitId}`<br>`POST /api/v1/development-projects/{projectId}/units/{unitId}/promote` |

Auth is `[Authorize(Roles = "Admin")]` on the class. The aggregate's parent id
(`projectId`) appears in both the route **and** every service call.

### Example B — `Property` (public reads, per-caller visibility)

| Hop | File |
|---|---|
| Entity | `src/Domain/Entities/Property.cs` |
| Configuration | `src/Infrastructure/Data/Configurations/PropertyConfiguration.cs` |
| Contract / Context | `IAppDbContext.Properties` / `AppDbContext.Properties` |
| Interface | `src/Application/Properties/IPropertyService.cs` |
| Service | `src/Application/Properties/PropertyService.cs` (plus `PropertyMappers`, `PropertyStatusDisplay`, `PropertyTypeDisplay`) |
| DI | `AddScoped<IPropertyService, PropertyService>()` |
| Controller | `src/API/Controllers/PropertiesController.cs` |
| HTTP | `GET /api/v1/properties`, `/featured`, `/nearby`, `/slug/{slug}`, `/{id}` … plus write endpoints |

What distinguishes it from Example A:

- **No `[Authorize]` on the class** — public reads are anonymous; individual
  actions like `GetNearby` carry `[Authorize]` themselves.
- The service signature carries `string? currentUserId`,
  `bool includeSuspendedAgents` and `IList<string> currentUserRoles` on every
  method, because visibility and ownership depend on who is asking.
- `includeSuspendedAgents` is computed in the controller from the role claim and
  documented as never being bindable from the query string.
- Ownership checks use a shared `VerifyOwnershipAsync(...)` helper, and status
  changes are guarded by a shared rule
  (`RequireTransactionForDealStatusAsync` — a property cannot be marked Sold or
  Rented before the sale or rental is recorded).

---

## 10. Cross-cutting conventions

- **Enums serialize as strings.** `Program.cs` adds `JsonStringEnumConverter`
  globally; enums are additionally stored as text in Postgres via
  `HasConversion<string>()`.
- **Pagination** via `PaginatedResult<T>` (`Application/Common/PaginatedResult.cs`)
  plus a `*QueryParameters` class that clamps `PageNumber >= 1` and
  `1 <= PageSize <= 100` in its property setters, so bad input never reaches SQL.
- **Concurrency** uses `xmin` tokens; conflicts surface as `Error.Concurrency()`
  which becomes HTTP 409.
- **Optimistic, not automatic**: features that change public state (publishing a
  listing, filing a sale) are separate explicit actions, never a side effect of
  an unrelated save.
- **Business rules live next to the data they protect**, expressed as predicates
  in `Application/Common/PublicVisibility.cs` rather than as global query filters.

---

## 11. Known deviations (honest notes)

The three deviations this section used to list are **fixed**; they are kept as a
record of what the split changed, and the remaining notes are honest ones.

1. **`Application` → `API.Hubs` — fixed.** `MessageService.cs` and
   `ConversationEscalationService.cs` used to inject `IHubContext<MessagingHub>`
   directly. They now go through the Application-owned `IMessageNotifier`
   interface, implemented in `src/API/Hubs/SignalRMessageNotifier.cs`. Grep
   confirms Application has zero `Microsoft.AspNetCore.SignalR` / `PIPDC.API`
   references.
2. **Layering by convention — fixed by the split.** The four folders are now
   four projects, so a wrong-way `using` or `ProjectReference` is a compile
   error (section 1). The architecture tests in section 12 re-assert the same
   graph at test time.
3. **Migration namespace anomaly — fixed.** Generated migrations used to carry
   `PIPDC.src.Infrastructure.Data.Migrations`, an artifact of an early
   `src/PIPDC/src/…` layout. All 31 files + `AppDbContextModelSnapshot.cs` now
   use `PIPDC.Infrastructure.Data.Migrations`, matching hand-written code. Only
   the namespace declarations were rewritten; migration **content** (up/down
   operations) was not touched, so the database schema contract is unchanged.
   `dotnet ef migrations list` matches the pre-split baseline exactly and
   `dotnet ef migrations has-pending-model-changes` reports no model changes.
4. **`appsettings.json` lives in two places.** EF tools and Visual Studio resolve
   content root against the project directory, so `src/API/appsettings*.json`
   exist for them; launching `src/API/bin/…/PIPDC.API.exe` (or `dotnet run`)
   from the repo root resolves content root against the working directory, so
   the root copies are retained. The two sets are tracked and currently
   byte-identical; keep them in sync. Production ships the `src/API` copies
   (they ride along in the build output).
5. **Build output moved.** Binary output is no longer a single root `bin/`; each
   project has its own, e.g. `src/API/bin/Debug/net9.0/PIPDC.API.exe`.

---

## 12. Architecture tests

`tests/PIPDC.ArchitectureTests` (xUnit; run with `dotnet test
tests/PIPDC.ArchitectureTests`) walks the metadata of each layer assembly and
asserts:

- `PIPDC.Domain` references nothing from any other layer.
- `PIPDC.Application` references nothing from `PIPDC.Infrastructure`,
  `PIPDC.API`, or `Microsoft.AspNetCore.SignalR`.
- `PIPDC.Infrastructure` references nothing from `PIPDC.API`.
- Controllers (`PIPDC.API.Controllers`) take only Application service interfaces
  as constructor dependencies — no `IAppDbContext`, no Infrastructure service
  types, no Domain entities.
- Positive controls assert the edges that **must** exist (Application → Domain,
  Infrastructure → Application/Domain, API → Application/Infrastructure), so a
  scanner blind spot can never make the negative rules pass vacuously.

The scanner inspects every `TypeReference` the compiler emitted into the
assembly (fields, method bodies, attributes, generic arguments) and compares
namespace roots with prefix semantics. `NetArchTest.Rules` was tried first and
rejected: its `HaveDependencyOn` matches an *exact* namespace string, while
every layer is spread across sub-namespaces (`PIPDC.Application.Properties`, …),
so a genuine `Application → PIPDC.Infrastructure.X` reference would dodge it.
The in-box `System.Reflection.Metadata` scanner has no such blind spot and adds
no package.
