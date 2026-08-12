IF OBJECT_ID(N'[dbo].[Rol]', N'U') IS NOT NULL
    AND OBJECT_ID(N'[dbo].[Role]', N'U') IS NULL
BEGIN
    EXEC sp_rename N'[dbo].[Rol]', N'Role';
END

DECLARE @renames TABLE (
    SchemaName SYSNAME NOT NULL,
    TableName SYSNAME NOT NULL,
    OldName SYSNAME NOT NULL,
    NewName SYSNAME NOT NULL
);

INSERT INTO @renames (SchemaName, TableName, OldName, NewName)
VALUES
    (N'dbo', N'Role', N'id', N'Id'),
    (N'dbo', N'Role', N'description', N'Name'),

    (N'dbo', N'User', N'id', N'Id'),
    (N'dbo', N'User', N'id_rol', N'RoleId'),
    (N'dbo', N'User', N'email', N'Email'),
    (N'dbo', N'User', N'password', N'PasswordHash'),
    (N'dbo', N'User', N'created_at', N'CreatedAt'),
    (N'dbo', N'User', N'updated_at', N'UpdatedAt'),
    (N'dbo', N'User', N'deleted_at', N'DeletedAt'),
    (N'dbo', N'User', N'created_by', N'CreatedBy'),
    (N'dbo', N'User', N'updated_by', N'UpdatedBy'),
    (N'dbo', N'User', N'deleted_by', N'DeletedBy'),

    (N'dbo', N'Product', N'id', N'Id'),
    (N'dbo', N'Product', N'description', N'Description'),
    (N'dbo', N'Product', N'stock', N'Stock'),
    (N'dbo', N'Product', N'price', N'Price'),
    (N'dbo', N'Product', N'break_price', N'BreakPrice'),
    (N'dbo', N'Product', N'created_at', N'CreatedAt'),
    (N'dbo', N'Product', N'updated_at', N'UpdatedAt'),
    (N'dbo', N'Product', N'deleted_at', N'DeletedAt'),
    (N'dbo', N'Product', N'created_by', N'CreatedBy'),
    (N'dbo', N'Product', N'updated_by', N'UpdatedBy'),
    (N'dbo', N'Product', N'deleted_by', N'DeletedBy'),

    (N'dbo', N'Order', N'id', N'Id'),
    (N'dbo', N'Order', N'client_name', N'ClientName'),
    (N'dbo', N'Order', N'phone', N'Phone'),
    (N'dbo', N'Order', N'event_adress', N'EventAddress'),
    (N'dbo', N'Order', N'date', N'Date'),
    (N'dbo', N'Order', N'shipment_price', N'ShipmentPrice'),
    (N'dbo', N'Order', N'observation', N'Observation'),
    (N'dbo', N'Order', N'created_at', N'CreatedAt'),
    (N'dbo', N'Order', N'updated_at', N'UpdatedAt'),
    (N'dbo', N'Order', N'deleted_at', N'DeletedAt'),
    (N'dbo', N'Order', N'created_by', N'CreatedBy'),
    (N'dbo', N'Order', N'updated_by', N'UpdatedBy'),
    (N'dbo', N'Order', N'deleted_by', N'DeletedBy'),

    (N'dbo', N'Expense', N'id', N'Id'),
    (N'dbo', N'Expense', N'date', N'Date'),
    (N'dbo', N'Expense', N'description', N'Description'),
    (N'dbo', N'Expense', N'price', N'Price'),
    (N'dbo', N'Expense', N'created_at', N'CreatedAt'),
    (N'dbo', N'Expense', N'updated_at', N'UpdatedAt'),
    (N'dbo', N'Expense', N'deleted_at', N'DeletedAt'),
    (N'dbo', N'Expense', N'created_by', N'CreatedBy'),
    (N'dbo', N'Expense', N'updated_by', N'UpdatedBy'),
    (N'dbo', N'Expense', N'deleted_by', N'DeletedBy'),

    (N'dbo', N'ProductOrder', N'id_product', N'ProductId'),
    (N'dbo', N'ProductOrder', N'id_order', N'OrderId'),
    (N'dbo', N'ProductOrder', N'quantity', N'Quantity'),
    (N'dbo', N'ProductOrder', N'unit_price', N'UnitPrice');

DECLARE @schema SYSNAME;
DECLARE @table SYSNAME;
DECLARE @old SYSNAME;
DECLARE @new SYSNAME;
DECLARE @objectId INT;
DECLARE @columnObject NVARCHAR(776);

DECLARE rename_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT SchemaName, TableName, OldName, NewName
    FROM @renames;

OPEN rename_cursor;
FETCH NEXT FROM rename_cursor INTO @schema, @table, @old, @new;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @objectId = OBJECT_ID(QUOTENAME(@schema) + N'.' + QUOTENAME(@table));

    IF @objectId IS NOT NULL
        AND EXISTS (
            SELECT 1
            FROM sys.columns
            WHERE object_id = @objectId
                AND name COLLATE Latin1_General_BIN2 = @old COLLATE Latin1_General_BIN2
        )
        AND NOT EXISTS (
            SELECT 1
            FROM sys.columns
            WHERE object_id = @objectId
                AND name COLLATE Latin1_General_BIN2 = @new COLLATE Latin1_General_BIN2
        )
    BEGIN
        SET @columnObject = QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N'.' + QUOTENAME(@old);
        EXEC sp_rename @columnObject, @new, N'COLUMN';
    END

    FETCH NEXT FROM rename_cursor INTO @schema, @table, @old, @new;
END

CLOSE rename_cursor;
DEALLOCATE rename_cursor;
