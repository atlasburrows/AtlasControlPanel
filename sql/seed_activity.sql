DECLARE @PId UNIQUEIDENTIFIER;

-- Parent: Drag and drop
SET @PId = NEWID();
INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (@PId, 'Implemented drag and drop', 'Added HTML5 drag/drop to task board with review reject workflow', GETUTCDATE(), 'FileAccess', 0, 0, 0, 'USD', NULL, NULL);

INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (NEWID(), 'Edited TaskBoard.razor', 'Added ondragstart, ondrop handlers and ondragover preventDefault', DATEADD(SECOND, 1, GETUTCDATE()), 'FileAccess', 0, 0, 0, 'USD', @PId, 'Added draggable="true" to task cards with drag start/drop event handlers on columns');

INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (NEWID(), 'Added reject dialog', 'Created modal for Review to InProgress with message input', DATEADD(SECOND, 2, GETUTCDATE()), 'FileAccess', 0, 0, 0, 'USD', @PId, 'When dragging from Review to InProgress, shows dialog asking for reason. Message prepended to task description.');

INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (NEWID(), 'Updated atlas-shared.css', 'Added drag cursor and hover states', DATEADD(SECOND, 3, GETUTCDATE()), 'FileAccess', 0, 0, 0, 'USD', @PId, '.task-card[draggable="true"] { cursor: grab; }
.task-card[draggable="true"]:active { cursor: grabbing; opacity: 0.7; }');

-- Parent: Activity system rework  
SET @PId = NEWID();
INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (@PId, 'Redesigned activity logging', 'Added hierarchical parent/child activity entries with expandable details', DATEADD(SECOND, 10, GETUTCDATE()), 'CommandExecution', 0, 0, 0, 'USD', NULL, NULL);

INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (NEWID(), 'Altered ActivityLogs table', 'Added ParentId and Details columns to support hierarchy', DATEADD(SECOND, 11, GETUTCDATE()), 'CommandExecution', 0, 0, 0, 'USD', @PId, 'ALTER TABLE ActivityLogs ADD ParentId UNIQUEIDENTIFIER NULL;
ALTER TABLE ActivityLogs ADD Details NVARCHAR(MAX) NULL;');

INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (NEWID(), 'Created sp_ActivityLogs_GetWithDetails', 'New stored proc returns parents + children in two result sets', DATEADD(SECOND, 12, GETUTCDATE()), 'CommandExecution', 0, 0, 0, 'USD', @PId, 'Uses QueryMultipleAsync to return parent entries and their children in a single call');

INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (NEWID(), 'Updated ActivityLog.cs entity', 'Added ParentId, Details, and SubEntries properties', DATEADD(SECOND, 13, GETUTCDATE()), 'FileAccess', 0, 0, 0, 'USD', @PId, 'public Guid? ParentId { get; set; }
public string? Details { get; set; }
public List<ActivityLog> SubEntries { get; set; } = new();');

INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (NEWID(), 'Rewrote ActivityLogPage.razor', 'Expandable entries with click-to-expand, code blocks, sub-entry timeline', DATEADD(SECOND, 14, GETUTCDATE()), 'FileAccess', 0, 0, 0, 'USD', @PId, 'Replaced MudTable with custom expandable entry layout. Each parent shows summary, clicking reveals sub-actions with code blocks and details.');
