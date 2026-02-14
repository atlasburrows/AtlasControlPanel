-- Chat message persistence
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatMessages')
BEGIN
    CREATE TABLE ChatMessages (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Role NVARCHAR(20) NOT NULL,  -- 'user' or 'assistant'
        Content NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ChatMessages_CreatedAt ON ChatMessages(CreatedAt);
END
GO

-- Get chat messages (most recent N, returned in chronological order)
CREATE OR ALTER PROCEDURE sp_ChatMessages_GetRecent
    @Limit INT = 100
AS
BEGIN
    SELECT Id, Role, Content, CreatedAt
    FROM (
        SELECT TOP (@Limit) Id, Role, Content, CreatedAt
        FROM ChatMessages
        ORDER BY CreatedAt DESC
    ) sub
    ORDER BY CreatedAt ASC;
END
GO

-- Add a chat message
CREATE OR ALTER PROCEDURE sp_ChatMessages_Add
    @Role NVARCHAR(20),
    @Content NVARCHAR(MAX)
AS
BEGIN
    INSERT INTO ChatMessages (Id, Role, Content, CreatedAt)
    VALUES (NEWID(), @Role, @Content, SYSUTCDATETIME());
END
GO
