-- Drops every numeric CHECK constraint. From here on, the numeric bounds live only in
-- ValidationLimits and in the web's matching *_MAX constants.
--
-- Why: the caps are magnitudes in pesos and they age. The shipment ceiling of 1.000.000 was already
-- 80% consumed — the average shipment went from $27.090 in 2023 to $230.168 in 2026 — and every time
-- inflation catches up with one of these numbers, moving it means a DROP and recreate against the
-- live database. That is a migration, a rehearsal and a maintenance window for what is really just a
-- typo guard. Keeping the bounds in code alone makes the same change a one-line edit and a deploy.
--
-- What this gives up: these constraints mixed the ceiling with a floor (> 0, >= 0), and the floor is
-- what stopped a negative price or a zero quantity reaching a column. The floor never ages, so if it
-- is ever wanted back it belongs in a constraint of its own with no upper bound at all — that one
-- would never need another migration.
--
-- Length constraints are deliberately left alone: a column width is not affected by inflation, and
-- they still backstop the NVARCHAR sizes. CK_AuditEntry_Core_Lengths stays for the same reason even
-- though it carries a UserId > 0 clause, because dropping it would take the length guards with it.
--
-- This file is not additive, but it is the cheap kind of DROP: a CHECK is metadata, so nothing is
-- rewritten and no data moves. Dropping a constraint can never make existing rows invalid.
--
-- The last four names below come from migrations 08 and 09. They are listed so a database that
-- already ran those from an earlier checkout ends up in the same state as one that never did.

-- Every comparison against sys.check_constraints.name is forced to Latin1_General_BIN2 on both
-- sides, the same way migration 04 matches sys.columns.name. The database collation is
-- Modern_Spanish_CI_AS while the catalog views carry the server/catalog collation
-- (SQL_Latin1_General_CP1_CI_AS on Azure SQL), and a table variable column or a local variable takes
-- the database default, so an unqualified equality between the two raises "Cannot resolve the
-- collation conflict" and rolls the whole file back. A binary collation is also the correct
-- semantics here: these are identifiers, matched exactly.

DECLARE @numericConstraints TABLE (
    ConstraintName SYSNAME NOT NULL
);

INSERT INTO @numericConstraints (ConstraintName)
VALUES
    (N'CK_Product_Stock_Range'),
    (N'CK_Product_Price_Range'),
    (N'CK_Product_BreakPrice_Range'),
    (N'CK_ProductOrder_Quantity_Range'),
    (N'CK_ProductOrder_UnitPrice_Range'),
    (N'CK_Order_ShipmentPrice_Range'),
    (N'CK_Expense_Price_Range'),
    (N'CK_User_RoleId_Range'),
    (N'CK_ProductPriceHistory_Prices'),
    (N'CK_ProductPriceHistory_Counts'),
    (N'CK_ProductVariant_Stock_Range'),
    (N'CK_ProductVariant_PriceExtra_Range');

DECLARE @constraintName SYSNAME;
DECLARE @schemaName SYSNAME;
DECLARE @tableName SYSNAME;
DECLARE @dropSql NVARCHAR(MAX);

-- Guarded on the constraint still existing, so a rerun drops nothing and reports no error
WHILE EXISTS (
    SELECT 1
    FROM @numericConstraints c
    JOIN sys.check_constraints k ON k.name COLLATE Latin1_General_BIN2 = c.ConstraintName COLLATE Latin1_General_BIN2
)
BEGIN
    SELECT TOP 1
        @constraintName = k.name,
        @schemaName = SCHEMA_NAME(t.schema_id),
        @tableName = t.name
    FROM @numericConstraints c
    JOIN sys.check_constraints k ON k.name COLLATE Latin1_General_BIN2 = c.ConstraintName COLLATE Latin1_General_BIN2
    JOIN sys.tables t ON t.object_id = k.parent_object_id;

    SET @dropSql =
        N'ALTER TABLE ' + QUOTENAME(@schemaName) + N'.' + QUOTENAME(@tableName)
        + N' DROP CONSTRAINT ' + QUOTENAME(@constraintName) + N';';

    EXEC sys.sp_executesql @dropSql;

    DELETE FROM @numericConstraints
    WHERE ConstraintName COLLATE Latin1_General_BIN2 = @constraintName COLLATE Latin1_General_BIN2;
END
