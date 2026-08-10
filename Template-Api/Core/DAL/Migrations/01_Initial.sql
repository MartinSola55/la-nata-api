IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Role]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Role] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Name] NVARCHAR(50) NOT NULL
    );

    CREATE UNIQUE INDEX [IX_Role_Name] ON [dbo].[Role] ([Name]);
END

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[User]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[User] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [RoleId] INT NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [LastName] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(256) NOT NULL,
        [PasswordHash] NVARCHAR(MAX) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NULL,
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_User_Role_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Role] ([Id])
    );

    CREATE UNIQUE INDEX [IX_User_Email] ON [dbo].[User] ([Email]) WHERE [DeletedAt] IS NULL;
    CREATE INDEX [IX_User_RoleId] ON [dbo].[User] ([RoleId]);
END

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GenericItem]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[GenericItem] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Name] NVARCHAR(150) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NULL,
        [DeletedAt] DATETIME2 NULL
    );

    CREATE UNIQUE INDEX [IX_GenericItem_Name] ON [dbo].[GenericItem] ([Name]) WHERE [DeletedAt] IS NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WhatsAppMessage]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[WhatsAppMessage] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [PhoneNumber] NVARCHAR(30) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [Provider] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(30) NOT NULL,
        [ProviderResponse] NVARCHAR(MAX) NULL,
        [Error] NVARCHAR(MAX) NULL,
        [SentAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NULL,
        [DeletedAt] DATETIME2 NULL
    );

    CREATE INDEX [IX_WhatsAppMessage_PhoneNumber] ON [dbo].[WhatsAppMessage] ([PhoneNumber]);
    CREATE INDEX [IX_WhatsAppMessage_Status] ON [dbo].[WhatsAppMessage] ([Status]);
END
