-- ============================================================================
-- Atlas Control Panel - Complete SQL Server Setup Script
-- ============================================================================
-- This script creates the complete AtlasControlPanel database with all tables,
-- indexes, stored procedures, and initial data.
--
-- Usage: sqlcmd -S localhost -E -i setup.sql
-- This script is idempotent - it can be run multiple times safely.
-- ============================================================================

-- Create database if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'AtlasControlPanel')
BEGIN
    CREATE DATABASE AtlasControlPanel;
END
GO

USE AtlasControlPanel;
GO

-- ============================================================================
-- TABLES
-- ============================================================================

-- ActivityLogs Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ActivityLogs')
BEGIN
    CREATE TABLE ActivityLogs (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Action NVARCHAR(500) NOT NULL,
        Description NVARCHAR(MAX),
        Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        Category NVARCHAR(50) NOT NULL,
        TokensUsed INT NOT NULL DEFAULT 0,
        ApiCalls INT NOT NULL DEFAULT 0,
        EstimatedCost DECIMAL(18,6) NOT NULL DEFAULT 0,
        Currency NVARCHAR(10) NOT NULL DEFAULT 'USD',
        RelatedTaskId UNIQUEIDENTIFIER,
        ParentId UNIQUEIDENTIFIER,
        Details NVARCHAR(MAX)
    );
    CREATE INDEX IX_ActivityLogs_Timestamp ON ActivityLogs(Timestamp DESC);
    CREATE INDEX IX_ActivityLogs_RelatedTaskId ON ActivityLogs(RelatedTaskId);
    CREATE INDEX IX_ActivityLogs_Category ON ActivityLogs(Category);
END
GO

-- ChatMessages Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatMessages')
BEGIN
    CREATE TABLE ChatMessages (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Role NVARCHAR(20) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ChatMessages_CreatedAt ON ChatMessages(CreatedAt DESC);
END
GO

-- CostSummary Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CostSummary')
BEGIN
    CREATE TABLE CostSummary (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Date DATE NOT NULL,
        DailyCost DECIMAL(18,6) NOT NULL DEFAULT 0,
        MonthlyCost DECIMAL(18,6) NOT NULL DEFAULT 0,
        TaskBreakdown NVARCHAR(MAX)
    );
    CREATE UNIQUE INDEX IX_CostSummary_Date ON CostSummary(Date);
END
GO

-- DailyCosts Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DailyCosts')
BEGIN
    CREATE TABLE DailyCosts (
        Date DATE PRIMARY KEY,
        Cost DECIMAL(18,6) NOT NULL DEFAULT 0
    );
END
GO

-- PermissionRequests Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PermissionRequests')
BEGIN
    CREATE TABLE PermissionRequests (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        RequestType NVARCHAR(50) NOT NULL,
        Description NVARCHAR(MAX),
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ResolvedAt DATETIME2,
        ResolvedBy NVARCHAR(200),
        Urgency NVARCHAR(20),
        CredentialId UNIQUEIDENTIFIER,
        Category NVARCHAR(50),
        ExpiresAt DATETIME2
    );
    CREATE INDEX IX_PermissionRequests_Status ON PermissionRequests(Status);
    CREATE INDEX IX_PermissionRequests_RequestedAt ON PermissionRequests(RequestedAt DESC);
END
GO

-- SecureCredentials Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SecureCredentials')
BEGIN
    CREATE TABLE SecureCredentials (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(100) NOT NULL,
        Category NVARCHAR(50) NOT NULL,
        Username NVARCHAR(200),
        Description NVARCHAR(500),
        StorageKey NVARCHAR(200) NOT NULL,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),
        LastAccessedAt DATETIME2,
        AccessCount INT DEFAULT 0,
        VaultMode NVARCHAR(20) NOT NULL DEFAULT 'locked'
    );
    CREATE INDEX IX_SecureCredentials_Category ON SecureCredentials(Category);
    CREATE INDEX IX_SecureCredentials_Name ON SecureCredentials(Name);
END
GO

-- SecurityAudits Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SecurityAudits')
BEGIN
    CREATE TABLE SecurityAudits (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Action NVARCHAR(500) NOT NULL,
        Severity NVARCHAR(50) NOT NULL DEFAULT 'Info',
        Details NVARCHAR(MAX),
        Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_SecurityAudits_Severity ON SecurityAudits(Severity);
    CREATE INDEX IX_SecurityAudits_Timestamp ON SecurityAudits(Timestamp DESC);
END
GO

-- SystemStatus Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemStatus')
BEGIN
    CREATE TABLE SystemStatus (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        GatewayHealth NVARCHAR(100) NOT NULL DEFAULT 'Unknown',
        ActiveSessions INT NOT NULL DEFAULT 0,
        MemoryUsage FLOAT NOT NULL DEFAULT 0,
        Uptime BIGINT NOT NULL DEFAULT 0,
        LastChecked DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        AnthropicBalance NVARCHAR(50),
        TokensRemaining NVARCHAR(20)
    );
END
GO

-- Tasks Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tasks')
BEGIN
    CREATE TABLE Tasks (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Title NVARCHAR(500) NOT NULL,
        Description NVARCHAR(MAX),
        Status NVARCHAR(50) NOT NULL DEFAULT 'ToDo',
        Priority NVARCHAR(50) NOT NULL DEFAULT 'Medium',
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2,
        AssignedTo NVARCHAR(200),
        TokensUsed INT NOT NULL DEFAULT 0,
        ApiCalls INT NOT NULL DEFAULT 0,
        EstimatedCost DECIMAL(18,6) NOT NULL DEFAULT 0,
        Currency NVARCHAR(10) NOT NULL DEFAULT 'USD',
        ScheduledAt DATETIME2,
        RecurrenceType NVARCHAR(20) NOT NULL DEFAULT 'None',
        RecurrenceInterval INT NOT NULL DEFAULT 0,
        RecurrenceDays NVARCHAR(50),
        NextRunAt DATETIME2,
        LastRunAt DATETIME2
    );
    CREATE INDEX IX_Tasks_Status ON Tasks(Status);
    CREATE INDEX IX_Tasks_Priority ON Tasks(Priority);
    CREATE INDEX IX_Tasks_CreatedAt ON Tasks(CreatedAt DESC);
END
GO

-- ============================================================================
-- INITIAL DATA
-- ============================================================================

-- Insert default SystemStatus record if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM SystemStatus WHERE Id = '00000000-0000-0000-0000-000000000001')
BEGIN
    INSERT INTO SystemStatus (Id, GatewayHealth, ActiveSessions, MemoryUsage, Uptime, LastChecked, AnthropicBalance, TokensRemaining)
    VALUES ('00000000-0000-0000-0000-000000000001', 'Unknown', 0, 0, 0, GETUTCDATE(), NULL, NULL);
END
GO

-- ============================================================================
-- STORED PROCEDURES
-- ============================================================================

CREATE OR ALTER PROCEDURE sp_ActivityLogs_Create
    @Action NVARCHAR(500),
    @Description NVARCHAR(MAX),
    @Category NVARCHAR(50),
    @TokensUsed INT = 0,
    @ApiCalls INT = 0,
    @EstimatedCost DECIMAL(18,6) = 0,
    @Currency NVARCHAR(10) = 'USD',
    @RelatedTaskId UNIQUEIDENTIFIER = NULL,
    @ParentId UNIQUEIDENTIFIER = NULL,
    @Details NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO ActivityLogs (Id, Action, Description, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, RelatedTaskId, ParentId, Details)
    VALUES (NEWID(), @Action, @Description, @Category, @TokensUsed, @ApiCalls, @EstimatedCost, @Currency, @RelatedTaskId, @ParentId, @Details);
END
GO

CREATE OR ALTER PROCEDURE sp_ActivityLogs_GetAll
    @Take INT = 50
AS
BEGIN
    SELECT TOP (@Take) Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, RelatedTaskId, ParentId, Details
    FROM ActivityLogs
    WHERE ParentId IS NULL
    ORDER BY Timestamp DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_ActivityLogs_GetById
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SELECT Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, RelatedTaskId, ParentId, Details
    FROM ActivityLogs WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_ActivityLogs_GetByTaskId
    @TaskId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, RelatedTaskId
    FROM ActivityLogs WHERE RelatedTaskId = @TaskId
    ORDER BY Timestamp DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_ActivityLogs_GetWithDetails
    @Take INT = 50
AS
BEGIN
    SELECT * FROM ActivityLogs 
    WHERE ParentId IS NULL 
    ORDER BY Timestamp DESC 
    OFFSET 0 ROWS FETCH NEXT @Take ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE sp_ChatMessages_Add
    @Role NVARCHAR(20),
    @Content NVARCHAR(MAX)
AS
BEGIN
    INSERT INTO ChatMessages (Id, Role, Content, CreatedAt)
    VALUES (NEWID(), @Role, @Content, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE sp_ChatMessages_GetRecent
    @Limit INT = 100
AS
BEGIN
    SELECT Id, Role, Content, CreatedAt
    FROM (
        SELECT TOP (@Limit) Id, Role, Content, CreatedAt
        FROM ChatMessages
        ORDER BY CreatedAt DESC
    ) SubQuery
    ORDER BY CreatedAt ASC;
END
GO

CREATE OR ALTER PROCEDURE sp_CostSummary_GetDaily
    @Date DATE
AS
BEGIN
    SELECT Id, Date, DailyCost, MonthlyCost, TaskBreakdown
    FROM CostSummary WHERE Date = @Date;
END
GO

CREATE OR ALTER PROCEDURE sp_CostSummary_GetMonthly
    @Year INT,
    @Month INT
AS
BEGIN
    SELECT NULL AS Id, NULL AS Date,
        SUM(DailyCost) AS DailyCost,
        MAX(MonthlyCost) AS MonthlyCost,
        NULL AS TaskBreakdown
    FROM CostSummary
    WHERE YEAR(Date) = @Year AND MONTH(Date) = @Month;
END
GO

CREATE OR ALTER PROCEDURE sp_CostSummary_Upsert
    @Date DATE,
    @DailyCost DECIMAL(18,6),
    @MonthlyCost DECIMAL(18,6),
    @TaskBreakdown NVARCHAR(MAX) = NULL
AS
BEGIN
    IF EXISTS (SELECT 1 FROM CostSummary WHERE Date = @Date)
        UPDATE CostSummary SET DailyCost = @DailyCost, MonthlyCost = @MonthlyCost, TaskBreakdown = @TaskBreakdown WHERE Date = @Date
    ELSE
        INSERT INTO CostSummary (Id, Date, DailyCost, MonthlyCost, TaskBreakdown) VALUES (NEWID(), @Date, @DailyCost, @MonthlyCost, @TaskBreakdown);
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
    INSERT INTO SecureCredentials (Id, Name, Category, Username, Description, StorageKey, CreatedAt, UpdatedAt)
    VALUES (@Id, @Name, @Category, @Username, @Description, @StorageKey, GETUTCDATE(), GETUTCDATE());
    SELECT @Id AS Id;
END
GO

CREATE OR ALTER PROCEDURE sp_Credentials_Delete
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    DELETE FROM SecureCredentials WHERE Id = @Id;
END
GO

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

CREATE OR ALTER PROCEDURE sp_Credentials_RecordAccess
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    UPDATE SecureCredentials
    SET AccessCount = AccessCount + 1, LastAccessedAt = GETUTCDATE(), UpdatedAt = GETUTCDATE()
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_DailyCosts_Get
    @Days INT = 30
AS
BEGIN
    SELECT Date, Cost FROM DailyCosts 
    WHERE Date >= DATEADD(DAY, -@Days, CAST(GETUTCDATE() AS DATE)) 
    ORDER BY Date;
END
GO

CREATE OR ALTER PROCEDURE sp_DailyCosts_Increment
    @Cost DECIMAL(18,6)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Today DATE = CAST(GETUTCDATE() AS DATE);
    IF EXISTS (SELECT 1 FROM DailyCosts WHERE Date = @Today)
        UPDATE DailyCosts SET Cost = Cost + @Cost WHERE Date = @Today
    ELSE
        INSERT INTO DailyCosts (Date, Cost) VALUES (@Today, @Cost);
END
GO

CREATE OR ALTER PROCEDURE sp_PermissionRequests_Create
    @RequestType NVARCHAR(50),
    @Description NVARCHAR(500) = NULL,
    @Urgency NVARCHAR(20) = NULL,
    @CredentialId UNIQUEIDENTIFIER = NULL,
    @Category NVARCHAR(50) = NULL,
    @ExpiresAt DATETIME2 = NULL
AS
BEGIN
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO PermissionRequests (Id, RequestType, Description, Status, RequestedAt, Urgency, CredentialId, Category, ExpiresAt)
    VALUES (@Id, @RequestType, @Description, 'Pending', GETUTCDATE(), @Urgency, @CredentialId, @Category, @ExpiresAt);
    SELECT @Id AS Id;
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
END
GO

CREATE OR ALTER PROCEDURE sp_PermissionRequests_Update
    @Id UNIQUEIDENTIFIER,
    @Status NVARCHAR(50),
    @ResolvedBy NVARCHAR(200) = NULL
AS
BEGIN
    UPDATE PermissionRequests SET Status = @Status, ResolvedBy = @ResolvedBy, ResolvedAt = GETUTCDATE()
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_SecurityAudits_Create
    @Action NVARCHAR(500),
    @Severity NVARCHAR(50) = 'Info',
    @Details NVARCHAR(MAX) = NULL
AS
BEGIN
    INSERT INTO SecurityAudits (Id, Action, Severity, Details, Timestamp)
    VALUES (NEWID(), @Action, @Severity, @Details, GETUTCDATE());
END
GO

CREATE OR ALTER PROCEDURE sp_SecurityAudits_GetAll
    @Take INT = 100
AS
BEGIN
    SELECT TOP (@Take) Id, Action, Severity, Details, Timestamp
    FROM SecurityAudits
    ORDER BY Timestamp DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_SecurityAudits_GetBySeverity
    @Severity NVARCHAR(50)
AS
BEGIN
    SELECT Id, Action, Severity, Details, Timestamp
    FROM SecurityAudits WHERE Severity = @Severity ORDER BY Timestamp DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_SystemStatus_Get
AS
BEGIN
    SELECT Id, GatewayHealth, ActiveSessions, MemoryUsage, Uptime, LastChecked, AnthropicBalance, TokensRemaining
    FROM SystemStatus
    WHERE Id = '00000000-0000-0000-0000-000000000001';
END
GO

CREATE OR ALTER PROCEDURE sp_SystemStatus_Upsert
    @Id UNIQUEIDENTIFIER,
    @GatewayHealth NVARCHAR(100),
    @ActiveSessions INT = 0,
    @MemoryUsage FLOAT = 0,
    @Uptime BIGINT = 0,
    @LastChecked DATETIME2 = NULL,
    @AnthropicBalance NVARCHAR(50) = NULL,
    @TokensRemaining NVARCHAR(20) = NULL
AS
BEGIN
    IF EXISTS (SELECT 1 FROM SystemStatus WHERE Id = @Id)
        UPDATE SystemStatus 
        SET GatewayHealth = @GatewayHealth, ActiveSessions = @ActiveSessions, MemoryUsage = @MemoryUsage, 
            Uptime = @Uptime, LastChecked = COALESCE(@LastChecked, GETUTCDATE()), 
            AnthropicBalance = @AnthropicBalance, TokensRemaining = @TokensRemaining
        WHERE Id = @Id
    ELSE
        INSERT INTO SystemStatus (Id, GatewayHealth, ActiveSessions, MemoryUsage, Uptime, LastChecked, AnthropicBalance, TokensRemaining)
        VALUES (@Id, @GatewayHealth, @ActiveSessions, @MemoryUsage, @Uptime, COALESCE(@LastChecked, GETUTCDATE()), @AnthropicBalance, @TokensRemaining);
END
GO

CREATE OR ALTER PROCEDURE sp_Tasks_Create
    @Title NVARCHAR(200),
    @Description NVARCHAR(MAX) = NULL,
    @Status NVARCHAR(20) = 'ToDo',
    @Priority NVARCHAR(20) = 'Medium',
    @AssignedTo NVARCHAR(200) = NULL,
    @TokensUsed INT = 0,
    @ApiCalls INT = 0,
    @EstimatedCost DECIMAL(18,6) = 0,
    @Currency NVARCHAR(10) = 'USD',
    @ScheduledAt DATETIME2 = NULL,
    @RecurrenceType NVARCHAR(20) = 'None',
    @RecurrenceInterval INT = 0,
    @RecurrenceDays NVARCHAR(50) = NULL,
    @NextRunAt DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO Tasks (Id, Title, Description, Status, Priority, CreatedAt, AssignedTo, TokensUsed, ApiCalls, EstimatedCost, Currency, ScheduledAt, RecurrenceType, RecurrenceInterval, RecurrenceDays, NextRunAt)
    VALUES (@Id, @Title, @Description, @Status, @Priority, GETUTCDATE(), @AssignedTo, @TokensUsed, @ApiCalls, @EstimatedCost, @Currency, @ScheduledAt, @RecurrenceType, @RecurrenceInterval, @RecurrenceDays, @NextRunAt);
    SELECT @Id AS Id;
END
GO

CREATE OR ALTER PROCEDURE sp_Tasks_Delete
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    DELETE FROM Tasks WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_Tasks_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Title, Description, Status, Priority, CreatedAt, UpdatedAt, AssignedTo, TokensUsed, ApiCalls, EstimatedCost, Currency, ScheduledAt, RecurrenceType, RecurrenceInterval, RecurrenceDays, NextRunAt, LastRunAt
    FROM Tasks
    ORDER BY CreatedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_Tasks_GetById
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Title, Description, Status, Priority, CreatedAt, UpdatedAt, AssignedTo, TokensUsed, ApiCalls, EstimatedCost, Currency, ScheduledAt, RecurrenceType, RecurrenceInterval, RecurrenceDays, NextRunAt, LastRunAt
    FROM Tasks
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_Tasks_Update
    @Id UNIQUEIDENTIFIER,
    @Title NVARCHAR(200),
    @Description NVARCHAR(MAX) = NULL,
    @Status NVARCHAR(20),
    @Priority NVARCHAR(20),
    @AssignedTo NVARCHAR(200) = NULL,
    @TokensUsed INT = 0,
    @ApiCalls INT = 0,
    @EstimatedCost DECIMAL(18,6) = 0,
    @Currency NVARCHAR(10) = 'USD',
    @ScheduledAt DATETIME2 = NULL,
    @RecurrenceType NVARCHAR(20) = 'None',
    @RecurrenceInterval INT = 0,
    @RecurrenceDays NVARCHAR(50) = NULL,
    @NextRunAt DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Tasks
    SET Title = @Title, Description = @Description, Status = @Status, Priority = @Priority, UpdatedAt = GETUTCDATE(),
        AssignedTo = @AssignedTo, TokensUsed = @TokensUsed, ApiCalls = @ApiCalls, EstimatedCost = @EstimatedCost,
        Currency = @Currency, ScheduledAt = @ScheduledAt, RecurrenceType = @RecurrenceType, RecurrenceInterval = @RecurrenceInterval,
        RecurrenceDays = @RecurrenceDays, NextRunAt = @NextRunAt
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_Tasks_UpdateStatus
    @Id UNIQUEIDENTIFIER,
    @Status NVARCHAR(50)
AS
BEGIN
    UPDATE Tasks SET Status = @Status, UpdatedAt = GETUTCDATE() WHERE Id = @Id;
END
GO

