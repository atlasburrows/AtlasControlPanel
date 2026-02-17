-- CredentialGroups table
CREATE TABLE CredentialGroups (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(255) NOT NULL,
    Category NVARCHAR(100) NULL,
    Description NVARCHAR(500) NULL,
    Icon NVARCHAR(50) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Junction table
CREATE TABLE CredentialGroupMembers (
    GroupId UNIQUEIDENTIFIER NOT NULL,
    CredentialId UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_CredentialGroupMembers PRIMARY KEY (GroupId, CredentialId),
    CONSTRAINT FK_CGM_Group FOREIGN KEY (GroupId) REFERENCES CredentialGroups(Id) ON DELETE CASCADE,
    CONSTRAINT FK_CGM_Credential FOREIGN KEY (CredentialId) REFERENCES SecureCredentials(Id) ON DELETE CASCADE
);

CREATE INDEX IX_CredentialGroupMembers_CredentialId ON CredentialGroupMembers(CredentialId);
GO

-- sp_CredentialGroups_GetAll
CREATE OR ALTER PROCEDURE sp_CredentialGroups_GetAll
AS
BEGIN
    SELECT * FROM CredentialGroups ORDER BY Name;
END
GO

-- sp_CredentialGroups_GetById
CREATE OR ALTER PROCEDURE sp_CredentialGroups_GetById
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SELECT * FROM CredentialGroups WHERE Id = @Id;
END
GO

-- sp_CredentialGroups_GetByName
CREATE OR ALTER PROCEDURE sp_CredentialGroups_GetByName
    @Name NVARCHAR(255)
AS
BEGIN
    SELECT TOP 1 * FROM CredentialGroups WHERE Name = @Name;
END
GO

-- sp_CredentialGroups_Create
CREATE OR ALTER PROCEDURE sp_CredentialGroups_Create
    @Name NVARCHAR(255),
    @Category NVARCHAR(100) = NULL,
    @Description NVARCHAR(500) = NULL,
    @Icon NVARCHAR(50) = NULL
AS
BEGIN
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO CredentialGroups (Id, Name, Category, Description, Icon, CreatedAt, UpdatedAt)
    VALUES (@Id, @Name, @Category, @Description, @Icon, GETUTCDATE(), GETUTCDATE());
    SELECT @Id;
END
GO

-- sp_CredentialGroups_Delete
CREATE OR ALTER PROCEDURE sp_CredentialGroups_Delete
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    DELETE FROM CredentialGroupMembers WHERE GroupId = @Id;
    DELETE FROM CredentialGroups WHERE Id = @Id;
END
GO

-- sp_CredentialGroups_AddMember
CREATE OR ALTER PROCEDURE sp_CredentialGroups_AddMember
    @GroupId UNIQUEIDENTIFIER,
    @CredentialId UNIQUEIDENTIFIER
AS
BEGIN
    IF NOT EXISTS (SELECT 1 FROM CredentialGroupMembers WHERE GroupId = @GroupId AND CredentialId = @CredentialId)
        INSERT INTO CredentialGroupMembers (GroupId, CredentialId) VALUES (@GroupId, @CredentialId);
END
GO

-- sp_CredentialGroups_RemoveMember
CREATE OR ALTER PROCEDURE sp_CredentialGroups_RemoveMember
    @GroupId UNIQUEIDENTIFIER,
    @CredentialId UNIQUEIDENTIFIER
AS
BEGIN
    DELETE FROM CredentialGroupMembers WHERE GroupId = @GroupId AND CredentialId = @CredentialId;
END
GO

-- sp_CredentialGroups_GetMembers
CREATE OR ALTER PROCEDURE sp_CredentialGroups_GetMembers
    @GroupId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT c.* FROM SecureCredentials c
    INNER JOIN CredentialGroupMembers m ON c.Id = m.CredentialId
    WHERE m.GroupId = @GroupId;
END
GO
