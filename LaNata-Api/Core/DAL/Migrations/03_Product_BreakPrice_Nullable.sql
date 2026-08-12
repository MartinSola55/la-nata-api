IF COL_LENGTH(N'dbo.Product', N'break_price') IS NOT NULL
    AND COLUMNPROPERTY(OBJECT_ID(N'dbo.Product'), N'break_price', 'AllowsNull') = 0
BEGIN
    ALTER TABLE [dbo].[Product]
    ALTER COLUMN [break_price] FLOAT NULL;
END
