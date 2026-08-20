-- Payment state on Order, plus the append-only log of catalogue price changes.
--
-- Raising a product price re-prices the lines of orders that are not paid and whose event has not
-- happened yet; a paid order stays frozen at what the client was quoted. Order had no notion of
-- payment at all, so the two columns below are what makes that rule expressible.
--
-- MigrationRunner sends this whole file as a single batch, and T-SQL resolves column names of an
-- already existing table at compile time. Anything that names [Order].[PaidAt] or [PaidBy] therefore
-- goes through EXEC sys.sp_executesql, or the batch fails with "Invalid column name" before the
-- ALTER TABLE above it ever runs.

-- [Order] ------------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Order', N'PaidAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order]
    ADD [PaidAt] DATETIME2 NULL,
        [PaidBy] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Order_User_PaidBy')
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE [dbo].[Order]
        ADD CONSTRAINT [FK_Order_User_PaidBy]
            FOREIGN KEY ([PaidBy]) REFERENCES [dbo].[User] ([Id]);';
END

-- Filtered: the re-pricing query only ever looks for orders that are still unpaid
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Order_PaidAt' AND object_id = OBJECT_ID(N'[dbo].[Order]'))
BEGIN
    EXEC sys.sp_executesql N'
        CREATE NONCLUSTERED INDEX [IX_Order_PaidAt]
        ON [dbo].[Order] ([Date])
        INCLUDE ([Id])
        WHERE [PaidAt] IS NULL;';
END

-- [ProductPriceHistory] ----------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[ProductPriceHistory]')
    AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[ProductPriceHistory] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ProductPriceHistory] PRIMARY KEY,
        [ProductId] INT NOT NULL,
        [PreviousPrice] DECIMAL(18, 2) NOT NULL,
        [NewPrice] DECIMAL(18, 2) NOT NULL,
        [PreviousBreakPrice] DECIMAL(18, 2) NULL,
        [NewBreakPrice] DECIMAL(18, 2) NULL,
        [ChangedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ProductPriceHistory_ChangedAt] DEFAULT (SYSUTCDATETIME()),
        [ChangedBy] INT NULL,
        [RepricedOrders] INT NOT NULL CONSTRAINT [DF_ProductPriceHistory_RepricedOrders] DEFAULT (0),
        [RepricedLines] INT NOT NULL CONSTRAINT [DF_ProductPriceHistory_RepricedLines] DEFAULT (0),
        [SkippedLines] INT NOT NULL CONSTRAINT [DF_ProductPriceHistory_SkippedLines] DEFAULT (0),
        CONSTRAINT [FK_ProductPriceHistory_Product_ProductId]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]),
        CONSTRAINT [FK_ProductPriceHistory_User_ChangedBy]
            FOREIGN KEY ([ChangedBy]) REFERENCES [dbo].[User] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_ProductPriceHistory_ProductId_ChangedAt]
    ON [dbo].[ProductPriceHistory] ([ProductId], [ChangedAt] DESC);
END


-- [audit] ------------------------------------------------------------------------------------
-- AuditEntry.ActionCode has a foreign key to audit.AuditAction, so a new code in AuditActionCode
-- only works once its row exists here
MERGE [audit].[AuditAction] AS target
USING (VALUES
    ('ORDER_PAID', N'Pedido cobrado', 'DATA', 0),
    ('ORDER_UNPAID', N'Cobro anulado', 'DATA', 0)
) AS source ([ActionCode], [ActionName], [CategoryCode], [IsInternal])
ON target.[ActionCode] = source.[ActionCode]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([ActionCode], [ActionName], [CategoryCode], [IsInternal])
    VALUES (source.[ActionCode], source.[ActionName], source.[CategoryCode], source.[IsInternal]);
