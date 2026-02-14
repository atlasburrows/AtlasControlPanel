using Atlas.Domain.Entities;

namespace Atlas.Application.Common.Interfaces;

public interface IPairingRepository
{
    // Pairing codes
    Task<PairingCode> CreateCodeAsync(PairingCode code);
    Task<PairingCode?> GetCodeByTokenAsync(string token);
    Task MarkCodeUsedAsync(Guid id);
    Task CleanupExpiredCodesAsync();

    // Paired devices
    Task<IEnumerable<PairedDevice>> GetAllDevicesAsync();
    Task<PairedDevice?> GetDeviceByIdAsync(Guid id);
    Task<PairedDevice?> GetDeviceByApiKeyAsync(string apiKey);
    Task<PairedDevice> AddDeviceAsync(PairedDevice device);
    Task UpdateLastSeenAsync(Guid id, string? ipAddress);
    Task<bool> DisconnectDeviceAsync(Guid id);
}
