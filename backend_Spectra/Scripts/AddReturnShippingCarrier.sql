-- Add returnShippingCarrier column to COMPLAINT_REQUEST table
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'COMPLAINT_REQUEST' AND COLUMN_NAME = 'returnShippingCarrier'
)
BEGIN
    ALTER TABLE COMPLAINT_REQUEST ADD returnShippingCarrier NVARCHAR(100) NULL;
    PRINT 'Added returnShippingCarrier column to COMPLAINT_REQUEST';
END
ELSE
BEGIN
    PRINT 'Column returnShippingCarrier already exists';
END
