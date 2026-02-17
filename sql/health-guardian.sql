-- Health Guardian Tables and Stored Procedures
-- SQL Server

CREATE TABLE HealthChecks (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ServiceName NVARCHAR(100) NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    CheckedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ResponseTimeMs FLOAT NULL,
    Details NVARCHAR(MAX) NULL,
    AutoRestarted BIT NOT NULL DEFAULT 0
);

CREATE TABLE HealthEvents (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ServiceName NVARCHAR(100) NOT NULL,
    EventType NVARCHAR(50) NOT NULL,
    OccurredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Details NVARCHAR(MAX) NULL,
    NotificationSent BIT NOT NULL DEFAULT 0
);

CREATE INDEX IX_HealthChecks_ServiceName_CheckedAt ON HealthChecks(ServiceName, CheckedAt DESC);
CREATE INDEX IX_HealthEvents_ServiceName_OccurredAt ON HealthEvents(ServiceName, OccurredAt DESC);
GO

-- Record a health check
CREATE OR ALTER PROCEDURE sp_HealthChecks_Record
    @ServiceName NVARCHAR(100),
    @Status NVARCHAR(20),
    @CheckedAt DATETIME2,
    @ResponseTimeMs FLOAT = NULL,
    @Details NVARCHAR(MAX) = NULL,
    @AutoRestarted BIT = 0
AS
BEGIN
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO HealthChecks (Id, ServiceName, Status, CheckedAt, ResponseTimeMs, Details, AutoRestarted)
    VALUES (@Id, @ServiceName, @Status, @CheckedAt, @ResponseTimeMs, @Details, @AutoRestarted);
    SELECT @Id;
END
GO

-- Get latest health checks
CREATE OR ALTER PROCEDURE sp_HealthChecks_GetLatest
    @Take INT = 50
AS
BEGIN
    SELECT TOP (@Take) * FROM HealthChecks ORDER BY CheckedAt DESC;
END
GO

-- Get latest check for a specific service
CREATE OR ALTER PROCEDURE sp_HealthChecks_GetLatestForService
    @ServiceName NVARCHAR(100)
AS
BEGIN
    SELECT TOP 1 * FROM HealthChecks WHERE ServiceName = @ServiceName ORDER BY CheckedAt DESC;
END
GO

-- Record a health event
CREATE OR ALTER PROCEDURE sp_HealthEvents_Record
    @ServiceName NVARCHAR(100),
    @EventType NVARCHAR(50),
    @OccurredAt DATETIME2,
    @Details NVARCHAR(MAX) = NULL,
    @NotificationSent BIT = 0
AS
BEGIN
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO HealthEvents (Id, ServiceName, EventType, OccurredAt, Details, NotificationSent)
    VALUES (@Id, @ServiceName, @EventType, @OccurredAt, @Details, @NotificationSent);
    SELECT @Id;
END
GO

-- Get all health events
CREATE OR ALTER PROCEDURE sp_HealthEvents_GetAll
    @Take INT = 100
AS
BEGIN
    SELECT TOP (@Take) * FROM HealthEvents ORDER BY OccurredAt DESC;
END
GO

-- Get uptime stats for a service
CREATE OR ALTER PROCEDURE sp_Health_GetUptimeStats
    @ServiceName NVARCHAR(100),
    @Days INT = 7
AS
BEGIN
    DECLARE @Since DATETIME2 = DATEADD(DAY, -@Days, GETUTCDATE());
    
    SELECT
        @ServiceName AS ServiceName,
        COUNT(*) AS TotalChecks,
        SUM(CASE WHEN Status = 'Healthy' THEN 1 ELSE 0 END) AS HealthyChecks,
        CASE WHEN COUNT(*) > 0 
            THEN ROUND(CAST(SUM(CASE WHEN Status = 'Healthy' THEN 1 ELSE 0 END) AS FLOAT) / COUNT(*) * 100, 2)
            ELSE 0 END AS UptimePercent,
        ISNULL(AVG(ResponseTimeMs), 0) AS AvgResponseTimeMs,
        @Days AS [Days]
    FROM HealthChecks
    WHERE ServiceName = @ServiceName AND CheckedAt >= @Since;
END
GO
