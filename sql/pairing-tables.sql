-- ============================================================
-- Atlas Control Panel — Device Pairing Tables & Stored Procedures
-- Run against the AtlasControlPanel database
-- ============================================================

-- Pairing Codes (temporary, expire after 5 min)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PairingCodes')
CREATE TABLE PairingCodes (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Code NVARCHAR(10) NOT NULL,
    Token NVARCHAR(100) NOT NULL UNIQUE,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ExpiresAt DATETIME2 NOT NULL,
    IsUsed BIT NOT NULL DEFAULT 0
);
GO

-- Paired Devices (persistent)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PairedDevices')
CREATE TABLE PairedDevices (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    DeviceType NVARCHAR(50) NOT NULL DEFAULT 'mobile',
    ApiKey NVARCHAR(200) NOT NULL UNIQUE,
    Platform NVARCHAR(50) NULL,
    PairedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    LastSeenAt DATETIME2 NULL,
    LastIpAddress NVARCHAR(50) NULL,
    IsActive BIT NOT NULL DEFAULT 1
);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PairingCodes_Token')
    CREATE INDEX IX_PairingCodes_Token ON PairingCodes(Token);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PairedDevices_ApiKey')
    CREATE INDEX IX_PairedDevices_ApiKey ON PairedDevices(ApiKey);
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PairedDevices_IsActive')
    CREATE INDEX IX_PairedDevices_IsActive ON PairedDevices(IsActive);
GO

-- ── Stored Procedures ──

CREATE OR ALTER PROCEDURE sp_PairingCodes_Create
    @Id UNIQUEIDENTIFIER, @Code NVARCHAR(10), @Token NVARCHAR(100),
    @CreatedAt DATETIME2, @ExpiresAt DATETIME2, @IsUsed BIT
AS
    INSERT INTO PairingCodes (Id, Code, Token, CreatedAt, ExpiresAt, IsUsed)
    VALUES (@Id, @Code, @Token, @CreatedAt, @ExpiresAt, @IsUsed);
GO

CREATE OR ALTER PROCEDURE sp_PairingCodes_GetByToken @Token NVARCHAR(100)
AS
    SELECT * FROM PairingCodes WHERE Token = @Token;
GO

CREATE OR ALTER PROCEDURE sp_PairingCodes_MarkUsed @Id UNIQUEIDENTIFIER
AS
    UPDATE PairingCodes SET IsUsed = 1 WHERE Id = @Id;
GO

CREATE OR ALTER PROCEDURE sp_PairingCodes_CleanupExpired
AS
    DELETE FROM PairingCodes WHERE ExpiresAt < SYSUTCDATETIME();
GO

CREATE OR ALTER PROCEDURE sp_PairedDevices_GetAll
AS
    SELECT * FROM PairedDevices ORDER BY PairedAt DESC;
GO

CREATE OR ALTER PROCEDURE sp_PairedDevices_GetById @Id UNIQUEIDENTIFIER
AS
    SELECT * FROM PairedDevices WHERE Id = @Id;
GO

CREATE OR ALTER PROCEDURE sp_PairedDevices_GetByApiKey @ApiKey NVARCHAR(200)
AS
    SELECT * FROM PairedDevices WHERE ApiKey = @ApiKey AND IsActive = 1;
GO

CREATE OR ALTER PROCEDURE sp_PairedDevices_Create
    @Id UNIQUEIDENTIFIER, @Name NVARCHAR(200), @DeviceType NVARCHAR(50),
    @ApiKey NVARCHAR(200), @Platform NVARCHAR(50), @PairedAt DATETIME2, @IsActive BIT
AS
    INSERT INTO PairedDevices (Id, Name, DeviceType, ApiKey, Platform, PairedAt, IsActive)
    VALUES (@Id, @Name, @DeviceType, @ApiKey, @Platform, @PairedAt, @IsActive);
GO

CREATE OR ALTER PROCEDURE sp_PairedDevices_UpdateLastSeen
    @Id UNIQUEIDENTIFIER, @LastSeenAt DATETIME2, @LastIpAddress NVARCHAR(50)
AS
    UPDATE PairedDevices SET LastSeenAt = @LastSeenAt, LastIpAddress = @LastIpAddress WHERE Id = @Id;
GO

CREATE OR ALTER PROCEDURE sp_PairedDevices_Disconnect @Id UNIQUEIDENTIFIER
AS
    UPDATE PairedDevices SET IsActive = 0 WHERE Id = @Id;
GO
