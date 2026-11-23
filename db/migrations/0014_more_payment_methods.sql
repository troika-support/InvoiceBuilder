BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111142109_AddMorePaymentMethods'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Documents]') AND [c].[name] = N'AllowedPaymentMethods');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Documents] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Documents] ADD DEFAULT 127 FOR [AllowedPaymentMethods];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111142109_AddMorePaymentMethods'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111142109_AddMorePaymentMethods', N'10.0.14');
END;

COMMIT;
GO

