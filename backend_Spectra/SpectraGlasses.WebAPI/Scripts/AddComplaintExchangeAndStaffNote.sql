-- ============================================================================
-- SCHEMA MIGRATION SCRIPT - Add ExchangeOrderId and StaffNote to COMPLAINT_REQUEST
-- Adds: exchangeOrderId column to link exchange complaints to replacement orders
-- Adds: staffNote column for staff resolution notes
--
-- This script is IDEMPOTENT - safe to run multiple times.
-- Run this BEFORE starting the application after updating the code.
-- ============================================================================

PRINT '=== Adding exchangeOrderId and staffNote to COMPLAINT_REQUEST table ===';

-- Add exchangeOrderId column
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('COMPLAINT_REQUEST') AND name = 'exchangeOrderId'
)
BEGIN
    ALTER TABLE [COMPLAINT_REQUEST]
        ADD [exchangeOrderId] UNIQUEIDENTIFIER NULL;
    PRINT 'Added exchangeOrderId column to COMPLAINT_REQUEST table.';
END
ELSE
BEGIN
    PRINT 'exchangeOrderId column already exists in COMPLAINT_REQUEST table. Skipping.';
END

-- Add staffNote column
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('COMPLAINT_REQUEST') AND name = 'staffNote'
)
BEGIN
    ALTER TABLE [COMPLAINT_REQUEST]
        ADD [staffNote] VARCHAR(500) NULL;
    PRINT 'Added staffNote column to COMPLAINT_REQUEST table.';
END
ELSE
BEGIN
    PRINT 'staffNote column already exists in COMPLAINT_REQUEST table. Skipping.';
END

-- Add foreign key constraint for exchangeOrderId -> ORDERS
IF NOT EXISTS (
    SELECT * FROM sys.foreign_keys 
    WHERE name = 'FK_COMPLAINT_EXCHANGE_ORDER'
)
BEGIN
    ALTER TABLE [COMPLAINT_REQUEST]
        ADD CONSTRAINT [FK_COMPLAINT_EXCHANGE_ORDER]
        FOREIGN KEY ([exchangeOrderId]) REFERENCES [ORDERS]([orderId]);
    PRINT 'Added FK_COMPLAINT_EXCHANGE_ORDER foreign key constraint.';
END
ELSE
BEGIN
    PRINT 'FK_COMPLAINT_EXCHANGE_ORDER constraint already exists. Skipping.';
END

PRINT '';
PRINT '=== Migration Complete ===';
