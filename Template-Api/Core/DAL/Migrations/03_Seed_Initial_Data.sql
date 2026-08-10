-- Seed default roles
MERGE [dbo].[Role] AS target
USING (VALUES
    ('ADMIN'),
    ('USER')
) AS source ([Name])
ON target.[Name] = source.[Name]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Name]) VALUES (source.[Name]);

-- Seed default admin user
-- Default credentials:
--   Email:    admin@localhost
--   Password: Password1!
-- WARNING: change the password (and ideally the email) before deploying to any non-local environment.
-- To rotate the password, generate a new BCrypt hash and replace the value below.
IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [Email] = 'admin@localhost')
BEGIN
    DECLARE @AdminRoleId INT = (SELECT [Id] FROM [dbo].[Role] WHERE [Name] = 'ADMIN');

    INSERT INTO [dbo].[User] (
        [RoleId],
        [Name],
        [LastName],
        [Email],
        [PasswordHash]
    )
    VALUES (
        @AdminRoleId,
        'Admin',
        'Admin',
        'admin@localhost',
        '$2b$11$s4dxamTXdxRFeWnD4DsOM.46BBUQbM6geS6SHNeWPbuozdTKpXpu.'
    );
END
