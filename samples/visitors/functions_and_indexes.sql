-- Functions and index DDL for visitor coverage.

SELECT dbo.GetTax(o.Total), COUNT(*) AS OrderCount
FROM dbo.Orders AS o
CROSS APPLY dbo.SplitTags(o.Tags) AS t
GROUP BY o.Total;

CREATE UNIQUE CLUSTERED INDEX IX_Users_Email
ON dbo.Users (Email);

ALTER INDEX IX_Users_Email ON dbo.Users REBUILD;

DROP INDEX IX_Users_Email ON dbo.Users;
