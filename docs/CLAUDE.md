# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Architecture

See [SKILL.md](./SKILL.md) for the full project structure, layer responsibilities, controller patterns, repository conventions, EF Core configuration rules, and step-by-step feature/migration workflows.

Key specifics not covered there:

### Controller families

- **`Controllers/WebUI/`** — use `[Authorize(Policy = ...)]` with policies defined in `AuthorizationPolicies`.
- **`Controllers/Sdk/`** — use `[BearerApiKeyAuthorization]` (OpenFeature-compatible). Rate-limited via `SdkRateLimitingConvention` / `SdkEvaluationRateLimiterPolicy`.

### Authentication & authorization

- WebUI: cookie auth (`FlareAuth`, HttpOnly, Secure, SameSite=Strict, 7-day sliding expiry).
- SDK: `BearerApiKeyAuthorizationFilter` validates the project API key from the `Authorization: Bearer <key>` header and populates `HttpContext.Items[HttpContextKeys.ProjectId]` and `HttpContext.Items[HttpContextKeys.ProjectAlias]`.
- Two-level permission model:
  - **Project-level** — `ProjectPermission` enum, checked via `ProjectPermissionRequirement` / `ProjectPermissionRequirementHandler`.
  - **Scope-level** — `ScopePermission` enum, checked via `ScopePermissionRequirement` / `ScopePermissionRequirementHandler`.

### Data layer

- `IUnitOfWork` / `UnitOfWork` wraps `SaveChangesAsync`.


### Package management

All NuGet versions are centrally managed in `Directory.Packages.props`. Add new packages with a `<PackageVersion>` entry there and a version-less `<PackageReference>` in the `.csproj`.
