-- Add Notes column to ORDERS table
-- This column stores optional customer notes/instructions for the order

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'notes'
)
BEGIN
    ALTER TABLE ORDERS ADD notes NVARCHAR(MAX) NULL;
    PRINT 'Added notes column to ORDERS table.';
END
ELSE
BEGIN
    PRINT 'Column notes already exists in ORDERS table. Skipping.';
END
GO
