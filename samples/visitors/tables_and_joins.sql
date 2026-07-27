-- Tables, joins, and aliases for visitor coverage.

SELECT u.Id, o.Total
FROM dbo.Users AS u
INNER JOIN dbo.Orders AS o ON o.UserId = u.Id
CROSS JOIN dbo.Regions AS r
WHERE u.IsActive = 1
  AND o.Total > 100;
