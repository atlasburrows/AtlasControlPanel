-- SecureCredentials table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SecureCredentials')
BEGIN
    CREATE TABLE SecureCredentials (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(100) NOT NULL,
        Category NVARCHAR(50) NOT NULL,
        Username NVARCHAR(200) NULL,
        Description NVARCHAR(500) NULL,
        StorageKey NVARCHAR(200) NOT NULL,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),
        LastAccessedAt DATETIME2 NULL,
        AccessCount INT DEFAULT 0
    );
END
GO

-- Add columns to PermissionRequests if missing
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PermissionRequests') AND name = 'Urgency')
    ALTER TABLE PermissionRequests ADD Urgency NVARCHAR(20) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PermissionRequests') AND name = 'CredentialId')
    ALTER TABLE PermissionRequests ADD CredentialId UNIQUEIDENTIFIER NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PermissionRequests') AND name = 'Category')
    ALTER TABLE PermissionRequests ADD Category NVARCHAR(50) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PermissionRequests') AND name = 'ExpiresAt')
    ALTER TABLE PermissionRequests ADD ExpiresAt DATETIME2 NULL;
GO

-- FK if both tables exist
IF EXISTS (SELECT 1 FROM sys.tables WHERE name='SecureCredentials') AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('PermissionRequests') AND name='CredentialId')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PermissionRequests_SecureCredentials')
        ALTER TABLE PermissionRequests ADD CONSTRAINT FK_PermissionRequests_SecureCredentials
            FOREIGN KEY (CredentialId) REFERENCES SecureCredentials(Id);
END
GO

-- Credential stored procs
CREATE OR ALTER PROCEDURE sp_Credentials_GetAll
AS
BEGIN
    SELECT Id, Name, Category, Username, Description, StorageKey, CreatedAt, UpdatedAt, LastAccessedAt, AccessCount
    FROM SecureCredentials ORDER BY Category, Name;
END
GO

CREATE OR ALTER PROCEDURE sp_Credentials_GetById
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SELECT Id, Name, Category, Username, Description, StorageKey, CreatedAt, UpdatedAt, LastAccessedAt, AccessCount
    FROM SecureCredentials WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_Credentials_Create
    @Name NVARCHAR(100),
    @Category NVARCHAR(50),
    @Username NVARCHAR(200) = NULL,
    @Description NVARCHAR(500) = NULL,
    @StorageKey NVARCHAR(200)
AS
BEGIN
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO SecureCredentials (Id, Name, Category, Username, Description, StorageKey)
    VALUES (@Id, @Name, @Category, @Username, @Description, @StorageKey);
    SELECT @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_Credentials_Delete
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    DELETE FROM SecureCredentials WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_Credentials_RecordAccess
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    UPDATE SecureCredentials
    SET AccessCount = AccessCount + 1, LastAccessedAt = GETUTCDATE(), UpdatedAt = GETUTCDATE()
    WHERE Id = @Id;
END
GO

-- Update Permission Request stored procs
CREATE OR ALTER PROCEDURE sp_PermissionRequests_Create
    @RequestType NVARCHAR(50),
    @Description NVARCHAR(500),
    @Urgency NVARCHAR(20) = NULL,
    @CredentialId UNIQUEIDENTIFIER = NULL,
    @Category NVARCHAR(50) = NULL,
    @ExpiresAt DATETIME2 = NULL
AS
BEGIN
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO PermissionRequests (Id, RequestType, Description, Status, RequestedAt, Urgency, CredentialId, Category, ExpiresAt)
    VALUES (@Id, @RequestType, @Description, 'Pending', GETUTCDATE(), @Urgency, @CredentialId, @Category, @ExpiresAt);
    SELECT @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_PermissionRequests_GetAll
AS
BEGIN
    SELECT Id, RequestType, Description, Status, RequestedAt, ResolvedAt, ResolvedBy, Urgency, CredentialId, Category, ExpiresAt
    FROM PermissionRequests ORDER BY RequestedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_PermissionRequests_GetPending
AS
BEGIN
    SELECT Id, RequestType, Description, Status, RequestedAt, ResolvedAt, ResolvedBy, Urgency, CredentialId, Category, ExpiresAt
    FROM PermissionRequests WHERE Status = 'Pending' ORDER BY RequestedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_PermissionRequests_Resolve
    @Id UNIQUEIDENTIFIER,
    @Status NVARCHAR(20),
    @ResolvedBy NVARCHAR(100)
AS
BEGIN
    UPDATE PermissionRequests
    SET Status = @Status, ResolvedAt = GETUTCDATE(), ResolvedBy = @ResolvedBy
    WHERE Id = @Id;
    
    SELECT Id, RequestType, Description, Status, RequestedAt, ResolvedAt, ResolvedBy, Urgency, CredentialId, Category, ExpiresAt
    FROM PermissionRequests WHERE Id = @Id;
END
GO
