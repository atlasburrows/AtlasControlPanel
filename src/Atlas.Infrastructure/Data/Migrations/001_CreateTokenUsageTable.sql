-- Create TokenUsage table for granular per-request tracking
CREATE TABLE TokenUsage (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Provider NVARCHAR(50) NOT NULL,        -- e.g. 'anthropic'
    Model NVARCHAR(100) NOT NULL,          -- e.g. 'claude-opus-4-6'
    InputTokens INT NOT NULL DEFAULT 0,
    OutputTokens INT NOT NULL DEFAULT 0,
    TotalTokens INT NOT NULL DEFAULT 0,
    CostUsd DECIMAL(10,6) NOT NULL DEFAULT 0,
    DurationMs INT NULL,
    SessionKey NVARCHAR(200) NULL,         -- which session used it
    TaskCategory NVARCHAR(50) NULL,        -- Development, Research, etc.
    ContextPercent INT NULL,
    INDEX IX_TokenUsage_Timestamp (Timestamp DESC),
    INDEX IX_TokenUsage_Model (Model),
    INDEX IX_TokenUsage_Session (SessionKey)
);

-- To apply this migration on localhost, run:
-- sqlcmd -S localhost -E -d AtlasControlPanel -i "001_CreateTokenUsageTable.sql"
