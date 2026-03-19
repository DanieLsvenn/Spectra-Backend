-- Add cancelledByCustomer column to COMPLAINT_REQUEST table
-- This column tracks whether the complaint was cancelled by the customer themselves

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'COMPLAINT_REQUEST' AND COLUMN_NAME = 'cancelledByCustomer'
)
BEGIN
    ALTER TABLE COMPLAINT_REQUEST ADD cancelledByCustomer BIT NULL;
END
GO
