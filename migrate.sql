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
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] uniqueidentifier NULL,
        [Details] nvarchar(2000) NULL,
        [CreatedOn] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [Mobile] nvarchar(20) NULL,
        [Email] nvarchar(256) NULL,
        [ProfilePhotoUrl] nvarchar(500) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedOn] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedOn] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [LastLoginOn] datetimeoffset NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [CompanyName] nvarchar(200) NULL,
        [Mobile] nvarchar(20) NULL,
        [Email] nvarchar(256) NULL,
        [PhotoUrl] nvarchar(500) NULL,
        [CreatedOn] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedOn] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Customers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE TABLE [Notes] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NULL,
        [ShortSummary] nvarchar(500) NULL,
        [FullSummary] nvarchar(max) NULL,
        [Transcript] nvarchar(max) NULL,
        [AudioFileUrl] nvarchar(500) NULL,
        [ProcessingStatus] nvarchar(32) NOT NULL DEFAULT N'Draft',
        [ProcessingError] nvarchar(2000) NULL,
        [DurationSeconds] int NULL,
        [DetectedLanguages] nvarchar(200) NULL,
        [ImportantEntitiesJson] nvarchar(max) NULL,
        [CreatedOn] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedOn] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [CompletedOn] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Notes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(128) NOT NULL,
        [ExpiresOn] datetimeoffset NOT NULL,
        [RevokedOn] datetimeoffset NULL,
        [CreatedOn] datetimeoffset NOT NULL,
        [DeviceInfo] nvarchar(256) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE TABLE [CustomerInteractions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [PhotoUrl] nvarchar(500) NULL,
        [AudioFileUrl] nvarchar(500) NULL,
        [Transcript] nvarchar(max) NULL,
        [ShortSummary] nvarchar(500) NULL,
        [FullSummary] nvarchar(max) NULL,
        [InteractionDate] datetimeoffset NOT NULL,
        [DurationSeconds] int NULL,
        [ProcessingStatus] nvarchar(32) NOT NULL DEFAULT N'Draft',
        [ProcessingError] nvarchar(2000) NULL,
        [DetectedLanguages] nvarchar(200) NULL,
        [ImportantEntitiesJson] nvarchar(max) NULL,
        [CreatedOn] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedOn] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [CompletedOn] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_CustomerInteractions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerInteractions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerInteractions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE TABLE [NoteActionItems] (
        [Id] uniqueidentifier NOT NULL,
        [NoteId] uniqueidentifier NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [DueDate] datetimeoffset NULL,
        [IsCompleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Kind] nvarchar(32) NOT NULL DEFAULT N'Action',
        [CreatedOn] datetimeoffset NOT NULL,
        CONSTRAINT [PK_NoteActionItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NoteActionItems_Notes_NoteId] FOREIGN KEY ([NoteId]) REFERENCES [Notes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE TABLE [CustomerInteractionActionItems] (
        [Id] uniqueidentifier NOT NULL,
        [InteractionId] uniqueidentifier NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [DueDate] datetimeoffset NULL,
        [IsCompleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Kind] nvarchar(32) NOT NULL DEFAULT N'Action',
        [CreatedOn] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CustomerInteractionActionItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerInteractionActionItems_CustomerInteractions_InteractionId] FOREIGN KEY ([InteractionId]) REFERENCES [CustomerInteractions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedOn] ON [AuditLogs] ([CreatedOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [AuditLogs] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerInteractionActionItems_InteractionId] ON [CustomerInteractionActionItems] ([InteractionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerInteractions_CustomerId_InteractionDate] ON [CustomerInteractions] ([CustomerId], [InteractionDate] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerInteractions_UserId_InteractionDate] ON [CustomerInteractions] ([UserId], [InteractionDate] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_UserId_CompanyName] ON [Customers] ([UserId], [CompanyName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_UserId_Mobile] ON [Customers] ([UserId], [Mobile]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_UserId_Name] ON [Customers] ([UserId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_NoteActionItems_NoteId] ON [NoteActionItems] ([NoteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notes_UserId_CreatedOn] ON [Notes] ([UserId], [CreatedOn] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notes_UserId_ProcessingStatus] ON [Notes] ([UserId], [ProcessingStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]) WHERE [Email] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_Mobile] ON [Users] ([Mobile]) WHERE [Mobile] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819162552_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260819162552_InitialCreate', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825164856_AddLeads'
)
BEGIN
    CREATE TABLE [Leads] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NULL,
        [ShortSummary] nvarchar(500) NULL,
        [FullSummary] nvarchar(max) NULL,
        [Transcript] nvarchar(max) NULL,
        [AudioFileUrl] nvarchar(500) NULL,
        [PhotoUrl] nvarchar(500) NULL,
        [ProcessingStatus] nvarchar(32) NOT NULL DEFAULT N'Draft',
        [ProcessingError] nvarchar(2000) NULL,
        [DurationSeconds] int NULL,
        [DetectedLanguages] nvarchar(200) NULL,
        [ImportantEntitiesJson] nvarchar(max) NULL,
        [CreatedOn] datetimeoffset NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedOn] datetimeoffset NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [CompletedOn] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Leads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Leads_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825164856_AddLeads'
)
BEGIN
    CREATE TABLE [LeadActionItems] (
        [Id] uniqueidentifier NOT NULL,
        [LeadId] uniqueidentifier NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [DueDate] datetimeoffset NULL,
        [IsCompleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Kind] nvarchar(32) NOT NULL DEFAULT N'Action',
        [CreatedOn] datetimeoffset NOT NULL,
        CONSTRAINT [PK_LeadActionItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LeadActionItems_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825164856_AddLeads'
)
BEGIN
    CREATE INDEX [IX_LeadActionItems_LeadId] ON [LeadActionItems] ([LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825164856_AddLeads'
)
BEGIN
    CREATE INDEX [IX_Leads_UserId_CreatedOn] ON [Leads] ([UserId], [CreatedOn] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825164856_AddLeads'
)
BEGIN
    CREATE INDEX [IX_Leads_UserId_ProcessingStatus] ON [Leads] ([UserId], [ProcessingStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825164856_AddLeads'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825164856_AddLeads', N'10.0.11');
END;

COMMIT;
GO

