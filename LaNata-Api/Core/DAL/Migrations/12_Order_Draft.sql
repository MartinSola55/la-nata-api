-- A pending quote is an order that does not reserve anything yet. IsDraft = 1 means "presupuesto":
-- the row holds the client, the date and the whole line set, but every availability query skips it,
-- so the same stock stays free for whoever books it first. Confirming it flips the flag, and that is
-- the moment the stock is checked.
--
-- Additive: one NOT NULL column with a default, so every existing order reads as confirmed, which is
-- what all 2200 of them are.
--
-- EXEC sys.sp_executesql on anything naming the new column: MigrationRunner sends the file as a
-- single batch and T-SQL resolves column names of an already existing table at compile time.

-- [Order] ------------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Order', N'IsDraft') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order]
    ADD [IsDraft] BIT NOT NULL CONSTRAINT [DF_Order_IsDraft] DEFAULT (0);
END

-- Filtered on the drafts alone: they are the small side of the table and the only one the listing
-- ever asks for by itself. The reservation queries stay on the date range and read IsDraft as a
-- residual predicate, which is why there is no index on ([Date], [IsDraft]).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Order_IsDraft' AND object_id = OBJECT_ID(N'[dbo].[Order]'))
BEGIN
    EXEC sys.sp_executesql N'
        CREATE NONCLUSTERED INDEX [IX_Order_IsDraft]
        ON [dbo].[Order] ([Date])
        INCLUDE ([Id])
        WHERE [IsDraft] = 1;';
END

-- [audit] ------------------------------------------------------------------------------------
-- AuditEntry.ActionCode has a foreign key to audit.AuditAction, so a new code in AuditActionCode
-- only works once its row exists here
MERGE [audit].[AuditAction] AS target
USING (VALUES
    ('ORDER_CONFIRMED', N'Pedido confirmado', 'DATA', 0)
) AS source ([ActionCode], [ActionName], [CategoryCode], [IsInternal])
ON target.[ActionCode] = source.[ActionCode]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([ActionCode], [ActionName], [CategoryCode], [IsInternal])
    VALUES (source.[ActionCode], source.[ActionName], source.[CategoryCode], source.[IsInternal]);
