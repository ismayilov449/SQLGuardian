-- Demo: bad query for SQLGuardian recommendations (use with DemoOrders/DemoCustomers schema).
SELECT *
FROM dbo.DemoOrders AS o
INNER JOIN dbo.DemoCustomers AS c ON o.CustomerId = c.Id;
