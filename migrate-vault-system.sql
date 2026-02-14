-- Migration script to add Vault Tier System
-- Adds VaultMode column to SecureCredentials and creates CredentialAccessLog table

USE AtlasControlPanel;
GO

-- Add VaultMode column to SecureCredentials if it doesn't exist
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'SecureCredentials' AND COLUMN_NAME = 'VaultMode'
)
BEGIN
    ALTER TABLE SecureCredentials 
    ADD VaultMode NVARCHAR(20) NOT NULL DEFAULT 'locked';
    PRINT 'Added VaultMode column to SecureCredentials';
END
ELSE
BEGIN
    PRINT 'VaultMode column already exists in SecureCredentials';
END
GO

-- Create CredentialAccessLog table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CredentialAccessLog')
BEGIN
    CREATE TABLE CredentialAccessLog (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CredentialId UNIQUEIDENTIFIER NOT NULL,
        CredentialName NVARCHAR(100) NOT NULL,
        Requester NVARCHAR(200),
        AccessedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        VaultMode NVARCHAR(20) NOT NULL DEFAULT 'locked',
        AutoApproved BIT NOT NULL DEFAULT 0,
        Details NVARCHAR(MAX)
    );
    CREATE INDEX IX_CredentialAccessLog_CredentialId ON CredentialAccessLog(CredentialId);
    CREATE INDEX IX_CredentialAccessLog_AccessedAt ON CredentialAccessLog(AccessedAt DESC);
    PRINT 'Created CredentialAccessLog table';
END
ELSE
BEGIN
    PRINT 'CredentialAccessLog table already exists';
END
GO

PRINT 'Migration completed successfully!';
