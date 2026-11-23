BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102103355_AddBusinessLogo'
)
BEGIN
    ALTER TABLE [Businesses] ADD [Logo] varbinary(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102103355_AddBusinessLogo'
)
BEGIN
    ALTER TABLE [Businesses] ADD [LogoContentType] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102103355_AddBusinessLogo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102103355_AddBusinessLogo', N'10.0.14');
END;

COMMIT;
GO

