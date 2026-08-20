-- Product variants with their own stock, plus the ProductOrder key change that makes them possible.
--
-- ############################################################################################
-- THIS FILE IS NOT ADDITIVE. It rebuilds PK_ProductOrder on a table that holds every order line
-- ever written. Back up the database and rehearse against a restored copy before running it, and
-- prefer running it by hand in a maintenance window with Database:RunSqlMigrations set to false
-- rather than letting a deploy trigger it.
-- ############################################################################################
--
-- Why the key has to change: with variants, one order must be able to hold the same product at the
-- same price twice with different variants. VariantId is nullable ("sin variante"), and a primary
-- key column cannot be nullable, so it cannot join the old composite key. A surrogate identity takes
-- over as the primary key and the uniqueness rule moves to a unique index, where SQL Server treats
-- NULLs as equal and therefore still allows only one "sin variante" line per product and price.
--
-- Migration 05 already dropped and recreated this same primary key, which is both the precedent for
-- the pattern and the proof that no foreign key references ProductOrder. The pre-flight below
-- refuses to run if that stopped being true.
--
-- MigrationRunner sends this whole file as a single batch, and T-SQL resolves column names of an
-- already existing table at compile time. Anything that names the new ProductOrder columns therefore
-- goes through EXEC sys.sp_executesql, or the batch fails with "Invalid column name" before the
-- ALTER TABLE above it ever runs.

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE referenced_object_id = OBJECT_ID(N'[dbo].[ProductOrder]'))
    THROW 51002, 'A foreign key now references ProductOrder; the primary key rebuild must be reviewed by hand.', 1;

-- [ProductVariant] ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[ProductVariant]')
    AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[ProductVariant] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ProductVariant] PRIMARY KEY,
        [ProductId] INT NOT NULL,
        [Description] NVARCHAR(100) NOT NULL,
        [Stock] INT NOT NULL CONSTRAINT [DF_ProductVariant_Stock] DEFAULT (0),
        [PriceExtra] DECIMAL(18, 2) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ProductVariant_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [DeletedAt] DATETIME2 NULL,
        [CreatedBy] INT NULL,
        [UpdatedBy] INT NULL,
        [DeletedBy] INT NULL,
        CONSTRAINT [FK_ProductVariant_Product_ProductId]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]),
        CONSTRAINT [FK_ProductVariant_User_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([Id]),
        CONSTRAINT [FK_ProductVariant_User_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [dbo].[User] ([Id]),
        CONSTRAINT [FK_ProductVariant_User_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [dbo].[User] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_ProductVariant_ProductId]
    ON [dbo].[ProductVariant] ([ProductId]);

    -- Filtered so the name of a soft deleted variant can be used again on the same product
    CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductVariant_Product_Description]
    ON [dbo].[ProductVariant] ([ProductId], [Description])
    WHERE [DeletedAt] IS NULL;
END

-- Only the length constraint: a column width does not age, while a numeric bound in pesos does, so
-- Stock and PriceExtra are bounded by ValidationLimits alone. See 10_Drop_Numeric_Check_Constraints.sql.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ProductVariant_Description_Required_Length')
BEGIN
    ALTER TABLE [dbo].[ProductVariant] WITH NOCHECK
    ADD CONSTRAINT [CK_ProductVariant_Description_Required_Length]
    CHECK ([Description] IS NOT NULL AND LEN(LTRIM(RTRIM([Description]))) > 0 AND LEN([Description]) <= 100);
END


-- [ProductOrder] -----------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.ProductOrder', N'VariantId') IS NULL
BEGIN
    ALTER TABLE [dbo].[ProductOrder]
    ADD [VariantId] INT NULL;
END

-- The key rebuild. Guarded on the surrogate column so a rerun is a no-op, and routed through
-- sp_executesql because every statement inside names a column added in this same batch.
IF COL_LENGTH(N'dbo.ProductOrder', N'Id') IS NULL
BEGIN
    DECLARE @productOrderPk SYSNAME;
    DECLARE @dropProductOrderPkSql NVARCHAR(MAX);

    SELECT @productOrderPk = [name]
    FROM sys.key_constraints
    WHERE [type] = N'PK'
        AND parent_object_id = OBJECT_ID(N'[dbo].[ProductOrder]');

    IF @productOrderPk IS NOT NULL
    BEGIN
        SET @dropProductOrderPkSql =
            N'ALTER TABLE [dbo].[ProductOrder] DROP CONSTRAINT '
            + QUOTENAME(@productOrderPk)
            + N';';

        EXEC sys.sp_executesql @dropProductOrderPkSql;
    END

    -- Rewrites the table under a schema modification lock while it numbers the existing rows
    EXEC sys.sp_executesql N'
        ALTER TABLE [dbo].[ProductOrder]
        ADD [Id] INT IDENTITY(1,1) NOT NULL;';

    EXEC sys.sp_executesql N'
        ALTER TABLE [dbo].[ProductOrder]
        ADD CONSTRAINT [PK_ProductOrder] PRIMARY KEY CLUSTERED ([Id]);';
END

-- Leads with ProductId exactly like the old clustered key did, so the availability query stays a
-- seek, and includes Quantity so its SUM never leaves the index
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductOrder_Product_Order_UnitPrice_Variant' AND object_id = OBJECT_ID(N'[dbo].[ProductOrder]'))
BEGIN
    EXEC sys.sp_executesql N'
        CREATE UNIQUE NONCLUSTERED INDEX [UX_ProductOrder_Product_Order_UnitPrice_Variant]
        ON [dbo].[ProductOrder] ([ProductId], [OrderId], [UnitPrice], [VariantId])
        INCLUDE ([Quantity]);';
END

-- Order.GetAll projects Products.Count and their Sum for every listed order, and until now that had
-- no usable index at all
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductOrder_OrderId' AND object_id = OBJECT_ID(N'[dbo].[ProductOrder]'))
BEGIN
    EXEC sys.sp_executesql N'
        CREATE NONCLUSTERED INDEX [IX_ProductOrder_OrderId]
        ON [dbo].[ProductOrder] ([OrderId])
        INCLUDE ([ProductId], [Quantity], [UnitPrice], [VariantId]);';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductOrder_VariantId' AND object_id = OBJECT_ID(N'[dbo].[ProductOrder]'))
BEGIN
    EXEC sys.sp_executesql N'
        CREATE NONCLUSTERED INDEX [IX_ProductOrder_VariantId]
        ON [dbo].[ProductOrder] ([VariantId])
        INCLUDE ([OrderId], [Quantity])
        WHERE [VariantId] IS NOT NULL;';
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductOrder_ProductVariant_VariantId')
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE [dbo].[ProductOrder] WITH NOCHECK
        ADD CONSTRAINT [FK_ProductOrder_ProductVariant_VariantId]
            FOREIGN KEY ([VariantId]) REFERENCES [dbo].[ProductVariant] ([Id]);';
END
