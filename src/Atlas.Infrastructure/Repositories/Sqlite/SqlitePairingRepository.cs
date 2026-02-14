using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Dapper;

namespace Atlas.Infrastructure.Repositories.Sqlite;

public class SqlitePairingRepository(IDbConnectionFactory connectionFactory) : IPairingRepository
{
    // ── Pairing Codes ──

    public async Task<PairingCode> CreateCodeAsync(PairingCode code)
    {
        code.Id = Guid.NewGuid();
        code.CreatedAt = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO PairingCodes (Id, Code, Token, CreatedAt, ExpiresAt, IsUsed)
              VALUES (@Id, @Code, @Token, @CreatedAt, @ExpiresAt, @IsUsed)",
            new
            {
                Id = code.Id.ToString(),
                code.Code,
                code.Token,
                CreatedAt = code.CreatedAt.ToString("O"),
                ExpiresAt = code.ExpiresAt.ToString("O"),
                IsUsed = code.IsUsed ? 1 : 0
            });

        return code;
    }

    public async Task<PairingCode?> GetCodeByTokenAsync(string token)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PairingCodeRow>(
            "SELECT * FROM PairingCodes WHERE Token = @Token", new { Token = token });

        return row is null ? null : MapCode(row);
    }

    public async Task MarkCodeUsedAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE PairingCodes SET IsUsed = 1 WHERE Id = @Id",
            new { Id = id.ToString() });
    }

    public async Task CleanupExpiredCodesAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM PairingCodes WHERE ExpiresAt < @Now",
            new { Now = DateTime.UtcNow.ToString("O") });
    }

    // ── Paired Devices ──

    public async Task<IEnumerable<PairedDevice>> GetAllDevicesAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PairedDeviceRow>(
            "SELECT * FROM PairedDevices ORDER BY PairedAt DESC");

        return rows.Select(MapDevice);
    }

    public async Task<PairedDevice?> GetDeviceByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PairedDeviceRow>(
            "SELECT * FROM PairedDevices WHERE Id = @Id", new { Id = id.ToString() });

        return row is null ? null : MapDevice(row);
    }

    public async Task<PairedDevice?> GetDeviceByApiKeyAsync(string apiKey)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PairedDeviceRow>(
            "SELECT * FROM PairedDevices WHERE ApiKey = @ApiKey AND IsActive = 1",
            new { ApiKey = apiKey });

        return row is null ? null : MapDevice(row);
    }

    public async Task<PairedDevice> AddDeviceAsync(PairedDevice device)
    {
        device.Id = Guid.NewGuid();
        device.PairedAt = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO PairedDevices (Id, Name, DeviceType, ApiKey, Platform, PairedAt, LastSeenAt, LastIpAddress, IsActive)
              VALUES (@Id, @Name, @DeviceType, @ApiKey, @Platform, @PairedAt, @LastSeenAt, @LastIpAddress, @IsActive)",
            new
            {
                Id = device.Id.ToString(),
                device.Name,
                device.DeviceType,
                device.ApiKey,
                device.Platform,
                PairedAt = device.PairedAt.ToString("O"),
                LastSeenAt = device.LastSeenAt?.ToString("O"),
                device.LastIpAddress,
                IsActive = device.IsActive ? 1 : 0
            });

        return device;
    }

    public async Task UpdateLastSeenAsync(Guid id, string? ipAddress)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE PairedDevices SET LastSeenAt = @Now, LastIpAddress = @Ip WHERE Id = @Id",
            new { Now = DateTime.UtcNow.ToString("O"), Ip = ipAddress, Id = id.ToString() });
    }

    public async Task<bool> DisconnectDeviceAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(
            "UPDATE PairedDevices SET IsActive = 0 WHERE Id = @Id",
            new { Id = id.ToString() });

        return affected > 0;
    }

    // ── Mapping ──

    private static PairingCode MapCode(PairingCodeRow row) => new()
    {
        Id = Guid.Parse(row.Id),
        Code = row.Code,
        Token = row.Token,
        CreatedAt = DateTime.Parse(row.CreatedAt),
        ExpiresAt = DateTime.Parse(row.ExpiresAt),
        IsUsed = row.IsUsed == 1
    };

    private static PairedDevice MapDevice(PairedDeviceRow row) => new()
    {
        Id = Guid.Parse(row.Id),
        Name = row.Name,
        DeviceType = row.DeviceType,
        ApiKey = row.ApiKey,
        Platform = row.Platform,
        PairedAt = DateTime.Parse(row.PairedAt),
        LastSeenAt = string.IsNullOrEmpty(row.LastSeenAt) ? null : DateTime.Parse(row.LastSeenAt),
        LastIpAddress = row.LastIpAddress,
        IsActive = row.IsActive == 1
    };

    private class PairingCodeRow
    {
        public string Id { get; set; } = "";
        public string Code { get; set; } = "";
        public string Token { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string ExpiresAt { get; set; } = "";
        public int IsUsed { get; set; }
    }

    private class PairedDeviceRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string DeviceType { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string? Platform { get; set; }
        public string PairedAt { get; set; } = "";
        public string? LastSeenAt { get; set; }
        public string? LastIpAddress { get; set; }
        public int IsActive { get; set; }
    }
}
