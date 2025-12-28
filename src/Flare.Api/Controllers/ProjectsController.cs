using Asp.Versioning;
using Flare.Application.DTOs;
using Flare.Application.Extensions;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
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

        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _projectService.CreateAsync(dto, userId);

            _logger.LogInformation("Project {ProjectId} created by user {UserId}", result.Id, userId);

            return CreatedAtAction(nameof(GetProject), new { projectId = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<ProjectResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ProjectResponseDto>>> GetUserProjects()
    {
        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _projectService.GetUserProjectsAsync(userId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user projects");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{projectId}")]
    [Authorize]
    [ProducesResponseType(typeof(ProjectDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectDetailResponseDto>> GetProject(Guid projectId)
    {
        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _projectService.GetByIdAsync(projectId, userId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project {ProjectId}", projectId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("by-alias/{alias}")]
    [Authorize]
    [ProducesResponseType(typeof(ProjectResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProjectResponseDto>> GetProjectByAlias(string alias)
    {
        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _projectService.GetByAliasAsync(alias, userId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project by alias {Alias}", alias);
            return BadRequest(new { message = ex.Message });
        }
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

        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _projectService.UpdateAsync(projectId, dto, userId);

            _logger.LogInformation("Project {ProjectId} updated by user {UserId}", projectId, userId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectId}", projectId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{projectId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteProject(Guid projectId)
    {
        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            await _projectService.DeleteAsync(projectId, userId);

            _logger.LogInformation("Project {ProjectId} deleted by user {UserId}", projectId, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {ProjectId}", projectId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{projectId}/regenerate-api-key")]
    [Authorize]
    [ProducesResponseType(typeof(RegenerateApiKeyResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RegenerateApiKeyResponseDto>> RegenerateApiKey(Guid projectId)
    {
        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _projectService.RegenerateApiKeyAsync(projectId, userId);

            _logger.LogInformation("API key regenerated for project {ProjectId} by user {UserId}", projectId, userId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating API key for project {ProjectId}", projectId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{projectId}/archive")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ArchiveProject(Guid projectId)
    {
        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            await _projectService.ArchiveAsync(projectId, userId);

            _logger.LogInformation("Project {ProjectId} archived by user {UserId}", projectId, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving project {ProjectId}", projectId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{projectId}/unarchive")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UnarchiveProject(Guid projectId)
    {
        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            await _projectService.UnarchiveAsync(projectId, userId);

            _logger.LogInformation("Project {ProjectId} unarchived by user {UserId}", projectId, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unarchiving project {ProjectId}", projectId);
            return BadRequest(new { message = ex.Message });
        }
    }
}
