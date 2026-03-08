-- ============================================================================
-- SCHEMA MIGRATION SCRIPT - New Features
-- Adds: FRAME_SIZE table, LENS_INDEX brand/color, ORDER_ITEM/PREORDER_ITEM selectedSize
--
-- This script is IDEMPOTENT - safe to run multiple times.
-- Run this BEFORE starting the application after updating the code.
-- ============================================================================

PRINT '=== Starting New Features Migration ===';
PRINT '';

-- ============================================================================
-- STEP 1: Create FRAME_SIZE table (many-to-many: Frame <-> Size)
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FRAME_SIZE')
BEGIN
    CREATE TABLE [FRAME_SIZE] (
        [frameSizeId] UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [frameId]     UNIQUEIDENTIFIER NULL,
        [size]        VARCHAR(50)      NULL,
        [isDefault]   BIT              NULL     DEFAULT 0,
        CONSTRAINT [PK_FRAME_SIZE] PRIMARY KEY ([frameSizeId])
    );
    PRINT 'Created table: FRAME_SIZE';
END
ELSE
    PRINT 'Table FRAME_SIZE already exists - skipped';
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FRAME_SIZE_FRAME')
BEGIN
    ALTER TABLE [FRAME_SIZE] ADD CONSTRAINT [FK_FRAME_SIZE_FRAME]
        FOREIGN KEY ([frameId]) REFERENCES [FRAME]([frameId]);
    PRINT 'Added FK: FK_FRAME_SIZE_FRAME';
END
GO

-- ============================================================================
-- STEP 2: Add brandId and colorId to LENS_INDEX table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_INDEX') AND name = 'brandId')
BEGIN
    ALTER TABLE [LENS_INDEX] ADD [brandId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: LENS_INDEX.brandId';
END
ELSE
    PRINT 'Column LENS_INDEX.brandId already exists - skipped';
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'LENS_INDEX') AND name = 'colorId')
BEGIN
    ALTER TABLE [LENS_INDEX] ADD [colorId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: LENS_INDEX.colorId';
END
ELSE
    PRINT 'Column LENS_INDEX.colorId already exists - skipped';
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_LENS_INDEX_BRAND')
BEGIN
    ALTER TABLE [LENS_INDEX] ADD CONSTRAINT [FK_LENS_INDEX_BRAND]
        FOREIGN KEY ([brandId]) REFERENCES [BRAND]([brandId]);
    PRINT 'Added FK: FK_LENS_INDEX_BRAND';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_LENS_INDEX_COLOR')
BEGIN
    ALTER TABLE [LENS_INDEX] ADD CONSTRAINT [FK_LENS_INDEX_COLOR]
        FOREIGN KEY ([colorId]) REFERENCES [COLOR]([colorId]);
    PRINT 'Added FK: FK_LENS_INDEX_COLOR';
END
GO

-- ============================================================================
-- STEP 3: Add selectedSize to ORDER_ITEM table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'ORDER_ITEM') AND name = 'selectedSize')
BEGIN
    ALTER TABLE [ORDER_ITEM] ADD [selectedSize] VARCHAR(50) NULL;
    PRINT 'Added column: ORDER_ITEM.selectedSize';
END
ELSE
    PRINT 'Column ORDER_ITEM.selectedSize already exists - skipped';
GO

-- ============================================================================
-- STEP 4: Add selectedSize to PREORDER_ITEM table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'PREORDER_ITEM') AND name = 'selectedSize')
BEGIN
    ALTER TABLE [PREORDER_ITEM] ADD [selectedSize] VARCHAR(50) NULL;
    PRINT 'Added column: PREORDER_ITEM.selectedSize';
END
ELSE
    PRINT 'Column PREORDER_ITEM.selectedSize already exists - skipped';
GO

-- ============================================================================
-- DONE
-- ============================================================================

PRINT '';
PRINT '=== New Features Migration Complete ===';
PRINT '';
PRINT 'Summary of changes:';
PRINT '  - FRAME_SIZE table (created or verified)';
PRINT '  - LENS_INDEX: brandId, colorId columns + FK constraints';
PRINT '  - ORDER_ITEM: selectedSize column';
PRINT '  - PREORDER_ITEM: selectedSize column';
GO
