-- 003_ScheduledTasks.sql
-- Add scheduling and recurrence support to Tasks

ALTER TABLE Tasks ADD
    ScheduledAt     DATETIME2       NULL,
    RecurrenceType  NVARCHAR(20)    NOT NULL DEFAULT 'None',
    RecurrenceInterval INT          NOT NULL DEFAULT 0,
    RecurrenceDays  NVARCHAR(50)    NULL,       -- e.g. "Mon,Wed,Fri" for weekly
    NextRunAt       DATETIME2       NULL,
    LastRunAt       DATETIME2       NULL;
GO

-- Update sp_Tasks_Create to include scheduling fields
ALTER PROCEDURE sp_Tasks_Create
    @Title          NVARCHAR(200),
    @Description    NVARCHAR(MAX)   = NULL,
    @Status         NVARCHAR(20)    = 'ToDo',
    @Priority       NVARCHAR(20)    = 'Medium',
    @AssignedTo     NVARCHAR(100)   = NULL,
    @TokensUsed     INT             = 0,
    @ApiCalls       INT             = 0,
    @EstimatedCost  DECIMAL(18,6)   = 0,
    @Currency       NVARCHAR(10)    = 'USD',
    @ScheduledAt    DATETIME2       = NULL,
    @RecurrenceType NVARCHAR(20)    = 'None',
    @RecurrenceInterval INT         = 0,
    @RecurrenceDays NVARCHAR(50)    = NULL,
    @NextRunAt      DATETIME2       = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();

    INSERT INTO Tasks (Id, Title, Description, Status, Priority, AssignedTo, 
                       TokensUsed, ApiCalls, EstimatedCost, Currency, CreatedAt,
                       ScheduledAt, RecurrenceType, RecurrenceInterval, RecurrenceDays, NextRunAt)
    VALUES (@Id, @Title, @Description, @Status, @Priority, @AssignedTo,
            @TokensUsed, @ApiCalls, @EstimatedCost, @Currency, SYSUTCDATETIME(),
            @ScheduledAt, @RecurrenceType, @RecurrenceInterval, @RecurrenceDays, 
            COALESCE(@NextRunAt, @ScheduledAt));

    SELECT @Id;
END
GO

-- Update sp_Tasks_Update to include scheduling fields
ALTER PROCEDURE sp_Tasks_Update
    @Id             UNIQUEIDENTIFIER,
    @Title          NVARCHAR(200),
    @Description    NVARCHAR(MAX)   = NULL,
    @Status         NVARCHAR(20),
    @Priority       NVARCHAR(20),
    @AssignedTo     NVARCHAR(100)   = NULL,
    @TokensUsed     INT             = 0,
    @ApiCalls       INT             = 0,
    @EstimatedCost  DECIMAL(18,6)   = 0,
    @Currency       NVARCHAR(10)    = 'USD',
    @ScheduledAt    DATETIME2       = NULL,
    @RecurrenceType NVARCHAR(20)    = 'None',
    @RecurrenceInterval INT         = 0,
    @RecurrenceDays NVARCHAR(50)    = NULL,
    @NextRunAt      DATETIME2       = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Tasks SET
        Title = @Title,
        Description = @Description,
        Status = @Status,
        Priority = @Priority,
        AssignedTo = @AssignedTo,
        TokensUsed = @TokensUsed,
        ApiCalls = @ApiCalls,
        EstimatedCost = @EstimatedCost,
        Currency = @Currency,
        UpdatedAt = SYSUTCDATETIME(),
        ScheduledAt = @ScheduledAt,
        RecurrenceType = @RecurrenceType,
        RecurrenceInterval = @RecurrenceInterval,
        RecurrenceDays = @RecurrenceDays,
        NextRunAt = @NextRunAt
    WHERE Id = @Id;
END
GO

-- Update sp_Tasks_GetAll to return new columns
ALTER PROCEDURE sp_Tasks_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Title, Description, Status, Priority, CreatedAt, UpdatedAt, 
           AssignedTo, TokensUsed, ApiCalls, EstimatedCost, Currency,
           ScheduledAt, RecurrenceType, RecurrenceInterval, RecurrenceDays, 
           NextRunAt, LastRunAt
    FROM Tasks
    ORDER BY CreatedAt DESC;
END
GO

-- Update sp_Tasks_GetById
ALTER PROCEDURE sp_Tasks_GetById
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Title, Description, Status, Priority, CreatedAt, UpdatedAt, 
           AssignedTo, TokensUsed, ApiCalls, EstimatedCost, Currency,
           ScheduledAt, RecurrenceType, RecurrenceInterval, RecurrenceDays, 
           NextRunAt, LastRunAt
    FROM Tasks
    WHERE Id = @Id;
END
GO
