BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102191048_DocumentLinkToken'
)
BEGIN
    ALTER TABLE [Documents] ADD [AllowedPaymentMethods] int NOT NULL DEFAULT 7;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102191048_DocumentLinkToken'
)
BEGIN
    ALTER TABLE [Documents] ADD [LinkTokenHash] varchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102191048_DocumentLinkToken'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Documents_LinkTokenHash] ON [Documents] ([LinkTokenHash]) WHERE [LinkTokenHash] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102191048_DocumentLinkToken'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102191048_DocumentLinkToken', N'10.0.14');
END;

COMMIT;
GO

