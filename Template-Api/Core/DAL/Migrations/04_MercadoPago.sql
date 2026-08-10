IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MercadoPagoPayment]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[MercadoPagoPayment] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UserId] INT NULL,
        [PreferenceId] NVARCHAR(150) NULL,
        [PaymentId] NVARCHAR(100) NULL,
        [ExternalReference] NVARCHAR(150) NULL,
        [PayerEmail] NVARCHAR(256) NULL,
        [TransactionAmount] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [CurrencyId] NVARCHAR(10) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL,
        [StatusDetail] NVARCHAR(150) NULL,
        [InitPoint] NVARCHAR(MAX) NULL,
        [SandboxInitPoint] NVARCHAR(MAX) NULL,
        [ProviderResponse] NVARCHAR(MAX) NULL,
        [Error] NVARCHAR(MAX) NULL,
        [PaidAt] DATETIME2 NULL,
        [LastNotificationAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NULL,
        [DeletedAt] DATETIME2 NULL
    );

    CREATE UNIQUE INDEX [IX_MercadoPagoPayment_ExternalReference]
        ON [dbo].[MercadoPagoPayment] ([ExternalReference])
        WHERE [ExternalReference] IS NOT NULL AND [DeletedAt] IS NULL;

    CREATE UNIQUE INDEX [IX_MercadoPagoPayment_PreferenceId]
        ON [dbo].[MercadoPagoPayment] ([PreferenceId])
        WHERE [PreferenceId] IS NOT NULL AND [DeletedAt] IS NULL;

    CREATE UNIQUE INDEX [IX_MercadoPagoPayment_PaymentId]
        ON [dbo].[MercadoPagoPayment] ([PaymentId])
        WHERE [PaymentId] IS NOT NULL AND [DeletedAt] IS NULL;

    CREATE INDEX [IX_MercadoPagoPayment_UserId]
        ON [dbo].[MercadoPagoPayment] ([UserId]);

    CREATE INDEX [IX_MercadoPagoPayment_Status]
        ON [dbo].[MercadoPagoPayment] ([Status]);
END
