using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Controllers;

[ApiController]
[Route("api/security/credential-groups")]
public class CredentialGroupController(
    ICredentialGroupRepository groupRepository,
    ISecurityRepository securityRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CredentialGroup>>> GetAll()
        => Ok(await groupRepository.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CredentialGroup>> GetById(Guid id)
    {
        var group = await groupRepository.GetByIdAsync(id);
        if (group is null) return NotFound();

        group.Credentials = (await groupRepository.GetCredentialsInGroupAsync(id)).ToList();
        return Ok(group);
    }

    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<CredentialGroup>> GetByName(string name)
    {
        var group = await groupRepository.GetByNameAsync(name);
        if (group is null) return NotFound();

        group.Credentials = (await groupRepository.GetCredentialsInGroupAsync(group.Id)).ToList();
        return Ok(group);
    }

    [HttpPost]
    public async Task<ActionResult<CredentialGroup>> Create([FromBody] CreateCredentialGroupDto dto)
    {
        var group = new CredentialGroup
        {
            Name = dto.Name,
            Category = dto.Category,
            Description = dto.Description,
            Icon = dto.Icon
        };
        var created = await groupRepository.CreateAsync(group);
        return Created($"api/security/credential-groups/{created.Id}", created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var group = await groupRepository.GetByIdAsync(id);
        if (group is null) return NotFound();

        await groupRepository.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/members/{credentialId:guid}")]
    public async Task<ActionResult> AddMember(Guid id, Guid credentialId)
    {
        var group = await groupRepository.GetByIdAsync(id);
        if (group is null) return NotFound();

        await groupRepository.AddCredentialToGroupAsync(id, credentialId);
        return Ok();
    }

    [HttpDelete("{id:guid}/members/{credentialId:guid}")]
    public async Task<ActionResult> RemoveMember(Guid id, Guid credentialId)
    {
        await groupRepository.RemoveCredentialFromGroupAsync(id, credentialId);
        return NoContent();
    }

    /// <summary>
    /// Request access to ALL credentials in a group with a single approval.
    /// </summary>
    [HttpPost("{id:guid}/request-access")]
    public async Task<ActionResult<PermissionRequest>> RequestGroupAccess(Guid id, [FromBody] GroupAccessRequestDto dto)
    {
        var group = await groupRepository.GetByIdAsync(id);
        if (group is null) return NotFound();

        var credentials = await groupRepository.GetCredentialsInGroupAsync(id);
        var credList = credentials.ToList();
        if (credList.Count == 0)
            return BadRequest("Group has no credentials");

        var credNames = string.Join(", ", credList.Select(c => c.Name));

        var request = new PermissionRequest
        {
            RequestType = PermissionRequestType.CredentialAccess,
            Description = $"Group access requested for '{group.Name}' ({credNames}): {dto.Reason}",
            Status = PermissionStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            Urgency = "Normal",
            Category = $"CredentialGroup:{group.Name}",
            ExpiresAt = DateTime.UtcNow.AddMinutes(dto.DurationMinutes ?? 30),
            // Store the first credential ID for backwards compat; the Category tag identifies the group
            CredentialId = credList.First().Id
        };

        var created = await securityRepository.CreatePermissionRequestAsync(request);

        await securityRepository.CreateAuditAsync(new SecurityAudit
        {
            Action = "CredentialGroupAccessRequest",
            Details = $"Group '{group.Name}' access requested ({credList.Count} credentials): {dto.Reason}",
            Severity = Severity.Warning
        });

        return Created($"api/security/permissions/{created.Id}", created);
    }
}

public record CreateCredentialGroupDto(string Name, string? Category = null, string? Description = null, string? Icon = null);
public record GroupAccessRequestDto(string Reason, int? DurationMinutes = 30);
