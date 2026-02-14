using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Dapper;

namespace Atlas.Infrastructure.Repositories;

public class PairingRepository(IDbConnectionFactory connectionFactory) : IPairingRepository
{
    // ── Pairing Codes ──

    public async Task<PairingCode> CreateCodeAsync(PairingCode code)
    {
        code.Id = Guid.NewGuid();
        code.CreatedAt = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "sp_PairingCodes_Create",
            new { code.Id, code.Code, code.Token, code.CreatedAt, code.ExpiresAt, code.IsUsed },
            commandType: System.Data.CommandType.StoredProcedure);

        return code;
    }

    public async Task<PairingCode?> GetCodeByTokenAsync(string token)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PairingCode>(
            "sp_PairingCodes_GetByToken",
            new { Token = token },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task MarkCodeUsedAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "sp_PairingCodes_MarkUsed",
            new { Id = id },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task CleanupExpiredCodesAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "sp_PairingCodes_CleanupExpired",
            commandType: System.Data.CommandType.StoredProcedure);
    }

    // ── Paired Devices ──

    public async Task<IEnumerable<PairedDevice>> GetAllDevicesAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<PairedDevice>(
            "sp_PairedDevices_GetAll",
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task<PairedDevice?> GetDeviceByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PairedDevice>(
            "sp_PairedDevices_GetById",
            new { Id = id },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task<PairedDevice?> GetDeviceByApiKeyAsync(string apiKey)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PairedDevice>(
            "sp_PairedDevices_GetByApiKey",
            new { ApiKey = apiKey },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task<PairedDevice> AddDeviceAsync(PairedDevice device)
    {
        device.Id = Guid.NewGuid();
        device.PairedAt = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "sp_PairedDevices_Create",
            new { device.Id, device.Name, device.DeviceType, device.ApiKey, device.Platform, device.PairedAt, device.IsActive },
            commandType: System.Data.CommandType.StoredProcedure);

        return device;
    }

    public async Task UpdateLastSeenAsync(Guid id, string? ipAddress)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "sp_PairedDevices_UpdateLastSeen",
            new { Id = id, LastSeenAt = DateTime.UtcNow, LastIpAddress = ipAddress },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task<bool> DisconnectDeviceAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(
            "sp_PairedDevices_Disconnect",
            new { Id = id },
            commandType: System.Data.CommandType.StoredProcedure);

        return affected > 0;
    }
}
