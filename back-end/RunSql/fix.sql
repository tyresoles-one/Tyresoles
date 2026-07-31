
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
GO

CREATE TABLE [dbo].[CrmFleetApplication] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmFleetApplication] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [dbo].[CrmFleetVehicleMake] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [ParentId] int NULL,
    CONSTRAINT [PK_CrmFleetVehicleMake] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [dbo].[CrmFleetVehicleModel] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [ParentId] int NULL,
    CONSTRAINT [PK_CrmFleetVehicleModel] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [dbo].[CrmFleetVehicleType] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_CrmFleetVehicleType] PRIMARY KEY ([Id])
);
GO

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
GO

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
GO

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
GO

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[dbo].[CrmFleetVehicleType]'))
    SET IDENTITY_INSERT [dbo].[CrmFleetVehicleType] ON;
INSERT INTO [dbo].[CrmFleetVehicleType] ([Id], [Name])
VALUES (1, N'Truck'),
(2, N'Bus'),
(3, N'LCV'),
(4, N'Tractor');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[dbo].[CrmFleetVehicleType]'))
    SET IDENTITY_INSERT [dbo].[CrmFleetVehicleType] OFF;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260713203320_AddCrmContactFleetDetail', N'10.0.3');
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715104821_AddFleetMasterTables', N'10.0.3');
GO

