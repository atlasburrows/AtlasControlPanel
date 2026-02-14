using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Controllers;

[ApiController]
[Route("api/security/credentials")]
public class CredentialController(
    ICredentialRepository credentialRepository,
    ISecurityRepository securityRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SecureCredential>>> GetAll()
        => Ok(await credentialRepository.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SecureCredential>> GetById(Guid id)
    {
        var cred = await credentialRepository.GetByIdAsync(id);
        return cred is null ? NotFound() : Ok(cred);
    }

    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<SecureCredential>> GetByName(string name)
    {
        var cred = await credentialRepository.GetByNameAsync(name);
        return cred is null ? NotFound() : Ok(cred);
    }

    /// <summary>
    /// Decrypt a credential by name. Requires an approved permission request ID.
    /// </summary>
    [HttpPost("by-name/{name}/decrypt")]
    public async Task<ActionResult<object>> GetDecryptedByName(string name, [FromBody] DecryptRequestDto dto)
    {
        var cred = await credentialRepository.GetByNameAsync(name);
        if (cred is null)
            return NotFound(new { error = $"Credential '{name}' not found" });

        // Verify the permission request exists, is approved, and hasn't expired
        var permissions = await securityRepository.GetAllPermissionRequestsAsync();
        var approval = permissions.FirstOrDefault(p =>
            p.Id == dto.PermissionRequestId &&
            p.Status == PermissionStatus.Approved &&
            (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow));

        if (approval is null)
            return Unauthorized(new { error = "No valid approved permission request found." });

        var decrypted = await credentialRepository.GetDecryptedStorageKeyAsync(cred.Id);
        if (decrypted is null)
            return NotFound();

        await securityRepository.CreateAuditAsync(new SecurityAudit
        {
            Action = "CredentialDecrypted",
            Details = $"Credential '{name}' ({cred.Id}) decrypted via approved request {dto.PermissionRequestId}",
            Severity = Severity.Warning
        });

        return Ok(new { value = decrypted, credentialId = cred.Id, name = cred.Name });
    }

    [HttpPost]
    public async Task<ActionResult<SecureCredential>> Create(SecureCredential credential)
    {
        var created = await credentialRepository.CreateAsync(credential);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await credentialRepository.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/access")]
    public async Task<IActionResult> RecordAccess(Guid id)
    {
        await credentialRepository.RecordAccessAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Retrieve the decrypted credential value. Requires an approved permission request.
    /// </summary>
    [HttpPost("{id:guid}/decrypt")]
    public async Task<ActionResult<object>> GetDecrypted(Guid id, [FromBody] DecryptRequestDto dto)
    {
        // Verify the permission request exists, is approved, and hasn't expired
        var permissions = await securityRepository.GetAllPermissionRequestsAsync();
        var approval = permissions.FirstOrDefault(p =>
            p.Id == dto.PermissionRequestId &&
            p.Status == PermissionStatus.Approved &&
            (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow));

        if (approval is null)
            return Unauthorized(new { error = "No valid approved permission request found. Request credential access and wait for owner approval." });

        var decrypted = await credentialRepository.GetDecryptedStorageKeyAsync(id);
        if (decrypted is null)
            return NotFound();

        // Log the access in security audit
        await securityRepository.CreateAuditAsync(new SecurityAudit
        {
            Action = "CredentialDecrypted",
            Details = $"Credential {id} decrypted via approved request {dto.PermissionRequestId}",
            Severity = Severity.Warning
        });

        return Ok(new { value = decrypted });
    }
}

public record DecryptRequestDto(Guid PermissionRequestId);
