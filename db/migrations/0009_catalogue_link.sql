BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102174628_AddCatalogueLink'
)
BEGIN
    ALTER TABLE [DocumentLines] ADD [CatalogueItemId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102174628_AddCatalogueLink'
)
BEGIN
    CREATE INDEX [IX_DocumentLines_CatalogueItemId] ON [DocumentLines] ([CatalogueItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102174628_AddCatalogueLink'
)
BEGIN
    ALTER TABLE [DocumentLines] ADD CONSTRAINT [FK_DocumentLines_CatalogueItems_CatalogueItemId] FOREIGN KEY ([CatalogueItemId]) REFERENCES [CatalogueItems] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102174628_AddCatalogueLink'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102174628_AddCatalogueLink', N'10.0.14');
END;

COMMIT;
GO

