IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(200) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(200) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(200) NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(200) NULL,
        [SecurityStamp] nvarchar(200) NULL,
        [ConcurrencyStamp] nvarchar(200) NULL,
        [PhoneNumber] nvarchar(200) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(200) NOT NULL,
        [ClaimType] nvarchar(200) NULL,
        [ClaimValue] nvarchar(200) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(200) NOT NULL,
        [ClaimType] nvarchar(200) NULL,
        [ClaimValue] nvarchar(200) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(200) NOT NULL,
        [ProviderKey] nvarchar(200) NOT NULL,
        [ProviderDisplayName] nvarchar(200) NULL,
        [UserId] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(200) NOT NULL,
        [RoleId] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(200) NOT NULL,
        [LoginProvider] nvarchar(200) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Value] nvarchar(200) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [Businesses] (
        [Id] uniqueidentifier NOT NULL,
        [TradingName] nvarchar(200) NOT NULL,
        [RegisteredName] nvarchar(200) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [WhatsAppNumber] nvarchar(200) NULL,
        [Address] nvarchar(1000) NOT NULL,
        [LogoAssetId] uniqueidentifier NULL,
        [DefaultCurrency] nvarchar(3) NOT NULL,
        [DefaultTermsDays] int NOT NULL,
        [DefaultThemeId] uniqueidentifier NULL,
        [QuoteValidityDays] int NOT NULL,
        [AllowPartPayments] bit NOT NULL,
        [MinPartPaymentPercent] decimal(18,2) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Businesses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Businesses_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [Clients] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Email] nvarchar(200) NULL,
        [Phone] nvarchar(200) NULL,
        [WhatsApp] nvarchar(200) NULL,
        [Address] nvarchar(1000) NULL,
        [DefaultCurrency] nvarchar(3) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Clients] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Clients_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Clients_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [NumberSequences] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentType] int NOT NULL,
        [Prefix] nvarchar(20) NOT NULL,
        [NextValue] bigint NOT NULL,
        [Padding] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_NumberSequences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NumberSequences_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_NumberSequences_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [TaxRates] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Percent] decimal(18,4) NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [FiscalTaxId] nvarchar(200) NULL,
        [IsArchived] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TaxRates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaxRates_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaxRates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [Themes] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Tokens] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Themes] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Theme_Tokens_json] CHECK (ISJSON([Tokens]) = 1),
        CONSTRAINT [FK_Themes_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Themes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Method] int NOT NULL,
        [Status] int NOT NULL,
        [PaidAt] datetimeoffset NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [ProviderReference] nvarchar(200) NULL,
        [PollUrl] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [CatalogueItems] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [Unit] nvarchar(200) NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [TaxRateId] uniqueidentifier NULL,
        [HsCode] nvarchar(8) NULL,
        [IsRecurring] bit NOT NULL,
        [IsArchived] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CatalogueItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CatalogueItems_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CatalogueItems_TaxRates_TaxRateId] FOREIGN KEY ([TaxRateId]) REFERENCES [TaxRates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CatalogueItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [Templates] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [DocumentTypes] int NOT NULL,
        [ThemeId] uniqueidentifier NOT NULL,
        [CurrentVersionId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Templates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Templates_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Templates_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Templates_Themes_ThemeId] FOREIGN KEY ([ThemeId]) REFERENCES [Themes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [TemplateVersions] (
        [Id] uniqueidentifier NOT NULL,
        [TemplateId] uniqueidentifier NOT NULL,
        [Version] int NOT NULL,
        [Definition] nvarchar(max) NOT NULL,
        [PublishedAt] datetimeoffset NULL,
        [PublishedBy] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TemplateVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TemplateVersion_Definition_json] CHECK (ISJSON([Definition]) = 1),
        CONSTRAINT [FK_TemplateVersions_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TemplateVersions_Templates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [Templates] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TemplateVersions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [Documents] (
        [Id] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Number] nvarchar(32) NULL,
        [Status] int NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [ClientSnapshot] nvarchar(max) NOT NULL,
        [BusinessSnapshot] nvarchar(max) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [ExchangeRate] decimal(18,4) NULL,
        [IssueDate] date NULL,
        [DueDate] date NULL,
        [ValidUntil] date NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [DiscountTotal] decimal(18,2) NOT NULL,
        [TaxTotal] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [Notes] nvarchar(4000) NULL,
        [Terms] nvarchar(4000) NULL,
        [CreditReason] nvarchar(1000) NULL,
        [TemplateVersionId] uniqueidentifier NULL,
        [SourceDocumentId] uniqueidentifier NULL,
        [IssuedAt] datetimeoffset NULL,
        [PlanCount] int NULL,
        [PlanFrequency] int NULL,
        [PlanRescheduledAt] datetimeoffset NULL,
        [DiscountKind] int NOT NULL,
        [DiscountValue] decimal(18,4) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Documents] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Document_BusinessSnapshot_json] CHECK (ISJSON([BusinessSnapshot]) = 1),
        CONSTRAINT [CK_Document_ClientSnapshot_json] CHECK (ISJSON([ClientSnapshot]) = 1),
        CONSTRAINT [FK_Documents_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Documents_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Documents_Documents_SourceDocumentId] FOREIGN KEY ([SourceDocumentId]) REFERENCES [Documents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Documents_TemplateVersions_TemplateVersionId] FOREIGN KEY ([TemplateVersionId]) REFERENCES [TemplateVersions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Documents_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [DocumentLines] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [Position] int NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Detail] nvarchar(2000) NULL,
        [Quantity] decimal(18,3) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxRateId] uniqueidentifier NULL,
        [TaxPercentSnapshot] decimal(18,4) NOT NULL,
        [FiscalTaxIdSnapshot] nvarchar(200) NULL,
        [HsCodeSnapshot] nvarchar(8) NULL,
        [LineDiscount] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_DocumentLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentLines_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DocumentLines_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DocumentLines_TaxRates_TaxRateId] FOREIGN KEY ([TaxRateId]) REFERENCES [TaxRates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DocumentLines_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [Instalments] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [Position] int NOT NULL,
        [DueKind] int NOT NULL,
        [DueDate] date NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Instalments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Instalments_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Instalments_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Instalments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [PaymentAllocations] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PaymentAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentAllocations_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentAllocations_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentAllocations_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PaymentAllocations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [QuoteAcceptances] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [Decision] int NOT NULL,
        [SignerName] nvarchar(200) NOT NULL,
        [AcceptedAt] datetimeoffset NOT NULL,
        [Ip] nvarchar(64) NULL,
        [UserAgent] nvarchar(500) NULL,
        [Comment] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_QuoteAcceptances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QuoteAcceptances_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_QuoteAcceptances_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [Documents] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_QuoteAcceptances_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE TABLE [RecurringSchedules] (
        [Id] uniqueidentifier NOT NULL,
        [SourceDocumentId] uniqueidentifier NOT NULL,
        [Cadence] int NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [MaxOccurrences] int NULL,
        [AutoIssue] bit NOT NULL,
        [NextRunDate] date NOT NULL,
        [Paused] bit NOT NULL,
        [Occurrences] int NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_RecurringSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RecurringSchedules_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RecurringSchedules_Documents_SourceDocumentId] FOREIGN KEY ([SourceDocumentId]) REFERENCES [Documents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RecurringSchedules_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Businesses_TenantId_TradingName] ON [Businesses] ([TenantId], [TradingName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CatalogueItems_BusinessId_Name] ON [CatalogueItems] ([BusinessId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_CatalogueItems_TaxRateId] ON [CatalogueItems] ([TaxRateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_CatalogueItems_TenantId] ON [CatalogueItems] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Clients_BusinessId_Name] ON [Clients] ([BusinessId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Clients_TenantId] ON [Clients] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_DocumentLines_BusinessId] ON [DocumentLines] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentLines_DocumentId_Position] ON [DocumentLines] ([DocumentId], [Position]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_DocumentLines_TaxRateId] ON [DocumentLines] ([TaxRateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_DocumentLines_TenantId] ON [DocumentLines] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Documents_BusinessId_ClientId] ON [Documents] ([BusinessId], [ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Documents_BusinessId_Type_Number] ON [Documents] ([BusinessId], [Type], [Number]) WHERE [Number] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Documents_ClientId] ON [Documents] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Documents_SourceDocumentId] ON [Documents] ([SourceDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Documents_TemplateVersionId] ON [Documents] ([TemplateVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Documents_TenantId] ON [Documents] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Instalments_BusinessId] ON [Instalments] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Instalments_DocumentId_Position] ON [Instalments] ([DocumentId], [Position]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Instalments_TenantId] ON [Instalments] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NumberSequences_BusinessId_DocumentType] ON [NumberSequences] ([BusinessId], [DocumentType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_NumberSequences_TenantId] ON [NumberSequences] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_BusinessId] ON [PaymentAllocations] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_DocumentId] ON [PaymentAllocations] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentAllocations_PaymentId_DocumentId] ON [PaymentAllocations] ([PaymentId], [DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_PaymentAllocations_TenantId] ON [PaymentAllocations] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_BusinessId_ProviderReference] ON [Payments] ([BusinessId], [ProviderReference]) WHERE [ProviderReference] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Payments_ClientId] ON [Payments] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Payments_TenantId] ON [Payments] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_QuoteAcceptances_BusinessId] ON [QuoteAcceptances] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuoteAcceptances_DocumentId] ON [QuoteAcceptances] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_QuoteAcceptances_TenantId] ON [QuoteAcceptances] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_RecurringSchedules_BusinessId] ON [RecurringSchedules] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_RecurringSchedules_Paused_NextRunDate] ON [RecurringSchedules] ([Paused], [NextRunDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_RecurringSchedules_SourceDocumentId] ON [RecurringSchedules] ([SourceDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_RecurringSchedules_TenantId] ON [RecurringSchedules] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaxRates_BusinessId_Name_EffectiveFrom] ON [TaxRates] ([BusinessId], [Name], [EffectiveFrom]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_TaxRates_TenantId] ON [TaxRates] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Templates_BusinessId_Name] ON [Templates] ([BusinessId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Templates_TenantId] ON [Templates] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Templates_ThemeId] ON [Templates] ([ThemeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_TemplateVersions_BusinessId] ON [TemplateVersions] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TemplateVersions_TemplateId_Version] ON [TemplateVersions] ([TemplateId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_TemplateVersions_TenantId] ON [TemplateVersions] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Themes_BusinessId_Name] ON [Themes] ([BusinessId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    CREATE INDEX [IX_Themes_TenantId] ON [Themes] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102051811_Initial'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102051811_Initial', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102054248_ActivityLog'
)
BEGIN
    CREATE TABLE [ActivityEntries] (
        [Id] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NULL,
        [EntityType] nvarchar(64) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [Action] nvarchar(64) NOT NULL,
        [ActorUserId] nvarchar(450) NULL,
        [ActorLabel] nvarchar(200) NOT NULL,
        [OccurredAt] datetimeoffset NOT NULL,
        [Summary] nvarchar(2000) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ActivityEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ActivityEntries_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102054248_ActivityLog'
)
BEGIN
    CREATE INDEX [IX_ActivityEntries_EntityType_EntityId_OccurredAt] ON [ActivityEntries] ([EntityType], [EntityId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102054248_ActivityLog'
)
BEGIN
    CREATE INDEX [IX_ActivityEntries_TenantId] ON [ActivityEntries] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102054248_ActivityLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102054248_ActivityLog', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102070708_PaynowGateway'
)
BEGIN
    ALTER TABLE [Payments] ADD [ReviewReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102070708_PaynowGateway'
)
BEGIN
    CREATE TABLE [PaymentGatewayConfigs] (
        [Id] uniqueidentifier NOT NULL,
        [Provider] int NOT NULL,
        [IntegrationId] nvarchar(100) NOT NULL,
        [IntegrationKeyEncrypted] nvarchar(2000) NOT NULL,
        [IsLive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PaymentGatewayConfigs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentGatewayConfigs_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentGatewayConfigs_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102070708_PaynowGateway'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_BusinessId_Reference] ON [Payments] ([BusinessId], [Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102070708_PaynowGateway'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentGatewayConfigs_BusinessId_Provider] ON [PaymentGatewayConfigs] ([BusinessId], [Provider]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102070708_PaynowGateway'
)
BEGIN
    CREATE INDEX [IX_PaymentGatewayConfigs_TenantId] ON [PaymentGatewayConfigs] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102070708_PaynowGateway'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102070708_PaynowGateway', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102081308_AddUserBusiness'
)
BEGIN
    CREATE TABLE [UserBusinesses] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [BusinessId] uniqueidentifier NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserBusinesses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserBusinesses_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserBusinesses_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102081308_AddUserBusiness'
)
BEGIN
    CREATE INDEX [IX_UserBusinesses_BusinessId] ON [UserBusinesses] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102081308_AddUserBusiness'
)
BEGIN
    CREATE INDEX [IX_UserBusinesses_TenantId] ON [UserBusinesses] ([TenantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102081308_AddUserBusiness'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserBusinesses_UserId_BusinessId] ON [UserBusinesses] ([UserId], [BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102081308_AddUserBusiness'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102081308_AddUserBusiness', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102082503_AddSelectedBusiness'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [SelectedBusinessId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102082503_AddSelectedBusiness'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102082503_AddSelectedBusiness', N'10.0.14');
END;

COMMIT;
GO

