IF COL_LENGTH(N'dbo.User', N'CreatedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[User]
    ADD [CreatedBy] INT NULL,
        [UpdatedBy] INT NULL,
        [DeletedBy] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_User_User_CreatedBy')
BEGIN
    ALTER TABLE [dbo].[User]
    ADD CONSTRAINT [FK_User_User_CreatedBy]
        FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_User_User_UpdatedBy')
BEGIN
    ALTER TABLE [dbo].[User]
    ADD CONSTRAINT [FK_User_User_UpdatedBy]
        FOREIGN KEY ([UpdatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_User_User_DeletedBy')
BEGIN
    ALTER TABLE [dbo].[User]
    ADD CONSTRAINT [FK_User_User_DeletedBy]
        FOREIGN KEY ([DeletedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF COL_LENGTH(N'dbo.GenericItem', N'CreatedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[GenericItem]
    ADD [CreatedBy] INT NULL,
        [UpdatedBy] INT NULL,
        [DeletedBy] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_GenericItem_User_CreatedBy')
BEGIN
    ALTER TABLE [dbo].[GenericItem]
    ADD CONSTRAINT [FK_GenericItem_User_CreatedBy]
        FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_GenericItem_User_UpdatedBy')
BEGIN
    ALTER TABLE [dbo].[GenericItem]
    ADD CONSTRAINT [FK_GenericItem_User_UpdatedBy]
        FOREIGN KEY ([UpdatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_GenericItem_User_DeletedBy')
BEGIN
    ALTER TABLE [dbo].[GenericItem]
    ADD CONSTRAINT [FK_GenericItem_User_DeletedBy]
        FOREIGN KEY ([DeletedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF COL_LENGTH(N'dbo.Product', N'CreatedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD [CreatedBy] INT NULL,
        [UpdatedBy] INT NULL,
        [DeletedBy] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Product_User_CreatedBy')
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD CONSTRAINT [FK_Product_User_CreatedBy]
        FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Product_User_UpdatedBy')
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD CONSTRAINT [FK_Product_User_UpdatedBy]
        FOREIGN KEY ([UpdatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Product_User_DeletedBy')
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD CONSTRAINT [FK_Product_User_DeletedBy]
        FOREIGN KEY ([DeletedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF COL_LENGTH(N'dbo.Cart', N'CreatedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[Cart]
    ADD [CreatedBy] INT NULL,
        [UpdatedBy] INT NULL,
        [DeletedBy] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cart_User_CreatedBy')
BEGIN
    ALTER TABLE [dbo].[Cart]
    ADD CONSTRAINT [FK_Cart_User_CreatedBy]
        FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cart_User_UpdatedBy')
BEGIN
    ALTER TABLE [dbo].[Cart]
    ADD CONSTRAINT [FK_Cart_User_UpdatedBy]
        FOREIGN KEY ([UpdatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cart_User_DeletedBy')
BEGIN
    ALTER TABLE [dbo].[Cart]
    ADD CONSTRAINT [FK_Cart_User_DeletedBy]
        FOREIGN KEY ([DeletedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF COL_LENGTH(N'dbo.CartProduct', N'CreatedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[CartProduct]
    ADD [CreatedBy] INT NULL,
        [UpdatedBy] INT NULL,
        [DeletedBy] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CartProduct_User_CreatedBy')
BEGIN
    ALTER TABLE [dbo].[CartProduct]
    ADD CONSTRAINT [FK_CartProduct_User_CreatedBy]
        FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CartProduct_User_UpdatedBy')
BEGIN
    ALTER TABLE [dbo].[CartProduct]
    ADD CONSTRAINT [FK_CartProduct_User_UpdatedBy]
        FOREIGN KEY ([UpdatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CartProduct_User_DeletedBy')
BEGIN
    ALTER TABLE [dbo].[CartProduct]
    ADD CONSTRAINT [FK_CartProduct_User_DeletedBy]
        FOREIGN KEY ([DeletedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF COL_LENGTH(N'dbo.MercadoPagoPayment', N'CreatedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[MercadoPagoPayment]
    ADD [CreatedBy] INT NULL,
        [UpdatedBy] INT NULL,
        [DeletedBy] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MercadoPagoPayment_User_CreatedBy')
BEGIN
    ALTER TABLE [dbo].[MercadoPagoPayment]
    ADD CONSTRAINT [FK_MercadoPagoPayment_User_CreatedBy]
        FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MercadoPagoPayment_User_UpdatedBy')
BEGIN
    ALTER TABLE [dbo].[MercadoPagoPayment]
    ADD CONSTRAINT [FK_MercadoPagoPayment_User_UpdatedBy]
        FOREIGN KEY ([UpdatedBy]) REFERENCES [dbo].[User] ([Id]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MercadoPagoPayment_User_DeletedBy')
BEGIN
    ALTER TABLE [dbo].[MercadoPagoPayment]
    ADD CONSTRAINT [FK_MercadoPagoPayment_User_DeletedBy]
        FOREIGN KEY ([DeletedBy]) REFERENCES [dbo].[User] ([Id]);
END
