DECLARE @PId UNIQUEIDENTIFIER = NEWID();

INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (@PId, 'Created Atlas profile banner', 'Designed CSS constellation banner for sidebar representing Atlas identity', GETUTCDATE(), 'FileAccess', 0, 0, 0, 'USD', NULL, NULL);

INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (NEWID(), 'Edited NavMenu.razor', 'Added banner HTML structure with stars, constellation lines, globe, and tagline', DATEADD(SECOND, 1, GETUTCDATE()), 'FileAccess', 0, 0, 0, 'USD', @PId, 'Banner contains: 8 animated twinkling stars, 3 constellation connection lines, a globe element with CSS rings, and tagline "Navigate. Build. Protect."');

INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, EstimatedCost, Currency, ParentId, Details)
VALUES (NEWID(), 'Added banner CSS to atlas-shared.css', 'Created .atlas-banner with gradient background, star animations, globe effect', DATEADD(SECOND, 2, GETUTCDATE()), 'FileAccess', 0, 0, 0, 'USD', @PId, 'Background: linear-gradient(135deg, #0c1929 to #1a1040)
Stars: @keyframes twinkle with opacity/scale pulse
Globe: radial-gradient with box-shadow glow
Tagline: gradient text (blue to purple)');
