-- Atlas Control Panel - Initial Schema
-- Run against: AtlasControlPanel database on SQL Server

-- ============================================================
-- TABLES
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tasks')
CREATE TABLE Tasks (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Title           NVARCHAR(500)    NOT NULL,
    Description     NVARCHAR(MAX)    NULL,
    Status          NVARCHAR(50)     NOT NULL DEFAULT 'ToDo',
    Priority        NVARCHAR(50)     NOT NULL DEFAULT 'Medium',
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NULL,
    AssignedTo      NVARCHAR(200)    NULL,
    TokensUsed      INT              NOT NULL DEFAULT 0,
    ApiCalls        INT              NOT NULL DEFAULT 0,
    EstimatedCost   DECIMAL(18,6)    NOT NULL DEFAULT 0,
    Currency        NVARCHAR(10)     NOT NULL DEFAULT 'USD'
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ActivityLogs')
CREATE TABLE ActivityLogs (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Action          NVARCHAR(500)    NOT NULL,
    Description     NVARCHAR(MAX)    NULL,
    Timestamp       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    Category        NVARCHAR(50)     NOT NULL,
    TokensUsed      INT              NOT NULL DEFAULT 0,
    ApiCalls        INT              NOT NULL DEFAULT 0,
    EstimatedCost   DECIMAL(18,6)    NOT NULL DEFAULT 0,
    Currency        NVARCHAR(10)     NOT NULL DEFAULT 'USD',
    RelatedTaskId   UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_ActivityLogs_Tasks FOREIGN KEY (RelatedTaskId) REFERENCES Tasks(Id)
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PermissionRequests')
CREATE TABLE PermissionRequests (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RequestType     NVARCHAR(50)     NOT NULL,
    Description     NVARCHAR(MAX)    NULL,
    Status          NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    RequestedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    ResolvedAt      DATETIME2        NULL,
    ResolvedBy      NVARCHAR(200)    NULL
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SecurityAudits')
CREATE TABLE SecurityAudits (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Action          NVARCHAR(500)    NOT NULL,
    Severity        NVARCHAR(50)     NOT NULL DEFAULT 'Info',
    Details         NVARCHAR(MAX)    NULL,
    Timestamp       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemStatus')
CREATE TABLE SystemStatus (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    GatewayHealth   NVARCHAR(100)    NOT NULL DEFAULT 'Unknown',
    ActiveSessions  INT              NOT NULL DEFAULT 0,
    MemoryUsage     FLOAT            NOT NULL DEFAULT 0,
    Uptime          BIGINT           NOT NULL DEFAULT 0,
    LastChecked     DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CostSummary')
CREATE TABLE CostSummary (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Date            DATE             NOT NULL,
    DailyCost       DECIMAL(18,6)    NOT NULL DEFAULT 0,
    MonthlyCost     DECIMAL(18,6)    NOT NULL DEFAULT 0,
    TaskBreakdown   NVARCHAR(MAX)    NULL
);

-- ============================================================
-- STORED PROCEDURES - Tasks
-- ============================================================

GO
CREATE OR ALTER PROCEDURE sp_Tasks_GetAll
AS
BEGIN
    SELECT Id, Title, Description, Status, Priority, CreatedAt, UpdatedAt, AssignedTo,
           TokensUsed, ApiCalls, EstimatedCost, Currency
    FROM Tasks ORDER BY CreatedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_Tasks_GetById
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SELECT Id, Title, Description, Status, Priority, CreatedAt, UpdatedAt, AssignedTo,
           TokensUsed, ApiCalls, EstimatedCost, Currency
    FROM Tasks WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_Tasks_Create
    @Title NVARCHAR(500),
    @Description NVARCHAR(MAX) = NULL,
    @Status NVARCHAR(50) = 'ToDo',
    @Priority NVARCHAR(50) = 'Medium',
    @AssignedTo NVARCHAR(200) = NULL,
    @TokensUsed INT = 0,
    @ApiCalls INT = 0,
    @EstimatedCost DECIMAL(18,6) = 0,
    @Currency NVARCHAR(10) = 'USD'
AS
BEGIN
    DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO Tasks (Id, Title, Description, Status, Priority, CreatedAt, AssignedTo, TokensUsed, ApiCalls, EstimatedCost, Currency)
    VALUES (@NewId, @Title, @Description, @Status, @Priority, GETUTCDATE(), @AssignedTo, @TokensUsed, @ApiCalls, @EstimatedCost, @Currency);
    SELECT @NewId;
END
GO

CREATE OR ALTER PROCEDURE sp_Tasks_Update
    @Id UNIQUEIDENTIFIER,
    @Title NVARCHAR(500),
    @Description NVARCHAR(MAX) = NULL,
    @Status NVARCHAR(50),
    @Priority NVARCHAR(50),
    @AssignedTo NVARCHAR(200) = NULL,
    @TokensUsed INT = 0,
    @ApiCalls INT = 0,
    @EstimatedCost DECIMAL(18,6) = 0,
    @Currency NVARCHAR(10) = 'USD'
AS
BEGIN
    UPDATE Tasks SET Title = @Title, Description = @Description, Status = @Status, Priority = @Priority,
        AssignedTo = @AssignedTo, TokensUsed = @TokensUsed, ApiCalls = @ApiCalls,
        EstimatedCost = @EstimatedCost, Currency = @Currency, UpdatedAt = GETUTCDATE()
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

CREATE OR ALTER PROCEDURE sp_Tasks_Delete
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    DELETE FROM Tasks WHERE Id = @Id;
END
GO

-- ============================================================
-- STORED PROCEDURES - ActivityLogs
-- ============================================================

CREATE OR ALTER PROCEDURE sp_ActivityLogs_GetAll
    @Take INT = 50
AS
BEGIN
    SELECT TOP (@Take) Id, Action, Description, Timestamp, Category,
           TokensUsed, ApiCalls, EstimatedCost, Currency, RelatedTaskId
    FROM ActivityLogs ORDER BY Timestamp DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_ActivityLogs_GetById
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SELECT Id, Action, Description, Timestamp, Category,
           TokensUsed, ApiCalls, EstimatedCost, Currency, RelatedTaskId
    FROM ActivityLogs WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_ActivityLogs_Create
    @Action NVARCHAR(500),
    @Description NVARCHAR(MAX) = NULL,
    @Category NVARCHAR(50),
    @TokensUsed INT = 0,
    @ApiCalls INT = 0,
    @EstimatedCost DECIMAL(18,6) = 0,
    @Currency NVARCHAR(10) = 'USD',
    @RelatedTaskId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, RelatedTaskId)
    VALUES (@NewId, @Action, @Description, GETUTCDATE(), @Category, @TokensUsed, @ApiCalls, @EstimatedCost, @Currency, @RelatedTaskId);
    SELECT @NewId;
END
GO

CREATE OR ALTER PROCEDURE sp_ActivityLogs_GetByTaskId
    @TaskId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT Id, Action, Description, Timestamp, Category,
           TokensUsed, ApiCalls, EstimatedCost, Currency, RelatedTaskId
    FROM ActivityLogs WHERE RelatedTaskId = @TaskId ORDER BY Timestamp DESC;
END
GO

-- ============================================================
-- STORED PROCEDURES - PermissionRequests
-- ============================================================

CREATE OR ALTER PROCEDURE sp_PermissionRequests_GetAll
AS
BEGIN
    SELECT Id, RequestType, Description, Status, RequestedAt, ResolvedAt, ResolvedBy
    FROM PermissionRequests ORDER BY RequestedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_PermissionRequests_GetPending
AS
BEGIN
    SELECT Id, RequestType, Description, Status, RequestedAt, ResolvedAt, ResolvedBy
    FROM PermissionRequests WHERE Status = 'Pending' ORDER BY RequestedAt DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_PermissionRequests_Create
    @RequestType NVARCHAR(50),
    @Description NVARCHAR(MAX) = NULL
AS
BEGIN
    DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO PermissionRequests (Id, RequestType, Description, Status, RequestedAt)
    VALUES (@NewId, @RequestType, @Description, 'Pending', GETUTCDATE());
    SELECT @NewId;
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
    SELECT Id, RequestType, Description, Status, RequestedAt, ResolvedAt, ResolvedBy
    FROM PermissionRequests WHERE Id = @Id;
END
GO

-- ============================================================
-- STORED PROCEDURES - SecurityAudits
-- ============================================================

CREATE OR ALTER PROCEDURE sp_SecurityAudits_GetAll
    @Take INT = 100
AS
BEGIN
    SELECT TOP (@Take) Id, Action, Severity, Details, Timestamp
    FROM SecurityAudits ORDER BY Timestamp DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_SecurityAudits_Create
    @Action NVARCHAR(500),
    @Severity NVARCHAR(50) = 'Info',
    @Details NVARCHAR(MAX) = NULL
AS
BEGIN
    DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO SecurityAudits (Id, Action, Severity, Details, Timestamp)
    VALUES (@NewId, @Action, @Severity, @Details, GETUTCDATE());
    SELECT @NewId;
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

-- ============================================================
-- STORED PROCEDURES - SystemStatus
-- ============================================================

CREATE OR ALTER PROCEDURE sp_SystemStatus_Get
AS
BEGIN
    SELECT TOP 1 Id, GatewayHealth, ActiveSessions, MemoryUsage, Uptime, LastChecked
    FROM SystemStatus ORDER BY LastChecked DESC;
END
GO

CREATE OR ALTER PROCEDURE sp_SystemStatus_Upsert
    @Id UNIQUEIDENTIFIER,
    @GatewayHealth NVARCHAR(100),
    @ActiveSessions INT,
    @MemoryUsage FLOAT,
    @Uptime BIGINT,
    @LastChecked DATETIME2
AS
BEGIN
    IF EXISTS (SELECT 1 FROM SystemStatus WHERE Id = @Id)
        UPDATE SystemStatus SET GatewayHealth = @GatewayHealth, ActiveSessions = @ActiveSessions,
            MemoryUsage = @MemoryUsage, Uptime = @Uptime, LastChecked = @LastChecked
        WHERE Id = @Id;
    ELSE
        INSERT INTO SystemStatus (Id, GatewayHealth, ActiveSessions, MemoryUsage, Uptime, LastChecked)
        VALUES (@Id, @GatewayHealth, @ActiveSessions, @MemoryUsage, @Uptime, @LastChecked);
END
GO

-- ============================================================
-- STORED PROCEDURES - CostSummary
-- ============================================================

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
        UPDATE CostSummary SET DailyCost = @DailyCost, MonthlyCost = @MonthlyCost, TaskBreakdown = @TaskBreakdown
        WHERE Date = @Date;
    ELSE
        INSERT INTO CostSummary (Id, Date, DailyCost, MonthlyCost, TaskBreakdown)
        VALUES (NEWID(), @Date, @DailyCost, @MonthlyCost, @TaskBreakdown);
END
GO
