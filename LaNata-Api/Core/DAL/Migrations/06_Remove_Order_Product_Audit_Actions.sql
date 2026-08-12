IF OBJECT_ID(N'[audit].[AuditAction]', N'U') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'[audit].[AuditEntry]', N'U') IS NOT NULL
       AND EXISTS (
            SELECT 1
            FROM [audit].[AuditEntry]
            WHERE [ActionCode] IN ('PRODUCT_ADDED', 'PRODUCT_REMOVED')
       )
    BEGIN
        UPDATE [audit].[AuditEntry]
        SET [ActionCode] = 'UPDATED'
        WHERE [ActionCode] IN ('PRODUCT_ADDED', 'PRODUCT_REMOVED');
    END

    DELETE FROM [audit].[AuditAction]
    WHERE [ActionCode] IN ('PRODUCT_ADDED', 'PRODUCT_REMOVED');
END
