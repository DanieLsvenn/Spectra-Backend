-- =====================================================
-- Complaint Flow Verification & Safety Script
-- Run this to verify the COMPLAINT_REQUEST table has
-- all required columns for the continuation flows.
-- No destructive changes — only adds columns if missing.
-- =====================================================

-- Verify / Add refundAmount column
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'COMPLAINT_REQUEST' AND COLUMN_NAME = 'refundAmount'
)
BEGIN
    ALTER TABLE COMPLAINT_REQUEST ADD refundAmount FLOAT NULL;
    PRINT 'Added refundAmount column';
END
ELSE PRINT 'refundAmount column already exists';

-- Verify / Add returnTrackingNumber column
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'COMPLAINT_REQUEST' AND COLUMN_NAME = 'returnTrackingNumber'
)
BEGIN
    ALTER TABLE COMPLAINT_REQUEST ADD returnTrackingNumber VARCHAR(100) NULL;
    PRINT 'Added returnTrackingNumber column';
END
ELSE PRINT 'returnTrackingNumber column already exists';

-- Verify / Add refundedAt column
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'COMPLAINT_REQUEST' AND COLUMN_NAME = 'refundedAt'
)
BEGIN
    ALTER TABLE COMPLAINT_REQUEST ADD refundedAt DATETIME NULL;
    PRINT 'Added refundedAt column';
END
ELSE PRINT 'refundedAt column already exists';

-- Verify / Add exchangeOrderId column
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'COMPLAINT_REQUEST' AND COLUMN_NAME = 'exchangeOrderId'
)
BEGIN
    ALTER TABLE COMPLAINT_REQUEST ADD exchangeOrderId UNIQUEIDENTIFIER NULL;
    PRINT 'Added exchangeOrderId column';
END
ELSE PRINT 'exchangeOrderId column already exists';

-- Verify / Add staffNote column
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'COMPLAINT_REQUEST' AND COLUMN_NAME = 'staffNote'
)
BEGIN
    ALTER TABLE COMPLAINT_REQUEST ADD staffNote VARCHAR(500) NULL;
    PRINT 'Added staffNote column';
END
ELSE PRINT 'staffNote column already exists';

-- Verify / Add FK for exchangeOrderId -> ORDERS
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_NAME = 'FK_COMPLAINT_EXCHANGE_ORDER'
)
BEGIN
    ALTER TABLE COMPLAINT_REQUEST
    ADD CONSTRAINT FK_COMPLAINT_EXCHANGE_ORDER
    FOREIGN KEY (exchangeOrderId) REFERENCES [ORDER](orderId);
    PRINT 'Added FK_COMPLAINT_EXCHANGE_ORDER constraint';
END
ELSE PRINT 'FK_COMPLAINT_EXCHANGE_ORDER already exists';

PRINT '';
PRINT '=== Verification complete. All complaint flow columns are present. ===';
