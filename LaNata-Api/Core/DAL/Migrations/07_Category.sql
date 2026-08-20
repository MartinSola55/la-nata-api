-- Product categories: a new lookup table plus a nullable foreign key on Product, so the price list
-- can be printed per category (only vajilla, only mobiliario) instead of as one long listing.
--
-- MigrationRunner sends this whole file as a single batch, and T-SQL resolves column names of an
-- already existing table at compile time. Anything that names [Product].[CategoryId] therefore goes
-- through EXEC sys.sp_executesql, or the batch fails with "Invalid column name" before the ALTER
-- TABLE above it ever runs.

-- [Category] ---------------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[Category]')
    AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[Category] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Category] PRIMARY KEY,
        [Description] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Category_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [DeletedAt] DATETIME2 NULL,
        [CreatedBy] INT NULL,
        [UpdatedBy] INT NULL,
        [DeletedBy] INT NULL,
        CONSTRAINT [FK_Category_User_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([Id]),
        CONSTRAINT [FK_Category_User_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [dbo].[User] ([Id]),
        CONSTRAINT [FK_Category_User_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [dbo].[User] ([Id])
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Category_Description_Required_Length')
BEGIN
    ALTER TABLE [dbo].[Category] WITH NOCHECK
    ADD CONSTRAINT [CK_Category_Description_Required_Length]
    CHECK ([Description] IS NOT NULL AND LEN(LTRIM(RTRIM([Description]))) > 0 AND LEN([Description]) <= 100);
END

-- Filtered on DeletedAt so the name of a soft deleted category can be used again
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Category_Description' AND object_id = OBJECT_ID(N'[dbo].[Category]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Category_Description]
    ON [dbo].[Category] ([Description])
    WHERE [DeletedAt] IS NULL;
END

-- [Product] ----------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Product', N'CategoryId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD [CategoryId] INT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Product_Category_CategoryId')
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE [dbo].[Product]
        ADD CONSTRAINT [FK_Product_Category_CategoryId]
            FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Category] ([Id]);';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Product_CategoryId' AND object_id = OBJECT_ID(N'[dbo].[Product]'))
BEGIN
    EXEC sys.sp_executesql N'
        CREATE NONCLUSTERED INDEX [IX_Product_CategoryId]
        ON [dbo].[Product] ([CategoryId]);';
END
