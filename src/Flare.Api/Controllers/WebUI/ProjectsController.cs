using Asp.Versioning;
using Flare.Application.DTOs;
using Flare.Application.Extensions;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Controllers.WebUI;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ProjectDetailResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProjectDetailResponseDto>> CreateProject([FromBody] CreateProjectDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        var result = await _projectService.CreateAsync(dto, userId, username);

        return CreatedAtAction(nameof(GetProject), new { projectId = result.Id }, result);
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<ProjectResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ProjectResponseDto>>> GetUserProjects()
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _projectService.GetUserProjectsAsync(userId);

        return Ok(result);
    }

    [HttpGet("{projectId}")]
    [Authorize]
    [ProducesResponseType(typeof(ProjectDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectDetailResponseDto>> GetProject(Guid projectId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _projectService.GetByIdAsync(projectId, userId);

        return Ok(result);
    }

    [HttpPut("{projectId}")]
    [Authorize]
    [ProducesResponseType(typeof(ProjectDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectDetailResponseDto>> UpdateProject(Guid projectId, [FromBody] UpdateProjectDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        var result = await _projectService.UpdateAsync(projectId, dto, userId, username);

        return Ok(result);
    }

    [HttpDelete("{projectId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteProject(Guid projectId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _projectService.DeleteAsync(projectId, userId, username);

        return NoContent();
    }

    [HttpPost("{projectId}/regenerate-api-key")]
    [Authorize]
    [ProducesResponseType(typeof(RegenerateApiKeyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RegenerateApiKeyResponseDto>> RegenerateApiKey(Guid projectId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        var result = await _projectService.RegenerateApiKeyAsync(projectId, userId, username);

        return Ok(result);
    }

    [HttpPost("{projectId}/archive")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ArchiveProject(Guid projectId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _projectService.ArchiveAsync(projectId, userId, username);

        return NoContent();
    }

    [HttpPost("{projectId}/unarchive")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UnarchiveProject(Guid projectId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _projectService.UnarchiveAsync(projectId, userId, username);

        return NoContent();
    }

    /// <summary>
    /// Gets the current authenticated user's permissions for the specified project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the project.</param>
    /// <returns>The user's project-level and scope-level permissions.</returns>
    /// <response code="200">Returns the user's permissions for the project.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not a member of the project.</response>
    /// <response code="404">Project not found.</response>
    [HttpGet("{projectId}/my-permissions")]
    [Authorize]
    [ProducesResponseType(typeof(MyPermissionsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyPermissionsResponseDto>> GetMyPermissions(Guid projectId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _projectService.GetMyPermissionsAsync(projectId, userId);

        return Ok(result);
    }
}
