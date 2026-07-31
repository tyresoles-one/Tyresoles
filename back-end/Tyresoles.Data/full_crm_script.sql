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
CREATE TABLE [dbo].[CrmActivityOutcome] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmActivityOutcome] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmActivityType] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmActivityType] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmCallLog] (
    [Id] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NOT NULL,
    [CallDate] datetime2 NOT NULL,
    [Outcome] nvarchar(100) NOT NULL,
    [Notes] nvarchar(max) NULL,
    [CreatedBy] nvarchar(128) NOT NULL,
    CONSTRAINT [PK_CrmCallLog] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmCallReminder] (
    [Id] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NOT NULL,
    [ReminderDate] datetime2 NOT NULL,
    [Notes] nvarchar(max) NULL,
    [IsCompleted] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(128) NOT NULL,
    CONSTRAINT [PK_CrmCallReminder] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmContact] (
    [Id] uniqueidentifier NOT NULL,
    [ContactType] nvarchar(max) NULL,
    [ContactCategory] nvarchar(max) NULL,
    [FullName] nvarchar(max) NOT NULL,
    [CompanyName] nvarchar(max) NULL,
    [MobileNo] nvarchar(max) NULL,
    [MobileNo2] nvarchar(max) NULL,
    [EmailIds] nvarchar(max) NULL,
    [IsDecisionMaker] bit NOT NULL,
    [Address] nvarchar(max) NULL,
    [City] nvarchar(max) NULL,
    [State] nvarchar(max) NULL,
    [RespCenter] nvarchar(max) NULL,
    [ERPCustomerNos] nvarchar(max) NULL,
    [ERPAreaCodes] nvarchar(max) NULL,
    [Products] nvarchar(max) NULL,
    [Tags] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [LastCallDate] datetime2 NULL,
    [LastCallOutcome] nvarchar(100) NULL,
    CONSTRAINT [PK_CrmContact] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmContactFleetDetail] (
    [Id] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NOT NULL,
    [VehicleType] nvarchar(max) NOT NULL,
    [Make] nvarchar(max) NULL,
    [Model] nvarchar(max) NULL,
    [Quantity] int NOT NULL,
    [TyreSize] nvarchar(max) NULL,
    [Application] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmContactFleetDetail] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmContactType] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmContactType] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmContactCategory] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmContactCategory] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmPriority] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmPriority] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmSetting] (
    [Key] nvarchar(100) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    CONSTRAINT [PK_CrmSetting] PRIMARY KEY ([Key])
);

CREATE TABLE [dbo].[CrmSource] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmSource] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmStage] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmStage] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmWhatsappImage] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [ImageUrl] nvarchar(max) NULL,
    [Base64Data] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_CrmWhatsappImage] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmWhatsappTemplate] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Language] nvarchar(100) NOT NULL,
    [MessageText] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_CrmWhatsappTemplate] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmAgentContact] (
    [Id] uniqueidentifier NOT NULL,
    [AgentUsername] nvarchar(128) NOT NULL,
    [ContactId] uniqueidentifier NOT NULL,
    [AllocatedAt] datetime2 NOT NULL,
    [DeallocatedAt] datetime2 NULL,
    [DeallocatedBy] nvarchar(128) NULL,
    [LastCallOutcome] nvarchar(100) NULL,
    [LastCallDate] datetime2 NULL,
    [LastCallNotes] nvarchar(max) NULL,
    [CallCount] int NOT NULL DEFAULT 0,
    CONSTRAINT [PK_CrmAgentContact] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CrmAgentContact_CrmContact_ContactId] FOREIGN KEY ([ContactId]) REFERENCES [dbo].[CrmContact] ([Id]) ON DELETE CASCADE
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'Description', N'Value') AND [object_id] = OBJECT_ID(N'[dbo].[CrmSetting]'))
    SET IDENTITY_INSERT [dbo].[CrmSetting] ON;
INSERT INTO [dbo].[CrmSetting] ([Key], [Description], [Value])
VALUES (N'ContactsRecentSalesDaysCooldown', N'Days from latest invoice to cool down contact from allocation', N'30');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Key', N'Description', N'Value') AND [object_id] = OBJECT_ID(N'[dbo].[CrmSetting]'))
    SET IDENTITY_INSERT [dbo].[CrmSetting] OFF;

CREATE INDEX [IX_CrmAgentContact_AgentUsername_DeallocatedAt_AllocatedAt] ON [dbo].[CrmAgentContact] ([AgentUsername], [DeallocatedAt], [AllocatedAt]);

CREATE INDEX [IX_CrmAgentContact_ContactId_DeallocatedAt] ON [dbo].[CrmAgentContact] ([ContactId], [DeallocatedAt]);

CREATE INDEX [IX_CrmContact_IsActive_RespCenter_LastCallOutcome_LastCallDate] ON [dbo].[CrmContact] ([IsActive], [RespCenter], [LastCallOutcome], [LastCallDate]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260713203320_AddCrmContactFleetDetail', N'10.0.3');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [dbo].[CrmActivityOutcome] ADD [ActivityTypeId] int NULL;

ALTER TABLE [dbo].[CrmActivityOutcome] ADD [IsPositive] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE TABLE [dbo].[CrmEntityType] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmEntityType] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmFleetApplication] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmFleetApplication] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmFleetVehicleMake] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [ParentId] int NULL,
    CONSTRAINT [PK_CrmFleetVehicleMake] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmFleetVehicleModel] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [ParentId] int NULL,
    CONSTRAINT [PK_CrmFleetVehicleModel] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[CrmFleetVehicleType] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmFleetVehicleType] PRIMARY KEY ([Id])
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[dbo].[CrmFleetApplication]'))
    SET IDENTITY_INSERT [dbo].[CrmFleetApplication] ON;
INSERT INTO [dbo].[CrmFleetApplication] ([Id], [Name])
VALUES (1, N'Long Haul'),
(2, N'Mining'),
(3, N'Construction'),
(4, N'Passenger Transport'),
(5, N'City Distribution');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[dbo].[CrmFleetApplication]'))
    SET IDENTITY_INSERT [dbo].[CrmFleetApplication] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[dbo].[CrmFleetVehicleMake]'))
    SET IDENTITY_INSERT [dbo].[CrmFleetVehicleMake] ON;
INSERT INTO [dbo].[CrmFleetVehicleMake] ([Id], [Name], [ParentId])
VALUES (1, N'Tata Motors', 1),
(2, N'Ashok Leyland', 1),
(3, N'Eicher', 1),
(4, N'BharatBenz', 1),
(5, N'Mahindra', 1),
(6, N'Tata Motors (Bus)', 2),
(7, N'Ashok Leyland (Bus)', 2);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[dbo].[CrmFleetVehicleMake]'))
    SET IDENTITY_INSERT [dbo].[CrmFleetVehicleMake] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[dbo].[CrmFleetVehicleModel]'))
    SET IDENTITY_INSERT [dbo].[CrmFleetVehicleModel] ON;
INSERT INTO [dbo].[CrmFleetVehicleModel] ([Id], [Name], [ParentId])
VALUES (1, N'Signa', 1),
(2, N'Prima', 1),
(3, N'LPT', 1),
(4, N'Dost', 2),
(5, N'Partner', 2),
(6, N'Boss', 2),
(7, N'Ecomet', 2),
(8, N'Pro 2000', 3),
(9, N'Pro 3000', 3);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'ParentId') AND [object_id] = OBJECT_ID(N'[dbo].[CrmFleetVehicleModel]'))
    SET IDENTITY_INSERT [dbo].[CrmFleetVehicleModel] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[dbo].[CrmFleetVehicleType]'))
    SET IDENTITY_INSERT [dbo].[CrmFleetVehicleType] ON;
INSERT INTO [dbo].[CrmFleetVehicleType] ([Id], [Name])
VALUES (1, N'Truck'),
(2, N'Bus'),
(3, N'LCV'),
(4, N'Tractor');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[dbo].[CrmFleetVehicleType]'))
    SET IDENTITY_INSERT [dbo].[CrmFleetVehicleType] OFF;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715104821_AddFleetMasterTables', N'10.0.3');

COMMIT;
GO

