IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'audit')
BEGIN
    EXEC('CREATE SCHEMA [audit]');
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[audit].[AuditCategory]')
    AND type = N'U'
)
BEGIN
    CREATE TABLE [audit].[AuditCategory] (
        [CategoryCode] VARCHAR(50) NOT NULL PRIMARY KEY,
        [CategoryName] NVARCHAR(100) NOT NULL,
        [IsInternal] BIT NOT NULL DEFAULT 0
    );
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[audit].[AuditAction]')
    AND type = N'U'
)
BEGIN
    CREATE TABLE [audit].[AuditAction] (
        [ActionCode] VARCHAR(100) NOT NULL PRIMARY KEY,
        [ActionName] NVARCHAR(150) NOT NULL,
        [CategoryCode] VARCHAR(50) NOT NULL,
        [IsInternal] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_AuditAction_AuditCategory_CategoryCode]
            FOREIGN KEY ([CategoryCode]) REFERENCES [audit].[AuditCategory] ([CategoryCode])
    );

    CREATE INDEX [IX_AuditAction_CategoryCode] ON [audit].[AuditAction] ([CategoryCode]);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[audit].[AuditEntry]')
    AND type = N'U'
)
BEGIN
    CREATE TABLE [audit].[AuditEntry] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Date] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [ActionCode] VARCHAR(100) NOT NULL,
        [Entity] VARCHAR(100) NOT NULL,
        [EntityKey] NVARCHAR(100) NULL,
        [Name] NVARCHAR(200) NULL,
        [Description] NVARCHAR(1000) NULL,
        [UserId] INT NULL,
        [UserName] NVARCHAR(200) NULL,
        [UserEmail] NVARCHAR(256) NULL,
        [IpAddress] NVARCHAR(64) NULL,
        [State] NVARCHAR(MAX) NULL,
        [Payload] NVARCHAR(MAX) NULL,
        [Summary] NVARCHAR(MAX) NULL,
        -- UserId is intentionally not a foreign key to [dbo].[User]: an audit row must survive the
        -- user being removed, and writing it must never take locks on operational tables.
        CONSTRAINT [FK_AuditEntry_AuditAction_ActionCode]
            FOREIGN KEY ([ActionCode]) REFERENCES [audit].[AuditAction] ([ActionCode])
    );

    CREATE INDEX [IX_AuditEntry_Date] ON [audit].[AuditEntry] ([Date] DESC);
    CREATE INDEX [IX_AuditEntry_Entity_EntityKey] ON [audit].[AuditEntry] ([Entity], [EntityKey]);
    CREATE INDEX [IX_AuditEntry_ActionCode] ON [audit].[AuditEntry] ([ActionCode]);
    CREATE INDEX [IX_AuditEntry_UserId] ON [audit].[AuditEntry] ([UserId]);
END

MERGE [audit].[AuditCategory] AS target
USING (VALUES
    ('SECURITY', N'Seguridad', 0),
    ('DATA', N'Datos', 0),
    ('PAYMENT', N'Pagos', 0),
    ('NOTIFICATION', N'Notificaciones', 1)
) AS source ([CategoryCode], [CategoryName], [IsInternal])
ON target.[CategoryCode] = source.[CategoryCode]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([CategoryCode], [CategoryName], [IsInternal])
    VALUES (source.[CategoryCode], source.[CategoryName], source.[IsInternal]);

MERGE [audit].[AuditAction] AS target
USING (VALUES
    ('CREATED', N'Creación', 'DATA', 0),
    ('UPDATED', N'Edición', 'DATA', 0),
    ('DELETED', N'Eliminación', 'DATA', 0),
    ('ACTIVATED', N'Activación', 'DATA', 0),
    ('DEACTIVATED', N'Desactivación', 'DATA', 0),
    ('LOGIN', N'Inicio de sesión', 'SECURITY', 0),
    ('LOGIN_FAILED', N'Inicio de sesión fallido', 'SECURITY', 0),
    ('PASSWORD_CHANGED', N'Cambio de contraseña', 'SECURITY', 0),
    ('PAYMENT_CREATED', N'Pago iniciado', 'PAYMENT', 0),
    ('PAYMENT_UPDATED', N'Pago actualizado', 'PAYMENT', 0),
    ('EMAIL_SENT', N'Email enviado', 'NOTIFICATION', 1),
    ('WHATSAPP_SENT', N'WhatsApp enviado', 'NOTIFICATION', 1)
) AS source ([ActionCode], [ActionName], [CategoryCode], [IsInternal])
ON target.[ActionCode] = source.[ActionCode]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([ActionCode], [ActionName], [CategoryCode], [IsInternal])
    VALUES (source.[ActionCode], source.[ActionName], source.[CategoryCode], source.[IsInternal]);
