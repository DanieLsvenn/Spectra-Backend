-- ============================================================================
-- SCHEMA MIGRATION SCRIPT - Add ConvertedFromPreorderId to ORDERS
-- Adds: convertedFromPreorderId column to the ORDERS table
-- Links converted preorders back to their original preorder record.
--
-- This script is IDEMPOTENT - safe to run multiple times.
-- Run this BEFORE starting the application after updating the code.
-- ============================================================================

PRINT '=== Adding convertedFromPreorderId to ORDERS table ===';

IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('ORDERS') AND name = 'convertedFromPreorderId'
)
BEGIN
    ALTER TABLE [ORDERS]
        ADD [convertedFromPreorderId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added convertedFromPreorderId column to ORDERS table.';
END
ELSE
BEGIN
    PRINT 'convertedFromPreorderId column already exists in ORDERS table. Skipping.';
END

-- Add foreign key constraint
IF NOT EXISTS (
    SELECT * FROM sys.foreign_keys 
    WHERE name = 'FK_ORDERS_PREORDER_CONVERTED'
)
BEGIN
    ALTER TABLE [ORDERS]
        ADD CONSTRAINT [FK_ORDERS_PREORDER_CONVERTED]
        FOREIGN KEY ([convertedFromPreorderId]) REFERENCES [PREORDER]([preorderId]);
    PRINT 'Added FK_ORDERS_PREORDER_CONVERTED foreign key constraint.';
END
ELSE
BEGIN
    PRINT 'FK_ORDERS_PREORDER_CONVERTED constraint already exists. Skipping.';
END

PRINT '';
PRINT '=== Migration Complete ===';
