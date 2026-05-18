---
name: flare-api-patterns
description: Coding patterns extracted from flare-api — a .NET 10 Clean Architecture feature-flag management API
version: 1.0.0
source: local-git-analysis
analyzed_commits: 35
---

# Flare API Patterns

## Commit Conventions

This project uses **conventional commits** (with minor variation):

| Prefix | Usage |
|--------|-------|
| `feat:` / `feature:` | New features |
| `fix:` / `bugfix:` | Bug fixes |
| `chore:` | Maintenance, dependency updates, cleanup |
| `refactor:` | Code restructuring |

Examples from history:
```
feature: implement search and pagination on project detail, feature flag detail, user management endpoints
bugfix: create feature flag values for newly created scopes
chore: update dependencies
feat: different feature flag types
```

## Project Architecture

Clean Architecture with four projects in `src/`:

```
src/
├── Flare.Api/                      # ASP.NET Web API host
│   ├── Controllers/
│   │   ├── WebUI/                  # Cookie-auth endpoints for the front-end
│   │   └── Sdk/                    # API-key endpoints for SDK consumers
│   ├── Extensions/                 # ServiceCollectionRegistration.cs (Api-layer DI)
│   ├── Middleware/                 # GlobalExceptionHandler
│   ├── RateLimiting/               # Rate limit options, policies, conventions
│   ├── Attributes/                 # Custom authorization attributes
│   ├── Filters/                    # Custom authorization filters
│   ├── Startup.cs                  # ConfigureServices + Configure (classic style)
│   └── Program.cs                  # Host builder + pre-run startup sequence
│
├── Flare.Application/              # Use-cases, interfaces, DTOs
│   ├── DTOs/                       # Input/output data transfer objects
│   ├── Interfaces/                 # Service + repository interfaces
│   ├── Services/                   # Business logic implementations
│   ├── Authorization/Handlers/     # ASP.NET authorization requirement handlers
│   └── ServiceCollectionRegistration.cs
│
├── Flare.Domain/                   # Pure domain model — no DI dependencies
│   ├── Entities/                   # EF Core entities with domain behaviour
│   ├── Enums/                      # Domain enumerations
│   ├── Exceptions/                 # Domain-specific exceptions
│   └── Constants/                  # Domain constants (e.g. AuthConstants)
│
└── Flare.Infrastructure/           # EF Core, Postgres, migrations, seed
    ├── Data/
    │   ├── ApplicationDbContext.cs
    │   ├── Configurations/         # One IEntityTypeConfiguration<T> per entity
    │   └── Repositories/
    │       ├── Implementation/     # Concrete repository classes
    │       └── Interfaces/         # Repository contracts
    ├── Initialization/             # DatabaseInitializer, MigrationRunner
    ├── Migrations/                 # EF Core generated migrations
    └── ServiceCollectionRegistration.cs
```

## Key Patterns

### Controllers

- Always `[ApiController]`, `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}")]`
- Decorate every action with `[ProducesResponseType]` for all expected status codes
- Extract `userId` from `HttpContext.GetCurrentUserId()` — never trust route params for identity
- Return `Created()` (no body) for POST create actions; `Ok(result)` for reads

### Pagination

Always use `PagedResult<T>` for list endpoints. Clamp page/pageSize in the controller:

```csharp
[FromQuery] int page = 1,
[FromQuery] int pageSize = 20,
[FromQuery] string? search = null

if (page < 1) page = 1;
if (pageSize < 1) pageSize = 1;
if (pageSize > 25) pageSize = 25;
```

`PagedResult<T>` shape:
```csharp
public class PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
```

### Repositories

- One interface per aggregate in `Interfaces/`, one implementation in `Implementation/`
- Constructor-inject `ApplicationDbContext` (never `IUnitOfWork` directly in repos)
- Name query methods descriptively: `GetByIdWithScopesAndProjectAsync`, `GetPagedByProjectIdAsync`
- Use `.Include().ThenInclude()` chains for eager loading; avoid lazy loading

```csharp
public async Task<FeatureFlag?> GetByIdWithValuesAsync(Guid featureFlagId)
{
    return await _context.FeatureFlags
        .Include(f => f.Values)
            .ThenInclude(v => v.Scope)
        .Include(f => f.Values)
            .ThenInclude(v => v.TargetingRules)
                .ThenInclude(r => r.Conditions)
        .FirstOrDefaultAsync(f => f.Id == featureFlagId);
}
```

### Domain Entities

- Place domain behaviour directly on entities (not in services) where it's purely internal
- Use `switch` expressions for type-dispatch factory methods

```csharp
public FeatureFlagValue CreateValueForScope(Guid scopeId) => Type switch
{
    FeatureFlagType.Boolean => FeatureFlagValue.ForBoolean(Id, scopeId, false),
    FeatureFlagType.String  => FeatureFlagValue.ForString(Id, scopeId, null),
    FeatureFlagType.Number  => FeatureFlagValue.ForNumber(Id, scopeId, null),
    FeatureFlagType.Json    => FeatureFlagValue.ForJson(Id, scopeId, null),
    _ => throw new ArgumentOutOfRangeException(nameof(Type), Type, "Unsupported flag type.")
};
```

### EF Core Configurations

- One `IEntityTypeConfiguration<TEntity>` class per entity in `Data/Configurations/`
- Use snake_case column/table names (Npgsql convention)
- All migrations go in `Flare.Infrastructure/Migrations/` via EF CLI

### Dependency Registration

Each layer owns its own `ServiceCollectionRegistration.cs` with an `AddXxx(this IServiceCollection)` extension. Layers are registered top-down from `Startup.cs`:

```
Startup.cs → Api extensions → Application → Infrastructure
```

### Startup Sequence (Program.cs)

Always run in this order before `app.Run()`:
1. `MigrationRunner.RunAsync()` — advisory lock + `MigrateAsync()`
2. `DatabaseInitializer.InitializeAsync()` — seed admin if no users exist

### Exception Handling

Use `GlobalExceptionHandler` middleware to map domain exceptions to HTTP status codes. Domain-specific exceptions live in `Flare.Domain/Exceptions/`.

### Security

- Cookie-based auth for WebUI; API-key auth for SDK endpoints
- Brute-force protection on auth endpoints (lockout fields on `User` entity)
- Rate limiting on SDK evaluation endpoints via `SdkEvaluationRateLimiterPolicy`
- Authorization via ASP.NET `IAuthorizationRequirement` handlers in `Application/Authorization/Handlers/`

## Workflow: Adding a New Feature

1. **Domain** — add/update entity in `Flare.Domain/Entities/`, enum in `Flare.Domain/Enums/`
2. **Infrastructure** — add `IEntityTypeConfiguration`, update `ApplicationDbContext`, add repository interface + implementation, run `dotnet ef migrations add <Name>`
3. **Application** — define DTOs in `Flare.Application/DTOs/`, add method to service interface, implement in service
4. **API** — add action to relevant controller in `Controllers/WebUI/` or `Controllers/Sdk/`
5. **DI** — register new services in the owning layer's `ServiceCollectionRegistration.cs`

## Workflow: Adding a Migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Flare.Infrastructure \
  --startup-project src/Flare.Api
```

Migrations run automatically at startup via `MigrationRunner` with an advisory lock — no manual `dotnet ef database update` in production.
