IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'emailing')
BEGIN
    EXEC('CREATE SCHEMA [emailing]');
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[emailing].[EmailTemplate]')
    AND type = N'U'
)
BEGIN
    CREATE TABLE [emailing].[EmailTemplate] (
        [TemplateCode] VARCHAR(50) NOT NULL PRIMARY KEY,
        [Html] NVARCHAR(MAX) NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM [emailing].[EmailTemplate] WHERE [TemplateCode] = 'GENERIC_NOTIFICATION')
BEGIN
    INSERT INTO [emailing].[EmailTemplate] ([TemplateCode], [Html])
    VALUES ('GENERIC_NOTIFICATION', N'<p>Hola [NAME],</p><p>[MESSAGE]</p>');
END
