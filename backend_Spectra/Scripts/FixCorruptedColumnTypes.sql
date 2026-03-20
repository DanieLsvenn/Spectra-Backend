-- ============================================================================
-- FIX CORRUPTED COLUMN TYPES
-- The FixUnicodeColumns.sql script accidentally converted numeric (FLOAT) and
-- boolean (BIT) columns to NVARCHAR. This script reverts them to their
-- correct data types.
--
-- Affected columns:
--   LENS_INDEX.additionalPrice  NVARCHAR(500) -> FLOAT (should be FLOAT NOT NULL DEFAULT 0)
--   LENS_INDEX.minPrescription  NVARCHAR(500) -> FLOAT (should be FLOAT NULL)
--   FRAME_SIZE.isDefault        NVARCHAR(50)  -> BIT   (should be BIT NULL DEFAULT 0)
--
-- This script is IDEMPOTENT - safe to run multiple times.
-- ============================================================================

PRINT '=== Fixing corrupted column types ===';
PRINT '';

-- ============================================================================
-- 1. Fix LENS_INDEX.additionalPrice: NVARCHAR -> FLOAT
-- ============================================================================
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'LENS_INDEX' AND COLUMN_NAME = 'additionalPrice'
      AND DATA_TYPE IN ('nvarchar', 'varchar')
)
BEGIN
    -- Drop default constraint if exists
    DECLARE @defName1 NVARCHAR(256);
    SELECT @defName1 = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    JOIN sys.tables t ON dc.parent_object_id = t.object_id
    WHERE t.name = 'LENS_INDEX' AND c.name = 'additionalPrice';

    IF @defName1 IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [LENS_INDEX] DROP CONSTRAINT [' + @defName1 + ']');
    END

    -- Convert existing string data to float-compatible values (NULL out any non-numeric)
    UPDATE [LENS_INDEX]
    SET [additionalPrice] = NULL
    WHERE TRY_CAST([additionalPrice] AS FLOAT) IS NULL AND [additionalPrice] IS NOT NULL;

    ALTER TABLE [LENS_INDEX] ALTER COLUMN [additionalPrice] FLOAT NOT NULL;
    ALTER TABLE [LENS_INDEX] ADD CONSTRAINT [DF_LENS_INDEX_additionalPrice] DEFAULT (0) FOR [additionalPrice];
    PRINT 'Fixed: LENS_INDEX.additionalPrice -> FLOAT NOT NULL DEFAULT 0';
END
ELSE
    PRINT 'LENS_INDEX.additionalPrice is already correct type - skipped';
GO

-- ============================================================================
-- 2. Fix LENS_INDEX.minPrescription: NVARCHAR -> FLOAT
-- ============================================================================
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'LENS_INDEX' AND COLUMN_NAME = 'minPrescription'
      AND DATA_TYPE IN ('nvarchar', 'varchar')
)
BEGIN
    DECLARE @defName2 NVARCHAR(256);
    SELECT @defName2 = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    JOIN sys.tables t ON dc.parent_object_id = t.object_id
    WHERE t.name = 'LENS_INDEX' AND c.name = 'minPrescription';

    IF @defName2 IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [LENS_INDEX] DROP CONSTRAINT [' + @defName2 + ']');
    END

    UPDATE [LENS_INDEX]
    SET [minPrescription] = NULL
    WHERE TRY_CAST([minPrescription] AS FLOAT) IS NULL AND [minPrescription] IS NOT NULL;

    ALTER TABLE [LENS_INDEX] ALTER COLUMN [minPrescription] FLOAT NULL;
    PRINT 'Fixed: LENS_INDEX.minPrescription -> FLOAT NULL';
END
ELSE
    PRINT 'LENS_INDEX.minPrescription is already correct type - skipped';
GO

-- ============================================================================
-- 3. Fix FRAME_SIZE.isDefault: NVARCHAR -> BIT
-- ============================================================================
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'FRAME_SIZE' AND COLUMN_NAME = 'isDefault'
      AND DATA_TYPE IN ('nvarchar', 'varchar')
)
BEGIN
    DECLARE @defName3 NVARCHAR(256);
    SELECT @defName3 = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    JOIN sys.tables t ON dc.parent_object_id = t.object_id
    WHERE t.name = 'FRAME_SIZE' AND c.name = 'isDefault';

    IF @defName3 IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [FRAME_SIZE] DROP CONSTRAINT [' + @defName3 + ']');
    END

    -- Convert string values: 'True'/'1' -> 1, everything else -> 0
    UPDATE [FRAME_SIZE]
    SET [isDefault] = CASE
        WHEN [isDefault] IN ('1', 'True', 'true', 'TRUE') THEN '1'
        ELSE '0'
    END
    WHERE [isDefault] IS NOT NULL;

    ALTER TABLE [FRAME_SIZE] ALTER COLUMN [isDefault] BIT NULL;
    ALTER TABLE [FRAME_SIZE] ADD CONSTRAINT [DF_FRAME_SIZE_isDefault] DEFAULT (0) FOR [isDefault];
    PRINT 'Fixed: FRAME_SIZE.isDefault -> BIT NULL DEFAULT 0';
END
ELSE
    PRINT 'FRAME_SIZE.isDefault is already correct type - skipped';
GO

-- ============================================================================
-- 4. Add missing cancelledByCustomer column to ORDERS table
--    (The Order C# model has this property but no migration added it to ORDERS)
-- ============================================================================
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'cancelledByCustomer'
)
BEGIN
    ALTER TABLE [ORDERS] ADD [cancelledByCustomer] BIT NULL;
    PRINT 'Added missing column: ORDERS.cancelledByCustomer (BIT NULL)';
END
ELSE
    PRINT 'ORDERS.cancelledByCustomer already exists - skipped';
GO

PRINT '';
PRINT '=== Column type fix complete ===';
GO
