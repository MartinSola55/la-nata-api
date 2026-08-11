-- The legacy production schema is the source of truth: this script only ADDs the audit columns the
-- API needs, and never alters or drops anything that already exists.
--
-- Already present, so untouched: Product.created_at, Product.deleted_at, Order.created_at,
-- Order.deleted_at, Expense.deleted_at.
--
-- ProductOrder and Rol are deliberately left alone. ProductOrder is a join table the app hard
-- deletes from, and Rol is a two-row lookup.

-- [User] -------------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.User', N'created_at') IS NULL
BEGIN
    ALTER TABLE [dbo].[User]
    ADD [created_at] DATETIME NOT NULL CONSTRAINT [DF_User_created_at] DEFAULT (GETUTCDATE());
END

IF COL_LENGTH(N'dbo.User', N'updated_at') IS NULL
BEGIN
    ALTER TABLE [dbo].[User]
    ADD [updated_at] DATETIME NULL;
END

IF COL_LENGTH(N'dbo.User', N'deleted_at') IS NULL
BEGIN
    ALTER TABLE [dbo].[User]
    ADD [deleted_at] DATETIME NULL;
END

IF COL_LENGTH(N'dbo.User', N'created_by') IS NULL
BEGIN
    ALTER TABLE [dbo].[User]
    ADD [created_by] INT NULL,
        [updated_by] INT NULL,
        [deleted_by] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_User_User_created_by')
BEGIN
    ALTER TABLE [dbo].[User]
    ADD CONSTRAINT [FK_User_User_created_by]
        FOREIGN KEY ([created_by]) REFERENCES [dbo].[User] ([id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_User_User_updated_by')
BEGIN
    ALTER TABLE [dbo].[User]
    ADD CONSTRAINT [FK_User_User_updated_by]
        FOREIGN KEY ([updated_by]) REFERENCES [dbo].[User] ([id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_User_User_deleted_by')
BEGIN
    ALTER TABLE [dbo].[User]
    ADD CONSTRAINT [FK_User_User_deleted_by]
        FOREIGN KEY ([deleted_by]) REFERENCES [dbo].[User] ([id]);
END

-- [Product] ----------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Product', N'updated_at') IS NULL
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD [updated_at] DATETIME NULL;
END

IF COL_LENGTH(N'dbo.Product', N'created_by') IS NULL
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD [created_by] INT NULL,
        [updated_by] INT NULL,
        [deleted_by] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Product_User_created_by')
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD CONSTRAINT [FK_Product_User_created_by]
        FOREIGN KEY ([created_by]) REFERENCES [dbo].[User] ([id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Product_User_updated_by')
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD CONSTRAINT [FK_Product_User_updated_by]
        FOREIGN KEY ([updated_by]) REFERENCES [dbo].[User] ([id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Product_User_deleted_by')
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD CONSTRAINT [FK_Product_User_deleted_by]
        FOREIGN KEY ([deleted_by]) REFERENCES [dbo].[User] ([id]);
END

-- [Order] ------------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Order', N'updated_at') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order]
    ADD [updated_at] DATETIME NULL;
END

IF COL_LENGTH(N'dbo.Order', N'created_by') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order]
    ADD [created_by] INT NULL,
        [updated_by] INT NULL,
        [deleted_by] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Order_User_created_by')
BEGIN
    ALTER TABLE [dbo].[Order]
    ADD CONSTRAINT [FK_Order_User_created_by]
        FOREIGN KEY ([created_by]) REFERENCES [dbo].[User] ([id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Order_User_updated_by')
BEGIN
    ALTER TABLE [dbo].[Order]
    ADD CONSTRAINT [FK_Order_User_updated_by]
        FOREIGN KEY ([updated_by]) REFERENCES [dbo].[User] ([id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Order_User_deleted_by')
BEGIN
    ALTER TABLE [dbo].[Order]
    ADD CONSTRAINT [FK_Order_User_deleted_by]
        FOREIGN KEY ([deleted_by]) REFERENCES [dbo].[User] ([id]);
END

-- [Expense] ----------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Expense', N'created_at') IS NULL
BEGIN
    ALTER TABLE [dbo].[Expense]
    ADD [created_at] DATETIME NOT NULL CONSTRAINT [DF_Expense_created_at] DEFAULT (GETUTCDATE());
END

IF COL_LENGTH(N'dbo.Expense', N'updated_at') IS NULL
BEGIN
    ALTER TABLE [dbo].[Expense]
    ADD [updated_at] DATETIME NULL;
END

IF COL_LENGTH(N'dbo.Expense', N'created_by') IS NULL
BEGIN
    ALTER TABLE [dbo].[Expense]
    ADD [created_by] INT NULL,
        [updated_by] INT NULL,
        [deleted_by] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Expense_User_created_by')
BEGIN
    ALTER TABLE [dbo].[Expense]
    ADD CONSTRAINT [FK_Expense_User_created_by]
        FOREIGN KEY ([created_by]) REFERENCES [dbo].[User] ([id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Expense_User_updated_by')
BEGIN
    ALTER TABLE [dbo].[Expense]
    ADD CONSTRAINT [FK_Expense_User_updated_by]
        FOREIGN KEY ([updated_by]) REFERENCES [dbo].[User] ([id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Expense_User_deleted_by')
BEGIN
    ALTER TABLE [dbo].[Expense]
    ADD CONSTRAINT [FK_Expense_User_deleted_by]
        FOREIGN KEY ([deleted_by]) REFERENCES [dbo].[User] ([id]);
END
