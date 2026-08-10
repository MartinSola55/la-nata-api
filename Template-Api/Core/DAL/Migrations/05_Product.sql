IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Product]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Product] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Name] NVARCHAR(150) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [Price] DECIMAL(18,2) NOT NULL,
        [Stock] INT NOT NULL DEFAULT 0,
        [PictureUrl] NVARCHAR(MAX) NULL,
        [CategoryId] NVARCHAR(100) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NULL,
        [DeletedAt] DATETIME2 NULL
    );

    CREATE UNIQUE INDEX [IX_Product_Name] ON [dbo].[Product] ([Name]) WHERE [DeletedAt] IS NULL;
    CREATE INDEX [IX_Product_IsActive] ON [dbo].[Product] ([IsActive]);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[Product] WHERE [Name] = N'Producto de prueba')
BEGIN
    INSERT INTO [dbo].[Product] (
        [Name],
        [Description],
        [Price],
        [Stock],
        [CategoryId],
        [IsActive]
    )
    VALUES (
        N'Producto de prueba',
        N'Producto genérico para simular una compra con Mercado Pago.',
        1000.00,
        10,
        N'test',
        1
    );
END
