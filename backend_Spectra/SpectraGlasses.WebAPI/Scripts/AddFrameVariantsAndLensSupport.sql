-- ============================================================================
-- SCHEMA MIGRATION SCRIPT - Frame Variants (SKUs) & Lens Type Support
--
-- Adds:
--   1. stockQuantity column to FRAME_COLOR (per-color variant stock)
--   2. FRAME_LENS_TYPE junction table (which lens types a frame supports)
--   3. minRx, maxRx, minPd, maxPd columns to FRAME (prescription limits)
--
-- This script is IDEMPOTENT - safe to run multiple times.
-- Run this BEFORE starting the application after updating the code.
-- ============================================================================

PRINT '=== Starting Frame Variants & Lens Type Support Migration ===';
PRINT '';

-- ============================================================================
-- STEP 1: Add stockQuantity to FRAME_COLOR table (per-color variant stock)
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME_COLOR') AND name = 'stockQuantity')
BEGIN
    ALTER TABLE [FRAME_COLOR] ADD [stockQuantity] INT NULL;
    PRINT 'Added column: FRAME_COLOR.stockQuantity';
END
ELSE
    PRINT 'Column FRAME_COLOR.stockQuantity already exists - skipped';
GO

-- Migrate existing frame-level stock to color variants (distribute evenly)
-- Only runs if there are FRAME_COLOR rows without stockQuantity set
UPDATE fc
SET fc.stockQuantity = CASE
    WHEN colorCount.cnt > 0 THEN f.stockQuantity / colorCount.cnt
    ELSE 0
END
FROM FRAME_COLOR fc
INNER JOIN FRAME f ON fc.frameId = f.frameId
CROSS APPLY (
    SELECT COUNT(*) AS cnt
    FROM FRAME_COLOR fc2
    WHERE fc2.frameId = f.frameId
) colorCount
WHERE fc.stockQuantity IS NULL
  AND f.stockQuantity IS NOT NULL;

PRINT 'Migrated existing frame stock to color variants (evenly distributed)';
GO

-- ============================================================================
-- STEP 2: Create FRAME_LENS_TYPE junction table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FRAME_LENS_TYPE')
BEGIN
    CREATE TABLE [FRAME_LENS_TYPE] (
        [frameLensTypeId] UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [frameId]         UNIQUEIDENTIFIER NULL,
        [lensTypeId]      UNIQUEIDENTIFIER NULL,
        CONSTRAINT [PK_FRAME_LENS_TYPE] PRIMARY KEY ([frameLensTypeId])
    );
    PRINT 'Created table: FRAME_LENS_TYPE';
END
ELSE
    PRINT 'Table FRAME_LENS_TYPE already exists - skipped';
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FRAME_LENS_TYPE_FRAME')
BEGIN
    ALTER TABLE [FRAME_LENS_TYPE] ADD CONSTRAINT [FK_FRAME_LENS_TYPE_FRAME]
        FOREIGN KEY ([frameId]) REFERENCES [FRAME]([frameId]);
    PRINT 'Added FK: FK_FRAME_LENS_TYPE_FRAME';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FRAME_LENS_TYPE_LENS_TYPE')
BEGIN
    ALTER TABLE [FRAME_LENS_TYPE] ADD CONSTRAINT [FK_FRAME_LENS_TYPE_LENS_TYPE]
        FOREIGN KEY ([lensTypeId]) REFERENCES [LENS_TYPE]([lensTypeId]);
    PRINT 'Added FK: FK_FRAME_LENS_TYPE_LENS_TYPE';
END
GO

-- ============================================================================
-- STEP 3: Add prescription limit columns to FRAME table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'minRx')
BEGIN
    ALTER TABLE [FRAME] ADD [minRx] FLOAT NULL;
    PRINT 'Added column: FRAME.minRx';
END
ELSE
    PRINT 'Column FRAME.minRx already exists - skipped';
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'maxRx')
BEGIN
    ALTER TABLE [FRAME] ADD [maxRx] FLOAT NULL;
    PRINT 'Added column: FRAME.maxRx';
END
ELSE
    PRINT 'Column FRAME.maxRx already exists - skipped';
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'minPd')
BEGIN
    ALTER TABLE [FRAME] ADD [minPd] INT NULL;
    PRINT 'Added column: FRAME.minPd';
END
ELSE
    PRINT 'Column FRAME.minPd already exists - skipped';
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'maxPd')
BEGIN
    ALTER TABLE [FRAME] ADD [maxPd] INT NULL;
    PRINT 'Added column: FRAME.maxPd';
END
ELSE
    PRINT 'Column FRAME.maxPd already exists - skipped';
GO

-- ============================================================================
-- STEP 4: Set sensible defaults for prescription limits on existing frames
-- (Optional - only updates frames that have NULL limits)
-- ============================================================================

-- Set default Rx range for existing frames: -8.00 to +6.00 (common range)
UPDATE FRAME
SET minRx = -8.00, maxRx = 6.00
WHERE minRx IS NULL AND maxRx IS NULL;

-- Set default PD range for existing frames: 54mm to 74mm (common adult range)
UPDATE FRAME
SET minPd = 54, maxPd = 74
WHERE minPd IS NULL AND maxPd IS NULL;

PRINT 'Set default Rx/PD limits for existing frames';
GO

-- ============================================================================
-- DONE
-- ============================================================================

PRINT '';
PRINT '=== Frame Variants & Lens Type Support Migration Complete ===';
PRINT '';
PRINT 'Summary of changes:';
PRINT '  - FRAME_COLOR: stockQuantity column (per-color variant stock)';
PRINT '  - FRAME_LENS_TYPE: junction table (frame <-> supported lens types)';
PRINT '  - FRAME: minRx, maxRx, minPd, maxPd columns (prescription limits)';
PRINT '';
PRINT 'IMPORTANT: After running this script, update your frame data:';
PRINT '  1. Set stockQuantity for each FRAME_COLOR variant';
PRINT '  2. Add FRAME_LENS_TYPE entries for frames that support specific lens types';
PRINT '  3. Adjust minRx/maxRx/minPd/maxPd per frame as needed';
GO
