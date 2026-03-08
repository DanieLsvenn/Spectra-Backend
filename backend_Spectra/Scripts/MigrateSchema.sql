-- ============================================================================
-- SCHEMA MIGRATION SCRIPT
-- Migrates the database to match the current C# model structure.
--
-- This script is IDEMPOTENT — safe to run multiple times.
-- It uses IF NOT EXISTS / IF EXISTS checks before every change.
--
-- Run against your SQL Server database (e.g. via SSMS or sqlcmd).
-- ============================================================================

PRINT '=== Starting Schema Migration ===';
PRINT '';

-- ============================================================================
-- STEP 1: Create new lookup tables (BRAND, MATERIAL, COLOR)
-- ============================================================================

-- 1a. BRAND table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BRAND')
BEGIN
    CREATE TABLE [BRAND] (
        [brandId]   UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [brandName] VARCHAR(100)     NOT NULL,
        [status]    VARCHAR(50)      NULL     DEFAULT 'active',
        CONSTRAINT [PK_BRAND] PRIMARY KEY ([brandId])
    );
    PRINT 'Created table: BRAND';
END
ELSE
    PRINT 'Table BRAND already exists — skipped';
GO

-- 1b. MATERIAL table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MATERIAL')
BEGIN
    CREATE TABLE [MATERIAL] (
        [materialId]   UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [materialName] VARCHAR(100)     NOT NULL,
        [status]       VARCHAR(50)      NULL     DEFAULT 'active',
        CONSTRAINT [PK_MATERIAL] PRIMARY KEY ([materialId])
    );
    PRINT 'Created table: MATERIAL';
END
ELSE
    PRINT 'Table MATERIAL already exists — skipped';
GO

-- 1c. COLOR table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'COLOR')
BEGIN
    CREATE TABLE [COLOR] (
        [colorId]   UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [colorName] VARCHAR(50)      NOT NULL,
        [hexCode]   VARCHAR(7)       NULL,
        [status]    VARCHAR(50)      NULL     DEFAULT 'active',
        CONSTRAINT [PK_COLOR] PRIMARY KEY ([colorId])
    );
    PRINT 'Created table: COLOR';
END
ELSE
    PRINT 'Table COLOR already exists — skipped';
GO

-- ============================================================================
-- STEP 2: Create junction / association tables
-- ============================================================================

-- 2a. FRAME_COLOR (many-to-many: Frame ? Color)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FRAME_COLOR')
BEGIN
    CREATE TABLE [FRAME_COLOR] (
        [frameColorId] UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [frameId]      UNIQUEIDENTIFIER NULL,
        [colorId]      UNIQUEIDENTIFIER NULL,
        [isDefault]    BIT              NULL     DEFAULT 0,
        CONSTRAINT [PK_FRAME_COLOR] PRIMARY KEY ([frameColorId])
    );
    PRINT 'Created table: FRAME_COLOR';
END
ELSE
    PRINT 'Table FRAME_COLOR already exists — skipped';
GO

-- 2b. LENS_INDEX table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LENS_INDEX')
BEGIN
    CREATE TABLE [LENS_INDEX] (
        [lensIndexId]     UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [indexValue]      FLOAT            NOT NULL,
        [name]            VARCHAR(100)     NOT NULL,
        [description]     VARCHAR(500)     NULL,
        [additionalPrice] FLOAT            NOT NULL DEFAULT 0,
        [minPrescription] FLOAT            NULL,
        [maxPrescription] FLOAT            NULL,
        [status]          VARCHAR(50)      NULL     DEFAULT 'active',
        CONSTRAINT [PK_LENS_INDEX] PRIMARY KEY ([lensIndexId])
    );
    PRINT 'Created table: LENS_INDEX';
END
ELSE
    PRINT 'Table LENS_INDEX already exists — skipped';
GO

-- ============================================================================
-- STEP 3: Migrate FRAME table
--   Old columns: brand (varchar), color (varchar), material (varchar)
--   New columns: brandId (FK?BRAND), materialId (FK?MATERIAL)
--   Color is now via FRAME_COLOR junction table.
--   Also add stockQuantity, reorderLevel if missing.
-- ============================================================================

-- 3a. Migrate existing brand string values ? BRAND rows, then add brandId FK
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'brand')
BEGIN
    -- Insert distinct brand values into BRAND table (skip NULLs and empties)
    INSERT INTO [BRAND] ([brandId], [brandName])
    SELECT NEWID(), brand
    FROM (SELECT DISTINCT [brand] FROM [FRAME] WHERE [brand] IS NOT NULL AND [brand] <> '') AS src
    WHERE NOT EXISTS (SELECT 1 FROM [BRAND] b WHERE b.[brandName] = src.[brand]);

    PRINT 'Migrated distinct brand values from FRAME.brand ? BRAND table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'brandId')
BEGIN
    ALTER TABLE [FRAME] ADD [brandId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: FRAME.brandId';
END
GO

-- Backfill brandId from old brand column (if old column exists)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'brand')
   AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'brandId')
BEGIN
    UPDATE f
    SET f.[brandId] = b.[brandId]
    FROM [FRAME] f
    INNER JOIN [BRAND] b ON b.[brandName] = f.[brand]
    WHERE f.[brandId] IS NULL AND f.[brand] IS NOT NULL AND f.[brand] <> '';

    PRINT 'Backfilled FRAME.brandId from FRAME.brand';
END
GO

-- 3b. Migrate existing material string values ? MATERIAL rows, then add materialId FK
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'material')
BEGIN
    INSERT INTO [MATERIAL] ([materialId], [materialName])
    SELECT NEWID(), material
    FROM (SELECT DISTINCT [material] FROM [FRAME] WHERE [material] IS NOT NULL AND [material] <> '') AS src
    WHERE NOT EXISTS (SELECT 1 FROM [MATERIAL] m WHERE m.[materialName] = src.[material]);

    PRINT 'Migrated distinct material values from FRAME.material ? MATERIAL table';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'materialId')
BEGIN
    ALTER TABLE [FRAME] ADD [materialId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: FRAME.materialId';
END
GO

-- Backfill materialId from old material column
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'material')
   AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'materialId')
BEGIN
    UPDATE f
    SET f.[materialId] = m.[materialId]
    FROM [FRAME] f
    INNER JOIN [MATERIAL] m ON m.[materialName] = f.[material]
    WHERE f.[materialId] IS NULL AND f.[material] IS NOT NULL AND f.[material] <> '';

    PRINT 'Backfilled FRAME.materialId from FRAME.material';
END
GO

-- 3c. Migrate existing color string values ? COLOR + FRAME_COLOR rows
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'color')
BEGIN
    -- Insert distinct color values into COLOR table
    INSERT INTO [COLOR] ([colorId], [colorName])
    SELECT NEWID(), color
    FROM (SELECT DISTINCT [color] FROM [FRAME] WHERE [color] IS NOT NULL AND [color] <> '') AS src
    WHERE NOT EXISTS (SELECT 1 FROM [COLOR] c WHERE c.[colorName] = src.[color]);

    PRINT 'Migrated distinct color values from FRAME.color ? COLOR table';

    -- Create FRAME_COLOR entries for existing frame?color relationships
    INSERT INTO [FRAME_COLOR] ([frameColorId], [frameId], [colorId], [isDefault])
    SELECT NEWID(), f.[frameId], c.[colorId], 1
    FROM [FRAME] f
    INNER JOIN [COLOR] c ON c.[colorName] = f.[color]
    WHERE f.[color] IS NOT NULL AND f.[color] <> ''
      AND NOT EXISTS (
          SELECT 1 FROM [FRAME_COLOR] fc
          WHERE fc.[frameId] = f.[frameId] AND fc.[colorId] = c.[colorId]
      );

    PRINT 'Created FRAME_COLOR entries for existing frame colors';
END
GO

-- 3d. Add stockQuantity & reorderLevel (from the inventory migration)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'stockQuantity')
BEGIN
    ALTER TABLE [FRAME] ADD [stockQuantity] INT NULL;
    PRINT 'Added column: FRAME.stockQuantity';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'reorderLevel')
BEGIN
    ALTER TABLE [FRAME] ADD [reorderLevel] INT NULL;
    PRINT 'Added column: FRAME.reorderLevel';
END
GO

-- Set default values for existing frames
UPDATE [FRAME]
SET [stockQuantity] = 10, [reorderLevel] = 5
WHERE [stockQuantity] IS NULL OR [reorderLevel] IS NULL;
GO

-- 3e. Add FRAME FK constraints (if missing)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FRAME_BRAND')
BEGIN
    ALTER TABLE [FRAME] ADD CONSTRAINT [FK_FRAME_BRAND]
        FOREIGN KEY ([brandId]) REFERENCES [BRAND]([brandId]);
    PRINT 'Added FK: FK_FRAME_BRAND';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FRAME_MATERIAL')
BEGIN
    ALTER TABLE [FRAME] ADD CONSTRAINT [FK_FRAME_MATERIAL]
        FOREIGN KEY ([materialId]) REFERENCES [MATERIAL]([materialId]);
    PRINT 'Added FK: FK_FRAME_MATERIAL';
END
GO

-- 3f. Add FRAME_COLOR FK constraints (if missing)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FRAME_COLOR_FRAME')
BEGIN
    ALTER TABLE [FRAME_COLOR] ADD CONSTRAINT [FK_FRAME_COLOR_FRAME]
        FOREIGN KEY ([frameId]) REFERENCES [FRAME]([frameId]);
    PRINT 'Added FK: FK_FRAME_COLOR_FRAME';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FRAME_COLOR_COLOR')
BEGIN
    ALTER TABLE [FRAME_COLOR] ADD CONSTRAINT [FK_FRAME_COLOR_COLOR]
        FOREIGN KEY ([colorId]) REFERENCES [COLOR]([colorId]);
    PRINT 'Added FK: FK_FRAME_COLOR_COLOR';
END
GO

-- 3g. Drop old string columns from FRAME (after data is migrated)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'brand')
BEGIN
    ALTER TABLE [FRAME] DROP COLUMN [brand];
    PRINT 'Dropped column: FRAME.brand';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'color')
BEGIN
    ALTER TABLE [FRAME] DROP COLUMN [color];
    PRINT 'Dropped column: FRAME.color';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'material')
BEGIN
    ALTER TABLE [FRAME] DROP COLUMN [material];
    PRINT 'Dropped column: FRAME.material';
END
GO

-- ============================================================================
-- STEP 4: Migrate LENS_TYPE table
--   Old column:  extraPrice
--   New column:  basePrice
--   New columns: description, category, brandId, materialId, colorId, status
-- ============================================================================

-- 4a. Rename extraPrice ? basePrice (if old column exists)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_TYPE') AND name = 'extraPrice')
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_TYPE') AND name = 'basePrice')
BEGIN
    EXEC sp_rename 'LENS_TYPE.extraPrice', 'basePrice', 'COLUMN';
    PRINT 'Renamed column: LENS_TYPE.extraPrice ? basePrice';
END
GO

-- If neither exists, create basePrice
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_TYPE') AND name = 'basePrice')
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_TYPE') AND name = 'extraPrice')
BEGIN
    ALTER TABLE [LENS_TYPE] ADD [basePrice] FLOAT NULL;
    PRINT 'Added column: LENS_TYPE.basePrice';
END
GO

-- 4b. Add new columns
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_TYPE') AND name = 'description')
BEGIN
    ALTER TABLE [LENS_TYPE] ADD [description] VARCHAR(500) NULL;
    PRINT 'Added column: LENS_TYPE.description';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_TYPE') AND name = 'category')
BEGIN
    ALTER TABLE [LENS_TYPE] ADD [category] VARCHAR(50) NULL;
    PRINT 'Added column: LENS_TYPE.category';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_TYPE') AND name = 'brandId')
BEGIN
    ALTER TABLE [LENS_TYPE] ADD [brandId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: LENS_TYPE.brandId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_TYPE') AND name = 'materialId')
BEGIN
    ALTER TABLE [LENS_TYPE] ADD [materialId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: LENS_TYPE.materialId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_TYPE') AND name = 'colorId')
BEGIN
    ALTER TABLE [LENS_TYPE] ADD [colorId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: LENS_TYPE.colorId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_TYPE') AND name = 'status')
BEGIN
    ALTER TABLE [LENS_TYPE] ADD [status] VARCHAR(50) NULL DEFAULT 'active';
    PRINT 'Added column: LENS_TYPE.status';
END
GO

-- 4c. Add LENS_TYPE FK constraints
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_LENS_TYPE_BRAND')
BEGIN
    ALTER TABLE [LENS_TYPE] ADD CONSTRAINT [FK_LENS_TYPE_BRAND]
        FOREIGN KEY ([brandId]) REFERENCES [BRAND]([brandId]);
    PRINT 'Added FK: FK_LENS_TYPE_BRAND';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_LENS_TYPE_MATERIAL')
BEGIN
    ALTER TABLE [LENS_TYPE] ADD CONSTRAINT [FK_LENS_TYPE_MATERIAL]
        FOREIGN KEY ([materialId]) REFERENCES [MATERIAL]([materialId]);
    PRINT 'Added FK: FK_LENS_TYPE_MATERIAL';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_LENS_TYPE_COLOR')
BEGIN
    ALTER TABLE [LENS_TYPE] ADD CONSTRAINT [FK_LENS_TYPE_COLOR]
        FOREIGN KEY ([colorId]) REFERENCES [COLOR]([colorId]);
    PRINT 'Added FK: FK_LENS_TYPE_COLOR';
END
GO

-- ============================================================================
-- STEP 5: Migrate LENS_FEATURE table
--   Old column: lensIndex (removed — now separate LENS_INDEX table)
-- ============================================================================

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_FEATURE') AND name = 'lensIndex')
BEGIN
    ALTER TABLE [LENS_FEATURE] DROP COLUMN [lensIndex];
    PRINT 'Dropped column: LENS_FEATURE.lensIndex';
END
GO

-- ============================================================================
-- STEP 5b: Add colorId to FRAME_MEDIA table
--   Allows associating images with specific color variants of a frame.
--   Images with NULL colorId are generic/shared images for all colors.
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME_MEDIA') AND name = 'colorId')
BEGIN
    ALTER TABLE [FRAME_MEDIA] ADD [colorId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: FRAME_MEDIA.colorId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FRAME_MEDIA_COLOR')
BEGIN
    ALTER TABLE [FRAME_MEDIA] ADD CONSTRAINT [FK_FRAME_MEDIA_COLOR]
        FOREIGN KEY ([colorId]) REFERENCES [COLOR]([colorId]);
    PRINT 'Added FK: FK_FRAME_MEDIA_COLOR';
END
GO

-- ============================================================================
-- STEP 6: Migrate ORDER_ITEM table
--   Old columns: orderPrice, selectedColor (varchar)
--   New columns: unitPrice, selectedColorId (FK?COLOR), lensIndexId (FK?LENS_INDEX)
-- ============================================================================

-- 6a. Rename orderPrice ? unitPrice
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDER_ITEM') AND name = 'orderPrice')
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDER_ITEM') AND name = 'unitPrice')
BEGIN
    EXEC sp_rename 'ORDER_ITEM.orderPrice', 'unitPrice', 'COLUMN';
    PRINT 'Renamed column: ORDER_ITEM.orderPrice ? unitPrice';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDER_ITEM') AND name = 'unitPrice')
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDER_ITEM') AND name = 'orderPrice')
BEGIN
    ALTER TABLE [ORDER_ITEM] ADD [unitPrice] FLOAT NULL;
    PRINT 'Added column: ORDER_ITEM.unitPrice';
END
GO

-- 6b. Add selectedColorId
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDER_ITEM') AND name = 'selectedColorId')
BEGIN
    ALTER TABLE [ORDER_ITEM] ADD [selectedColorId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: ORDER_ITEM.selectedColorId';
END
GO

-- 6c. Migrate selectedColor string ? selectedColorId FK
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDER_ITEM') AND name = 'selectedColor')
   AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDER_ITEM') AND name = 'selectedColorId')
BEGIN
    -- Ensure all referenced colors exist in COLOR table
    INSERT INTO [COLOR] ([colorId], [colorName])
    SELECT NEWID(), selectedColor
    FROM (SELECT DISTINCT [selectedColor] FROM [ORDER_ITEM] WHERE [selectedColor] IS NOT NULL AND [selectedColor] <> '') AS src
    WHERE NOT EXISTS (SELECT 1 FROM [COLOR] c WHERE c.[colorName] = src.[selectedColor]);

    -- Backfill selectedColorId
    UPDATE oi
    SET oi.[selectedColorId] = c.[colorId]
    FROM [ORDER_ITEM] oi
    INNER JOIN [COLOR] c ON c.[colorName] = oi.[selectedColor]
    WHERE oi.[selectedColorId] IS NULL AND oi.[selectedColor] IS NOT NULL AND oi.[selectedColor] <> '';

    PRINT 'Migrated ORDER_ITEM.selectedColor ? selectedColorId';
END
GO

-- 6d. Add lensIndexId
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDER_ITEM') AND name = 'lensIndexId')
BEGIN
    ALTER TABLE [ORDER_ITEM] ADD [lensIndexId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: ORDER_ITEM.lensIndexId';
END
GO

-- 6e. Add ORDER_ITEM FK constraints
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ORDER_ITEM_COLOR')
BEGIN
    ALTER TABLE [ORDER_ITEM] ADD CONSTRAINT [FK_ORDER_ITEM_COLOR]
        FOREIGN KEY ([selectedColorId]) REFERENCES [COLOR]([colorId]);
    PRINT 'Added FK: FK_ORDER_ITEM_COLOR';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ORDER_ITEM_LENS_INDEX')
BEGIN
    ALTER TABLE [ORDER_ITEM] ADD CONSTRAINT [FK_ORDER_ITEM_LENS_INDEX]
        FOREIGN KEY ([lensIndexId]) REFERENCES [LENS_INDEX]([lensIndexId]);
    PRINT 'Added FK: FK_ORDER_ITEM_LENS_INDEX';
END
GO

-- 6f. Drop old selectedColor string column
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDER_ITEM') AND name = 'selectedColor')
BEGIN
    ALTER TABLE [ORDER_ITEM] DROP COLUMN [selectedColor];
    PRINT 'Dropped column: ORDER_ITEM.selectedColor';
END
GO

-- ============================================================================
-- STEP 7: Migrate PREORDER_ITEM table
--   Old columns: preorderPrice, selectedColor (varchar)
--   New columns: unitPrice, selectedColorId (FK?COLOR), lensIndexId (FK?LENS_INDEX)
-- ============================================================================

-- 7a. Rename preorderPrice ? unitPrice
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER_ITEM') AND name = 'preorderPrice')
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER_ITEM') AND name = 'unitPrice')
BEGIN
    EXEC sp_rename 'PREORDER_ITEM.preorderPrice', 'unitPrice', 'COLUMN';
    PRINT 'Renamed column: PREORDER_ITEM.preorderPrice ? unitPrice';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER_ITEM') AND name = 'unitPrice')
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER_ITEM') AND name = 'preorderPrice')
BEGIN
    ALTER TABLE [PREORDER_ITEM] ADD [unitPrice] FLOAT NULL;
    PRINT 'Added column: PREORDER_ITEM.unitPrice';
END
GO

-- 7b. Add selectedColorId
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER_ITEM') AND name = 'selectedColorId')
BEGIN
    ALTER TABLE [PREORDER_ITEM] ADD [selectedColorId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: PREORDER_ITEM.selectedColorId';
END
GO

-- 7c. Migrate selectedColor string ? selectedColorId FK
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER_ITEM') AND name = 'selectedColor')
   AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER_ITEM') AND name = 'selectedColorId')
BEGIN
    -- Ensure all referenced colors exist in COLOR table
    INSERT INTO [COLOR] ([colorId], [colorName])
    SELECT NEWID(), selectedColor
    FROM (SELECT DISTINCT [selectedColor] FROM [PREORDER_ITEM] WHERE [selectedColor] IS NOT NULL AND [selectedColor] <> '') AS src
    WHERE NOT EXISTS (SELECT 1 FROM [COLOR] c WHERE c.[colorName] = src.[selectedColor]);

    -- Backfill selectedColorId
    UPDATE pi
    SET pi.[selectedColorId] = c.[colorId]
    FROM [PREORDER_ITEM] pi
    INNER JOIN [COLOR] c ON c.[colorName] = pi.[selectedColor]
    WHERE pi.[selectedColorId] IS NULL AND pi.[selectedColor] IS NOT NULL AND pi.[selectedColor] <> '';

    PRINT 'Migrated PREORDER_ITEM.selectedColor ? selectedColorId';
END
GO

-- 7d. Add lensIndexId
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER_ITEM') AND name = 'lensIndexId')
BEGIN
    ALTER TABLE [PREORDER_ITEM] ADD [lensIndexId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: PREORDER_ITEM.lensIndexId';
END
GO

-- 7e. Add PREORDER_ITEM FK constraints
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PREORDER_ITEM_COLOR')
BEGIN
    ALTER TABLE [PREORDER_ITEM] ADD CONSTRAINT [FK_PREORDER_ITEM_COLOR]
        FOREIGN KEY ([selectedColorId]) REFERENCES [COLOR]([colorId]);
    PRINT 'Added FK: FK_PREORDER_ITEM_COLOR';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PREORDER_ITEM_LENS_INDEX')
BEGIN
    ALTER TABLE [PREORDER_ITEM] ADD CONSTRAINT [FK_PREORDER_ITEM_LENS_INDEX]
        FOREIGN KEY ([lensIndexId]) REFERENCES [LENS_INDEX]([lensIndexId]);
    PRINT 'Added FK: FK_PREORDER_ITEM_LENS_INDEX';
END
GO

-- 7f. Drop old selectedColor string column
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER_ITEM') AND name = 'selectedColor')
BEGIN
    ALTER TABLE [PREORDER_ITEM] DROP COLUMN [selectedColor];
    PRINT 'Dropped column: PREORDER_ITEM.selectedColor';
END
GO

-- ============================================================================
-- STEP 8: Ensure ORDERS table has shipping columns
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDERS') AND name = 'shippingMethod')
BEGIN
    ALTER TABLE [ORDERS] ADD [shippingMethod] VARCHAR(50) NULL;
    PRINT 'Added column: ORDERS.shippingMethod';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDERS') AND name = 'shippingFee')
BEGIN
    ALTER TABLE [ORDERS] ADD [shippingFee] FLOAT NULL;
    PRINT 'Added column: ORDERS.shippingFee';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDERS') AND name = 'trackingNumber')
BEGIN
    ALTER TABLE [ORDERS] ADD [trackingNumber] VARCHAR(100) NULL;
    PRINT 'Added column: ORDERS.trackingNumber';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDERS') AND name = 'shippedAt')
BEGIN
    ALTER TABLE [ORDERS] ADD [shippedAt] DATETIME NULL;
    PRINT 'Added column: ORDERS.shippedAt';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDERS') AND name = 'deliveredAt')
BEGIN
    ALTER TABLE [ORDERS] ADD [deliveredAt] DATETIME NULL;
    PRINT 'Added column: ORDERS.deliveredAt';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDERS') AND name = 'shippingCarrier')
BEGIN
    ALTER TABLE [ORDERS] ADD [shippingCarrier] VARCHAR(50) NULL;
    PRINT 'Added column: ORDERS.shippingCarrier';
END
GO

-- ============================================================================
-- STEP 9: Ensure PREORDER table has new columns
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER') AND name = 'campaignId')
BEGIN
    ALTER TABLE [PREORDER] ADD [campaignId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: PREORDER.campaignId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER') AND name = 'adminNotes')
BEGIN
    ALTER TABLE [PREORDER] ADD [adminNotes] TEXT NULL;
    PRINT 'Added column: PREORDER.adminNotes';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER') AND name = 'totalAmount')
BEGIN
    ALTER TABLE [PREORDER] ADD [totalAmount] FLOAT NULL;
    PRINT 'Added column: PREORDER.totalAmount';
END
GO

-- ============================================================================
-- STEP 10: Ensure COMPLAINT_REQUEST has extended columns
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'COMPLAINT_REQUEST') AND name = 'refundAmount')
BEGIN
    ALTER TABLE [COMPLAINT_REQUEST] ADD [refundAmount] FLOAT NULL;
    PRINT 'Added column: COMPLAINT_REQUEST.refundAmount';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'COMPLAINT_REQUEST') AND name = 'returnTrackingNumber')
BEGIN
    ALTER TABLE [COMPLAINT_REQUEST] ADD [returnTrackingNumber] VARCHAR(100) NULL;
    PRINT 'Added column: COMPLAINT_REQUEST.returnTrackingNumber';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'COMPLAINT_REQUEST') AND name = 'refundedAt')
BEGIN
    ALTER TABLE [COMPLAINT_REQUEST] ADD [refundedAt] DATETIME NULL;
    PRINT 'Added column: COMPLAINT_REQUEST.refundedAt';
END
GO

-- ============================================================================
-- STEP 11: Create PRODUCT_REVIEW table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PRODUCT_REVIEW')
BEGIN
    CREATE TABLE [PRODUCT_REVIEW] (
        [reviewId]    UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [userId]      UNIQUEIDENTIFIER NULL,
        [frameId]     UNIQUEIDENTIFIER NULL,
        [orderItemId] UNIQUEIDENTIFIER NULL,
        [rating]      INT              NOT NULL,
        [title]       VARCHAR(200)     NULL,
        [comment]     TEXT             NULL,
        [status]      VARCHAR(50)      NULL     DEFAULT 'visible',
        [createdAt]   DATETIME         NULL     DEFAULT (GETDATE()),
        [updatedAt]   DATETIME         NULL,
        CONSTRAINT [PK_PRODUCT_REVIEW] PRIMARY KEY ([reviewId])
    );
    PRINT 'Created table: PRODUCT_REVIEW';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PRODUCT_REVIEW_USER')
BEGIN
    ALTER TABLE [PRODUCT_REVIEW] ADD CONSTRAINT [FK_PRODUCT_REVIEW_USER]
        FOREIGN KEY ([userId]) REFERENCES [USER]([userId]);
    PRINT 'Added FK: FK_PRODUCT_REVIEW_USER';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PRODUCT_REVIEW_FRAME')
BEGIN
    ALTER TABLE [PRODUCT_REVIEW] ADD CONSTRAINT [FK_PRODUCT_REVIEW_FRAME]
        FOREIGN KEY ([frameId]) REFERENCES [FRAME]([frameId]);
    PRINT 'Added FK: FK_PRODUCT_REVIEW_FRAME';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PRODUCT_REVIEW_ORDER_ITEM')
BEGIN
    ALTER TABLE [PRODUCT_REVIEW] ADD CONSTRAINT [FK_PRODUCT_REVIEW_ORDER_ITEM]
        FOREIGN KEY ([orderItemId]) REFERENCES [ORDER_ITEM]([orderItemId]);
    PRINT 'Added FK: FK_PRODUCT_REVIEW_ORDER_ITEM';
END
GO

-- ============================================================================
-- STEP 12: Create PREORDER_CAMPAIGN table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PREORDER_CAMPAIGN')
BEGIN
    CREATE TABLE [PREORDER_CAMPAIGN] (
        [campaignId]             UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [campaignName]           VARCHAR(200)     NOT NULL,
        [description]            TEXT             NULL,
        [startDate]              DATETIME         NOT NULL,
        [endDate]                DATETIME         NOT NULL,
        [maxSlots]               INT              NULL,
        [currentSlots]           INT              NOT NULL DEFAULT 0,
        [status]                 VARCHAR(50)      NULL     DEFAULT 'upcoming',
        [estimatedDeliveryDate]  DATETIME         NULL,
        [createdAt]              DATETIME         NULL     DEFAULT (GETDATE()),
        CONSTRAINT [PK_PREORDER_CAMPAIGN] PRIMARY KEY ([campaignId])
    );
    PRINT 'Created table: PREORDER_CAMPAIGN';
END
GO

-- ============================================================================
-- STEP 13: Create CAMPAIGN_FRAME table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CAMPAIGN_FRAME')
BEGIN
    CREATE TABLE [CAMPAIGN_FRAME] (
        [campaignFrameId]    UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [campaignId]         UNIQUEIDENTIFIER NULL,
        [frameId]            UNIQUEIDENTIFIER NULL,
        [campaignPrice]      FLOAT            NULL,
        [maxQuantityPerOrder] INT             NOT NULL DEFAULT 2,
        CONSTRAINT [PK_CAMPAIGN_FRAME] PRIMARY KEY ([campaignFrameId])
    );
    PRINT 'Created table: CAMPAIGN_FRAME';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_CAMPAIGN_FRAME_CAMPAIGN')
BEGIN
    ALTER TABLE [CAMPAIGN_FRAME] ADD CONSTRAINT [FK_CAMPAIGN_FRAME_CAMPAIGN]
        FOREIGN KEY ([campaignId]) REFERENCES [PREORDER_CAMPAIGN]([campaignId]);
    PRINT 'Added FK: FK_CAMPAIGN_FRAME_CAMPAIGN';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_CAMPAIGN_FRAME_FRAME')
BEGIN
    ALTER TABLE [CAMPAIGN_FRAME] ADD CONSTRAINT [FK_CAMPAIGN_FRAME_FRAME]
        FOREIGN KEY ([frameId]) REFERENCES [FRAME]([frameId]);
    PRINT 'Added FK: FK_CAMPAIGN_FRAME_FRAME';
END
GO

-- Add FK from PREORDER to PREORDER_CAMPAIGN (after campaign table is created)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PREORDER_CAMPAIGN')
BEGIN
    ALTER TABLE [PREORDER] ADD CONSTRAINT [FK_PREORDER_CAMPAIGN]
        FOREIGN KEY ([campaignId]) REFERENCES [PREORDER_CAMPAIGN]([campaignId]);
    PRINT 'Added FK: FK_PREORDER_CAMPAIGN';
END
GO

-- ============================================================================
-- STEP 14: Create PREORDER_STATUS_LOG table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PREORDER_STATUS_LOG')
BEGIN
    CREATE TABLE [PREORDER_STATUS_LOG] (
        [logId]          UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [preorderId]     UNIQUEIDENTIFIER NULL,
        [previousStatus] VARCHAR(50)      NULL,
        [newStatus]      VARCHAR(50)      NULL,
        [message]        TEXT             NULL,
        [createdBy]      UNIQUEIDENTIFIER NULL,
        [createdAt]      DATETIME         NULL     DEFAULT (GETDATE()),
        CONSTRAINT [PK_PREORDER_STATUS_LOG] PRIMARY KEY ([logId])
    );
    PRINT 'Created table: PREORDER_STATUS_LOG';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PREORDER_STATUS_LOG_PREORDER')
BEGIN
    ALTER TABLE [PREORDER_STATUS_LOG] ADD CONSTRAINT [FK_PREORDER_STATUS_LOG_PREORDER]
        FOREIGN KEY ([preorderId]) REFERENCES [PREORDER]([preorderId]);
    PRINT 'Added FK: FK_PREORDER_STATUS_LOG_PREORDER';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PREORDER_STATUS_LOG_USER')
BEGIN
    ALTER TABLE [PREORDER_STATUS_LOG] ADD CONSTRAINT [FK_PREORDER_STATUS_LOG_USER]
        FOREIGN KEY ([createdBy]) REFERENCES [USER]([userId]);
    PRINT 'Added FK: FK_PREORDER_STATUS_LOG_USER';
END
GO

-- ============================================================================
-- DONE
-- ============================================================================

PRINT '';
PRINT '=== Schema Migration Complete ===';
PRINT '';
PRINT 'Summary of changes:';
PRINT '  - BRAND, MATERIAL, COLOR lookup tables (created or verified)';
PRINT '  - FRAME_COLOR junction table (created or verified)';
PRINT '  - LENS_INDEX table (created or verified)';
PRINT '  - FRAME: brand/color/material string columns ? brandId/materialId FKs + FRAME_COLOR';
PRINT '  - FRAME: stockQuantity, reorderLevel columns';
PRINT '  - LENS_TYPE: extraPrice ? basePrice, added description/category/brandId/materialId/colorId/status';
PRINT '  - LENS_FEATURE: dropped lensIndex column';
PRINT '  - ORDER_ITEM: orderPrice ? unitPrice, selectedColor ? selectedColorId FK, added lensIndexId FK';
PRINT '  - PREORDER_ITEM: preorderPrice ? unitPrice, selectedColor ? selectedColorId FK, added lensIndexId FK';
PRINT '  - ORDERS: shipping columns (shippingMethod/Fee/trackingNumber/shippedAt/deliveredAt/shippingCarrier)';
PRINT '  - PREORDER: campaignId/adminNotes/totalAmount columns';
PRINT '  - COMPLAINT_REQUEST: refundAmount/returnTrackingNumber/refundedAt columns';
PRINT '  - PRODUCT_REVIEW table (created or verified)';
PRINT '  - PREORDER_CAMPAIGN + CAMPAIGN_FRAME tables (created or verified)';
PRINT '  - PREORDER_STATUS_LOG table (created or verified)';
GO
