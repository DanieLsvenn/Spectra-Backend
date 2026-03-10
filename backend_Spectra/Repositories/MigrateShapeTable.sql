-- ============================================================================
-- SHAPE TABLE MIGRATION SCRIPT
-- Creates the SHAPE lookup table and migrates the FRAME.shape string column
-- to a FRAME.shapeId FK relationship.
--
-- This script is IDEMPOTENT - safe to run multiple times.
-- It uses IF NOT EXISTS / IF EXISTS checks before every change.
--
-- Run against your SQL Server database (e.g. via SSMS or sqlcmd).
-- Run this AFTER MigrateSchema.sql and BEFORE starting the application.
-- ============================================================================

PRINT '=== Starting Shape Table Migration ===';
PRINT '';

-- ============================================================================
-- STEP 1: Create the SHAPE table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SHAPE')
BEGIN
    CREATE TABLE [SHAPE] (
        [shapeId]   UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWID()),
        [shapeName] VARCHAR(100)     NOT NULL,
        [status]    VARCHAR(50)      NULL     DEFAULT 'active',
        CONSTRAINT [PK_SHAPE] PRIMARY KEY ([shapeId])
    );
    PRINT 'Created table: SHAPE';
END
ELSE
    PRINT 'Table SHAPE already exists - skipped';
GO

-- ============================================================================
-- STEP 2: Migrate existing shape string values from FRAME into SHAPE table
-- ============================================================================

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'shape')
BEGIN
    -- Insert distinct shape values into SHAPE table (skip NULLs and empties)
    INSERT INTO [SHAPE] ([shapeId], [shapeName])
    SELECT NEWID(), shape
    FROM (SELECT DISTINCT [shape] FROM [FRAME] WHERE [shape] IS NOT NULL AND [shape] <> '') AS src
    WHERE NOT EXISTS (SELECT 1 FROM [SHAPE] s WHERE s.[shapeName] = src.[shape]);

    PRINT 'Migrated distinct shape values from FRAME.shape -> SHAPE table';
END
ELSE
    PRINT 'Column FRAME.shape does not exist (may already be migrated) - skipped data migration';
GO

-- ============================================================================
-- STEP 3: Add shapeId column to FRAME table
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'shapeId')
BEGIN
    ALTER TABLE [FRAME] ADD [shapeId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added column: FRAME.shapeId';
END
ELSE
    PRINT 'Column FRAME.shapeId already exists - skipped';
GO

-- ============================================================================
-- STEP 4: Backfill shapeId from old shape string column
-- ============================================================================

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'shape')
   AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'shapeId')
BEGIN
    UPDATE f
    SET f.[shapeId] = s.[shapeId]
    FROM [FRAME] f
    INNER JOIN [SHAPE] s ON s.[shapeName] = f.[shape]
    WHERE f.[shapeId] IS NULL AND f.[shape] IS NOT NULL AND f.[shape] <> '';

    PRINT 'Backfilled FRAME.shapeId from FRAME.shape';
END
ELSE
    PRINT 'Backfill not needed (old shape column missing or shapeId column missing) - skipped';
GO

-- ============================================================================
-- STEP 5: Add FK constraint from FRAME to SHAPE
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_FRAME_SHAPE')
BEGIN
    ALTER TABLE [FRAME] ADD CONSTRAINT [FK_FRAME_SHAPE]
        FOREIGN KEY ([shapeId]) REFERENCES [SHAPE]([shapeId]);
    PRINT 'Added FK: FK_FRAME_SHAPE';
END
ELSE
    PRINT 'FK FK_FRAME_SHAPE already exists - skipped';
GO

-- ============================================================================
-- STEP 6: Drop old shape string column from FRAME (after data is migrated)
-- ============================================================================

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'shape')
BEGIN
    ALTER TABLE [FRAME] DROP COLUMN [shape];
    PRINT 'Dropped column: FRAME.shape';
END
ELSE
    PRINT 'Column FRAME.shape does not exist - skipped';
GO

-- ============================================================================
-- STEP 7: Seed some common eyeglass frame shapes (if table is empty)
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM [SHAPE])
BEGIN
    INSERT INTO [SHAPE] ([shapeId], [shapeName], [status]) VALUES
        (NEWID(), 'Rectangle', 'active'),
        (NEWID(), 'Round', 'active'),
        (NEWID(), 'Square', 'active'),
        (NEWID(), 'Oval', 'active'),
        (NEWID(), 'Cat Eye', 'active'),
        (NEWID(), 'Aviator', 'active'),
        (NEWID(), 'Browline', 'active'),
        (NEWID(), 'Geometric', 'active'),
        (NEWID(), 'Wayfarer', 'active'),
        (NEWID(), 'Rimless', 'active');
    PRINT 'Seeded SHAPE table with common eyeglass frame shapes';
END
ELSE
    PRINT 'SHAPE table already has data - skipped seeding';
GO

-- ============================================================================
-- DONE
-- ============================================================================

PRINT '';
PRINT '=== Shape Table Migration Complete ===';
PRINT '';
PRINT 'Summary of changes:';
PRINT '  - SHAPE table (created or verified)';
PRINT '  - FRAME.shape string values migrated to SHAPE rows (if old column existed)';
PRINT '  - FRAME.shapeId column (added or verified)';
PRINT '  - FRAME.shapeId backfilled from old FRAME.shape (if old column existed)';
PRINT '  - FK_FRAME_SHAPE constraint (added or verified)';
PRINT '  - Old FRAME.shape column dropped (if it existed)';
PRINT '  - Common shape values seeded (if table was empty)';
GO
