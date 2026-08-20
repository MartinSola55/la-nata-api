-- A price log row now belongs either to the product (VariantId NULL, what every existing row is) or
-- to one of its variants, carrying the surcharge on both sides. Additive: nullable columns, an FK and
-- an index.
--
-- EXEC sys.sp_executesql on anything naming the new columns: MigrationRunner sends the file as a
-- single batch and T-SQL resolves them at compile time.

-- [ProductPriceHistory] ----------------------------------------------------------------------
IF COL_LENGTH(N'dbo.ProductPriceHistory', N'VariantId') IS NULL
BEGIN
    ALTER TABLE [dbo].[ProductPriceHistory]
    ADD [VariantId] INT NULL,
        [PreviousPriceExtra] DECIMAL(18, 2) NULL,
        [NewPriceExtra] DECIMAL(18, 2) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductPriceHistory_ProductVariant_VariantId')
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE [dbo].[ProductPriceHistory]
        ADD CONSTRAINT [FK_ProductPriceHistory_ProductVariant_VariantId]
            FOREIGN KEY ([VariantId]) REFERENCES [dbo].[ProductVariant] ([Id]);';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductPriceHistory_VariantId' AND object_id = OBJECT_ID(N'[dbo].[ProductPriceHistory]'))
BEGIN
    EXEC sys.sp_executesql N'
        CREATE NONCLUSTERED INDEX [IX_ProductPriceHistory_VariantId]
        ON [dbo].[ProductPriceHistory] ([VariantId])
        WHERE [VariantId] IS NOT NULL;';
END
