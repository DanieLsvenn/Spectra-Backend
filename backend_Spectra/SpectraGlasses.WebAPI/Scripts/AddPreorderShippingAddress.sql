-- ============================================================================
-- SCHEMA MIGRATION SCRIPT - Add ShippingAddress to PREORDER
-- Adds: shippingAddress column to the PREORDER table
--
-- This script is IDEMPOTENT - safe to run multiple times.
-- Run this BEFORE starting the application after updating the code.
-- ============================================================================

PRINT '=== Adding shippingAddress to PREORDER table ===';

IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('PREORDER') AND name = 'shippingAddress'
)
BEGIN
    ALTER TABLE [PREORDER]
        ADD [shippingAddress] NVARCHAR(500) NULL;
    PRINT 'Added shippingAddress column to PREORDER table.';
END
ELSE
BEGIN
    PRINT 'shippingAddress column already exists in PREORDER table. Skipping.';
END

PRINT '';
PRINT '=== Migration Complete ===';
