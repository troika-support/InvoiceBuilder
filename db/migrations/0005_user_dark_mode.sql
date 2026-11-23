BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102100209_AddUserDarkMode'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [PrefersDarkMode] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102100209_AddUserDarkMode'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102100209_AddUserDarkMode', N'10.0.14');
END;

COMMIT;
GO

