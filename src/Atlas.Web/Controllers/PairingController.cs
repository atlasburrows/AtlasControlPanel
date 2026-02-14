using System.Security.Cryptography;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Controllers;

[ApiController]
[Route("api/pairing")]
public class PairingController(IPairingRepository pairingRepository, IConfiguration config) : ControllerBase
{
    /// <summary>
    /// Generate a new pairing code + QR data. Valid for 5 minutes.
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult> Generate()
    {
        // Clean up expired codes first
        await pairingRepository.CleanupExpiredCodesAsync();

        var code = new PairingCode
        {
            Code = GenerateReadableCode(),
            Token = GenerateSecureToken(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        await pairingRepository.CreateCodeAsync(code);

        // Build QR data: server URL + token for the mobile app to scan
        var serverUrl = config["PublicUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var qrData = $"atlas://{serverUrl}/pair?token={code.Token}";

        return Ok(new
        {
            code.Id,
            code.Code,
            code.Token,
            qrData,
            serverUrl,
            expiresAt = code.ExpiresAt,
            expiresInSeconds = 300
        });
    }

    /// <summary>
    /// Complete pairing — mobile app sends the token, gets back a persistent API key.
    /// </summary>
    [HttpPost("complete")]
    public async Task<ActionResult> Complete([FromBody] CompletePairingRequest request)
    {
        var code = await pairingRepository.GetCodeByTokenAsync(request.Token);

        if (code is null)
            return NotFound(new { error = "Invalid pairing token." });

        if (!code.IsValid)
            return BadRequest(new { error = code.IsUsed ? "Pairing code already used." : "Pairing code expired." });

        // Mark code as used
        await pairingRepository.MarkCodeUsedAsync(code.Id);

        // Create the paired device with a persistent API key
        var apiKey = GenerateApiKey();
        var device = new PairedDevice
        {
            Name = request.DeviceName ?? "Unknown Device",
            DeviceType = request.DeviceType ?? "mobile",
            Platform = request.Platform,
            ApiKey = apiKey,
            LastIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        await pairingRepository.AddDeviceAsync(device);

        return Ok(new
        {
            device.Id,
            apiKey,
            device.Name,
            message = "Device paired successfully. Use the API key for all future requests."
        });
    }

    /// <summary>
    /// List all paired devices.
    /// </summary>
    [HttpGet("devices")]
    public async Task<ActionResult> GetDevices()
    {
        var devices = await pairingRepository.GetAllDevicesAsync();
        return Ok(devices.Select(d => new
        {
            d.Id,
            d.Name,
            d.DeviceType,
            d.Platform,
            d.PairedAt,
            d.LastSeenAt,
            d.LastIpAddress,
            d.IsActive
        }));
    }

    /// <summary>
    /// Disconnect (deactivate) a paired device.
    /// </summary>
    [HttpDelete("devices/{id:guid}")]
    public async Task<ActionResult> Disconnect(Guid id)
    {
        var success = await pairingRepository.DisconnectDeviceAsync(id);
        return success
            ? Ok(new { message = "Device disconnected." })
            : NotFound(new { error = "Device not found." });
    }

    // ── Helpers ──

    private static string GenerateReadableCode()
    {
        // 6-digit numeric code for manual entry fallback
        return RandomNumberGenerator.GetInt32(100000, 999999).ToString();
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "atlas_dk_" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public record CompletePairingRequest
{
    public string Token { get; init; } = "";
    public string? DeviceName { get; init; }
    public string? DeviceType { get; init; }
    public string? Platform { get; init; }
}
