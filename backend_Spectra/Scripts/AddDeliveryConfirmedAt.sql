-- Add deliveryConfirmedAt column to ORDERS table
-- This column tracks when the customer confirmed they received their order

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'deliveryConfirmedAt'
)
BEGIN
    ALTER TABLE ORDERS ADD deliveryConfirmedAt DATETIME NULL;
END
GO
