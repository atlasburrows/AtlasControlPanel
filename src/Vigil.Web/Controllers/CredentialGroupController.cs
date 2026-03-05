using Vigil.Application.Common.Interfaces;
using Vigil.Domain.Entities;
using Vigil.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Vigil.Web.Controllers;

[ApiController]
[Route("api/security/credential-groups")]
public class CredentialGroupController(
    ICredentialGroupRepository groupRepository,
    ICredentialRepository credentialRepository,
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

    /// <summary>
    /// Retrieve all decrypted credentials from an approved group request.
    /// Requires the permission request ID from the group access request.
    /// </summary>
    [HttpPost("decrypt-approved-group")]
    public async Task<ActionResult<GroupDecryptionResult>> GetDecryptedGroupCredentials([FromBody] DecryptGroupRequestDto dto)
    {
        // Verify the permission request exists, is approved, and hasn't expired
        var permissions = await securityRepository.GetAllPermissionRequestsAsync();
        var approval = permissions.FirstOrDefault(p =>
            p.Id == dto.PermissionRequestId &&
            p.Status == PermissionStatus.Approved &&
            (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow));

        if (approval is null)
            return Unauthorized(new { error = "No valid approved permission request found. Request group access and wait for owner approval." });

        // Extract group name from Category (format: "CredentialGroup:{groupName}")
        string? groupName = null;
        if (!string.IsNullOrEmpty(approval.Category) && approval.Category.StartsWith("CredentialGroup:"))
        {
            groupName = approval.Category.Substring("CredentialGroup:".Length);
        }

        if (string.IsNullOrEmpty(groupName))
            return BadRequest(new { error = "This request is not a group credential request." });

        // Get all credentials in the group by name
        var credentials = await groupRepository.GetCredentialsInGroupByNameAsync(groupName);
        var credList = credentials.ToList();

        if (credList.Count == 0)
            return NotFound(new { error = $"Group '{groupName}' has no credentials or was not found." });

        // Decrypt all credentials
        var decryptedCreds = new List<DecryptedCredentialDto>();
        foreach (var cred in credList)
        {
            var decryptedValue = await credentialRepository.GetDecryptedStorageKeyAsync(cred.Id);
            if (decryptedValue != null)
            {
                decryptedCreds.Add(new DecryptedCredentialDto(
                    cred.Id,
                    cred.Name,
                    cred.Category,
                    cred.Username,
                    decryptedValue
                ));
            }
        }

        // Log the group access
        await securityRepository.CreateAuditAsync(new SecurityAudit
        {
            Action = "CredentialGroupDecrypted",
            Details = $"Group '{groupName}' ({decryptedCreds.Count} credentials) decrypted via approved request {dto.PermissionRequestId}",
            Severity = Severity.Warning
        });

        return Ok(new GroupDecryptionResult(
            GroupName: groupName,
            Credentials: decryptedCreds,
            RetrievedAt: DateTime.UtcNow
        ));
    }
}

public record CreateCredentialGroupDto(string Name, string? Category = null, string? Description = null, string? Icon = null);
public record GroupAccessRequestDto(string Reason, int? DurationMinutes = 30);
public record DecryptGroupRequestDto(Guid PermissionRequestId);
public record DecryptedCredentialDto(Guid Id, string Name, string Category, string? Username, string Value);
public record GroupDecryptionResult(string GroupName, List<DecryptedCredentialDto> Credentials, DateTime RetrievedAt);
