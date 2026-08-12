IF OBJECT_ID(N'[dbo].[Product]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM [dbo].[Product] WHERE [Stock] < -2147483648 OR [Stock] > 2147483647)
        THROW 51000, 'Product.Stock contains values outside the INT range.', 1;

    ALTER TABLE [dbo].[Product] ALTER COLUMN [Stock] INT NOT NULL;
    ALTER TABLE [dbo].[Product] ALTER COLUMN [Price] DECIMAL(18, 2) NOT NULL;
    ALTER TABLE [dbo].[Product] ALTER COLUMN [BreakPrice] DECIMAL(18, 2) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Product_Description_Required_Length')
        ALTER TABLE [dbo].[Product] WITH NOCHECK
        ADD CONSTRAINT [CK_Product_Description_Required_Length]
        CHECK ([Description] IS NOT NULL AND LEN(LTRIM(RTRIM([Description]))) > 0 AND LEN([Description]) <= 255);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Product_Stock_Range')
        ALTER TABLE [dbo].[Product] WITH NOCHECK
        ADD CONSTRAINT [CK_Product_Stock_Range]
        CHECK ([Stock] >= 0 AND [Stock] <= 10000);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Product_Price_Range')
        ALTER TABLE [dbo].[Product] WITH NOCHECK
        ADD CONSTRAINT [CK_Product_Price_Range]
        CHECK ([Price] > 0 AND [Price] <= 10000000);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Product_BreakPrice_Range')
        ALTER TABLE [dbo].[Product] WITH NOCHECK
        ADD CONSTRAINT [CK_Product_BreakPrice_Range]
        CHECK ([BreakPrice] IS NULL OR ([BreakPrice] > 0 AND [BreakPrice] <= 10000000));
END

IF OBJECT_ID(N'[dbo].[Order]', N'U') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Order] ALTER COLUMN [ShipmentPrice] DECIMAL(18, 2) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Order_ClientName_Required_Length')
        ALTER TABLE [dbo].[Order] WITH NOCHECK
        ADD CONSTRAINT [CK_Order_ClientName_Required_Length]
        CHECK ([ClientName] IS NOT NULL AND LEN(LTRIM(RTRIM([ClientName]))) > 0 AND LEN([ClientName]) <= 150);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Order_Phone_Length')
        ALTER TABLE [dbo].[Order] WITH NOCHECK
        ADD CONSTRAINT [CK_Order_Phone_Length]
        CHECK ([Phone] IS NULL OR LEN([Phone]) <= 20);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Order_EventAddress_Required_Length')
        ALTER TABLE [dbo].[Order] WITH NOCHECK
        ADD CONSTRAINT [CK_Order_EventAddress_Required_Length]
        CHECK ([EventAddress] IS NOT NULL AND LEN(LTRIM(RTRIM([EventAddress]))) > 0 AND LEN([EventAddress]) <= 255);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Order_ShipmentPrice_Range')
        ALTER TABLE [dbo].[Order] WITH NOCHECK
        ADD CONSTRAINT [CK_Order_ShipmentPrice_Range]
        CHECK ([ShipmentPrice] IS NULL OR ([ShipmentPrice] >= 0 AND [ShipmentPrice] <= 1000000));

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Order_Observation_Length')
        ALTER TABLE [dbo].[Order] WITH NOCHECK
        ADD CONSTRAINT [CK_Order_Observation_Length]
        CHECK ([Observation] IS NULL OR LEN([Observation]) <= 1000);
END

IF OBJECT_ID(N'[dbo].[Expense]', N'U') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Expense] ALTER COLUMN [Price] DECIMAL(18, 2) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Expense_Description_Required_Length')
        ALTER TABLE [dbo].[Expense] WITH NOCHECK
        ADD CONSTRAINT [CK_Expense_Description_Required_Length]
        CHECK ([Description] IS NOT NULL AND LEN(LTRIM(RTRIM([Description]))) > 0 AND LEN([Description]) <= 500);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Expense_Price_Range')
        ALTER TABLE [dbo].[Expense] WITH NOCHECK
        ADD CONSTRAINT [CK_Expense_Price_Range]
        CHECK ([Price] > 0 AND [Price] <= 1000000);
END

IF OBJECT_ID(N'[dbo].[ProductOrder]', N'U') IS NOT NULL
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

    ALTER TABLE [dbo].[ProductOrder] ALTER COLUMN [UnitPrice] DECIMAL(18, 2) NOT NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.key_constraints
        WHERE [type] = N'PK'
            AND parent_object_id = OBJECT_ID(N'[dbo].[ProductOrder]')
    )
        ALTER TABLE [dbo].[ProductOrder]
        ADD CONSTRAINT [PK_ProductOrder] PRIMARY KEY ([ProductId], [OrderId], [UnitPrice]);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ProductOrder_Quantity_Range')
        ALTER TABLE [dbo].[ProductOrder] WITH NOCHECK
        ADD CONSTRAINT [CK_ProductOrder_Quantity_Range]
        CHECK ([Quantity] > 0 AND [Quantity] <= 100000);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ProductOrder_UnitPrice_Range')
        ALTER TABLE [dbo].[ProductOrder] WITH NOCHECK
        ADD CONSTRAINT [CK_ProductOrder_UnitPrice_Range]
        CHECK ([UnitPrice] > 0 AND [UnitPrice] <= 10000000);
END

IF OBJECT_ID(N'[dbo].[User]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_User_RoleId_Range')
        ALTER TABLE [dbo].[User] WITH NOCHECK
        ADD CONSTRAINT [CK_User_RoleId_Range]
        CHECK ([RoleId] > 0);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_User_Email_Required_Length')
        ALTER TABLE [dbo].[User] WITH NOCHECK
        ADD CONSTRAINT [CK_User_Email_Required_Length]
        CHECK ([Email] IS NOT NULL AND LEN(LTRIM(RTRIM([Email]))) > 0 AND LEN([Email]) <= 60);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_User_PasswordHash_Required_Length')
        ALTER TABLE [dbo].[User] WITH NOCHECK
        ADD CONSTRAINT [CK_User_PasswordHash_Required_Length]
        CHECK ([PasswordHash] IS NOT NULL AND LEN(LTRIM(RTRIM([PasswordHash]))) > 0 AND LEN([PasswordHash]) <= 128);
END

IF OBJECT_ID(N'[dbo].[Role]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Role_Name_Required_Length')
        ALTER TABLE [dbo].[Role] WITH NOCHECK
        ADD CONSTRAINT [CK_Role_Name_Required_Length]
        CHECK ([Name] IS NOT NULL AND LEN(LTRIM(RTRIM([Name]))) > 0 AND LEN([Name]) <= 100);
END

IF OBJECT_ID(N'[audit].[AuditCategory]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AuditCategory_Code_Name_Length')
        ALTER TABLE [audit].[AuditCategory] WITH NOCHECK
        ADD CONSTRAINT [CK_AuditCategory_Code_Name_Length]
        CHECK (
            [CategoryCode] IS NOT NULL AND LEN(LTRIM(RTRIM([CategoryCode]))) > 0 AND LEN([CategoryCode]) <= 50
            AND [CategoryName] IS NOT NULL AND LEN(LTRIM(RTRIM([CategoryName]))) > 0 AND LEN([CategoryName]) <= 100
        );
END

IF OBJECT_ID(N'[audit].[AuditAction]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AuditAction_Code_Name_Category_Length')
        ALTER TABLE [audit].[AuditAction] WITH NOCHECK
        ADD CONSTRAINT [CK_AuditAction_Code_Name_Category_Length]
        CHECK (
            [ActionCode] IS NOT NULL AND LEN(LTRIM(RTRIM([ActionCode]))) > 0 AND LEN([ActionCode]) <= 100
            AND [ActionName] IS NOT NULL AND LEN(LTRIM(RTRIM([ActionName]))) > 0 AND LEN([ActionName]) <= 150
            AND [CategoryCode] IS NOT NULL AND LEN(LTRIM(RTRIM([CategoryCode]))) > 0 AND LEN([CategoryCode]) <= 50
        );
END

IF OBJECT_ID(N'[audit].[AuditEntry]', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AuditEntry_Core_Lengths')
        ALTER TABLE [audit].[AuditEntry] WITH NOCHECK
        ADD CONSTRAINT [CK_AuditEntry_Core_Lengths]
        CHECK (
            [ActionCode] IS NOT NULL AND LEN(LTRIM(RTRIM([ActionCode]))) > 0 AND LEN([ActionCode]) <= 100
            AND [Entity] IS NOT NULL AND LEN(LTRIM(RTRIM([Entity]))) > 0 AND LEN([Entity]) <= 100
            AND ([EntityKey] IS NULL OR LEN([EntityKey]) <= 100)
            AND ([Name] IS NULL OR LEN([Name]) <= 200)
            AND ([Description] IS NULL OR LEN([Description]) <= 1000)
            AND ([UserId] IS NULL OR [UserId] > 0)
            AND ([UserName] IS NULL OR LEN([UserName]) <= 200)
            AND ([UserEmail] IS NULL OR LEN([UserEmail]) <= 256)
            AND ([IpAddress] IS NULL OR LEN([IpAddress]) <= 64)
        );
END
