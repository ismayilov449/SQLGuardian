-- Columns, wildcards, and predicates for visitor coverage.

SELECT * FROM Products

SELECT *
FROM dbo.Products AS p
WHERE p.Name LIKE '%widget%'
  AND p.CategoryId IN (1, 2, 3)
  AND p.Discontinued IS NULL
  AND EXISTS (
      SELECT 1
      FROM dbo.Inventory AS i
      WHERE i.ProductId = p.Id
  );
