IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Cart]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Cart] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UserId] INT NOT NULL,
        [Status] NVARCHAR(50) NOT NULL,
        [Total] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [PaidAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_Cart_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
    );

    CREATE INDEX [IX_Cart_UserId] ON [dbo].[Cart] ([UserId]);
    CREATE INDEX [IX_Cart_Status] ON [dbo].[Cart] ([Status]);
END

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CartProduct]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[CartProduct] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [CartId] INT NOT NULL,
        [ProductId] INT NOT NULL,
        [ProductName] NVARCHAR(150) NOT NULL,
        [Quantity] INT NOT NULL,
        [SettedPrice] DECIMAL(18,2) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_CartProduct_Cart_CartId] FOREIGN KEY ([CartId]) REFERENCES [dbo].[Cart] ([Id]),
        CONSTRAINT [FK_CartProduct_Product_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id])
    );

    CREATE INDEX [IX_CartProduct_CartId] ON [dbo].[CartProduct] ([CartId]);
    CREATE INDEX [IX_CartProduct_ProductId] ON [dbo].[CartProduct] ([ProductId]);
END

IF COL_LENGTH(N'dbo.MercadoPagoPayment', N'CartId') IS NULL
BEGIN
    ALTER TABLE [dbo].[MercadoPagoPayment]
    ADD [CartId] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MercadoPagoPayment_CartId' AND object_id = OBJECT_ID(N'[dbo].[MercadoPagoPayment]'))
BEGIN
    CREATE INDEX [IX_MercadoPagoPayment_CartId]
        ON [dbo].[MercadoPagoPayment] ([CartId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MercadoPagoPayment_Cart_CartId')
BEGIN
    ALTER TABLE [dbo].[MercadoPagoPayment]
    ADD CONSTRAINT [FK_MercadoPagoPayment_Cart_CartId]
        FOREIGN KEY ([CartId]) REFERENCES [dbo].[Cart] ([Id]);
END
