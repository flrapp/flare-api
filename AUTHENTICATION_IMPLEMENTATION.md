# Authentication and Authorization Implementation Summary

## Overview
Implemented cookie-based session authentication for ASP.NET Core Web API without Identity framework, including role-based authorization for both global and project-level permissions.

## Architecture

### Layer Structure
- **Domain**: Entities and enums (no dependencies)
- **Application**: Interfaces and DTOs (references Domain only)
- **Infrastructure**: Service implementations and authorization handlers (references Domain and Application)
- **API**: Controllers and configuration (references Application and Infrastructure)

## Implemented Components

### 1. Domain Entities (Already Existed)
Located in: `src/Flare.Domain/Entities/`

- **User**: Id, Email, PasswordHash, FullName, GlobalRole, IsActive, CreatedAt, LastLoginAt
- **Project**: Id, Name, Description, CreatedBy, IsArchived, CreatedAt, UpdatedAt
- **ProjectMember**: Id, ProjectId, UserId, ProjectRole, InvitedBy, JoinedAt
- **Invitation**: Id, ProjectId, InvitedEmail, InvitedBy, Token, Status, ExpiresAt

### 2. Enums (Already Existed)
Located in: `src/Flare.Domain/Enums/`

- **GlobalRole**: User, Admin
- **ProjectRole**: Viewer, Editor, Owner
- **InvitationStatus**: Pending, Accepted, Expired, Cancelled

### 3. Application Layer DTOs
Located in: `src/Flare.Application/DTOs/`

- **LoginDto**: Email, Password
- **RegisterDto**: Email, Password, FullName
- **AuthResultDto**: UserId, Email, FullName, GlobalRole

### 4. Application Layer Interfaces
Located in: `src/Flare.Application/Interfaces/`

#### IAuthService
- `LoginAsync(LoginDto)`: Authenticate user and return auth result
- `RegisterAsync(RegisterDto)`: Register new user
- `GetUserByIdAsync(Guid)`: Retrieve user by ID
- `GetUserByEmailAsync(string)`: Retrieve user by email
- `UpdateLastLoginAsync(Guid)`: Update last login timestamp
- `HashPassword(string)`: Hash password using BCrypt
- `VerifyPassword(string, string)`: Verify password against hash

#### IPermissionService
- `GetUserProjectRoleAsync(Guid userId, Guid projectId)`: Get user's role in project
- `HasProjectAccessAsync(Guid userId, Guid projectId, ProjectRole minimumRole)`: Check if user has minimum role
- `IsProjectOwnerAsync(Guid userId, Guid projectId)`: Check if user is project owner
- `IsProjectMemberAsync(Guid userId, Guid projectId)`: Check if user is project member
- `CanManageProjectAsync(Guid userId, Guid projectId)`: Check if user can manage project (Owner or Editor)

### 5. Infrastructure Services
Located in: `src/Flare.Infrastructure/Services/`

#### AuthService
- Implements IAuthService
- Uses BCrypt with work factor 12 for password hashing
- Validates user credentials
- Manages user registration
- Checks user active status

#### PermissionService
- Implements IPermissionService
- Queries database for project memberships
- Evaluates role-based permissions
- Supports hierarchical role checks (Owner > Editor > Viewer)

### 6. Authorization Infrastructure
Located in: `src/Flare.Infrastructure/Authorization/`

#### Requirements
- **AdminRequirement**: Requires GlobalRole.Admin
- **ProjectAccessRequirement**: Requires minimum ProjectRole
- **ProjectOwnerRequirement**: Requires ProjectRole.Owner

#### Handlers
- **AdminRequirementHandler**: Validates admin role from claims
- **ProjectAccessRequirementHandler**: Validates project role from route/query parameters
- **ProjectOwnerRequirementHandler**: Validates project ownership

#### Policies
Defined in `AuthorizationPolicies.cs`:
- **AdminOnly**: Requires admin global role
- **ProjectViewer**: Requires at least Viewer role
- **ProjectEditor**: Requires at least Editor role
- **ProjectOwner**: Requires Owner role

### 7. Authentication Extensions
Located in: `src/Flare.Infrastructure/Extensions/AuthenticationExtensions.cs`

Helper methods for authentication operations:
- `SignInUserAsync(HttpContext, AuthResultDto)`: Create authentication cookie
- `SignOutUserAsync(HttpContext)`: Remove authentication cookie
- `GetCurrentUserId(HttpContext)`: Extract user ID from claims

### 8. Cookie Authentication Configuration
Located in: `src/Flare.Api/Startup.cs`

#### Cookie Settings
- **Name**: FlareAuth
- **HttpOnly**: true (prevents JavaScript access)
- **Secure**: Always (HTTPS only)
- **SameSite**: Strict (CSRF protection)
- **Expiration**: 7 days with sliding expiration
- **Login/Logout Paths**: Configured for API endpoints
- **Events**: Returns 401/403 status codes instead of redirects

#### Claims Structure
- **NameIdentifier**: User GUID
- **Email**: User email
- **Name**: User full name
- **Role**: GlobalRole (User/Admin)

### 9. Entity Framework Configurations
Located in: `src/Flare.Infrastructure/Data/Configurations/`

All entities have proper configurations:
- **UserConfiguration**: Unique email index, enum conversions
- **ProjectConfiguration**: Foreign key relationships
- **ProjectMemberConfiguration**: Composite unique index (ProjectId, UserId)
- **InvitationConfiguration**: Unique token index

## Usage Examples

### Controller Authentication
```csharp
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        var result = await _authService.LoginAsync(loginDto);
        if (result == null)
            return Unauthorized();

        await HttpContext.SignInUserAsync(result);
        await _authService.UpdateLastLoginAsync(result.UserId);
        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        var result = await _authService.RegisterAsync(registerDto);
        await HttpContext.SignInUserAsync(result);
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutUserAsync();
        return NoContent();
    }
}
```

### Controller Authorization
```csharp
// Require authentication
[Authorize]
public class ProjectsController : ControllerBase { }

// Require admin role
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminController : ControllerBase { }

// Require project viewer role
[Authorize(Policy = AuthorizationPolicies.ProjectViewer)]
[HttpGet("{projectId}")]
public async Task<IActionResult> GetProject(Guid projectId) { }

// Require project editor role
[Authorize(Policy = AuthorizationPolicies.ProjectEditor)]
[HttpPut("{projectId}")]
public async Task<IActionResult> UpdateProject(Guid projectId) { }

// Require project owner role
[Authorize(Policy = AuthorizationPolicies.ProjectOwner)]
[HttpDelete("{projectId}")]
public async Task<IActionResult> DeleteProject(Guid projectId) { }
```

### Manual Permission Checking
```csharp
public class ProjectService
{
    private readonly IPermissionService _permissionService;

    public async Task<bool> CanUserEditProject(Guid userId, Guid projectId)
    {
        return await _permissionService.HasProjectAccessAsync(
            userId,
            projectId,
            ProjectRole.Editor);
    }
}
```

## Security Features

1. **Password Security**
   - BCrypt hashing with work factor 12
   - Salted hashes (automatic with BCrypt)
   - No plaintext password storage

2. **Cookie Security**
   - HttpOnly flag prevents XSS attacks
   - Secure flag ensures HTTPS only
   - SameSite=Strict prevents CSRF attacks
   - Sliding expiration for better UX

3. **Authorization**
   - Claims-based authentication
   - Policy-based authorization
   - Hierarchical role system
   - Project-level permissions

4. **API Security**
   - No redirect loops (returns status codes)
   - Active user checking
   - Route/query parameter validation

## Dependencies

### NuGet Packages Added
- **BCrypt.Net-Next** (4.0.3): Password hashing
- **Microsoft.AspNetCore.Authentication.Cookies** (2.2.0): Cookie authentication
- **Microsoft.AspNetCore.Authorization** (10.0.1): Authorization framework
- **Microsoft.AspNetCore.Http.Abstractions** (2.2.0): HTTP context access
- **Microsoft.AspNetCore.Routing.Abstractions** (2.2.0): Route data access

## Notes

1. **No Controllers Implemented**: As requested, only authentication infrastructure was implemented
2. **Database Migrations**: Run migrations to create tables before using authentication
3. **Password Policy**: Consider adding password complexity requirements in production
4. **Token Expiration**: Invitation tokens should have cleanup job
5. **Audit Logging**: Consider adding audit trail for authentication events

## Next Steps for Implementation

1. Create authentication controllers (Login, Register, Logout)
2. Create project management controllers
3. Implement invitation system
4. Add email verification
5. Add refresh token support (optional)
6. Implement password reset functionality
7. Add rate limiting for login attempts
8. Create database migrations
