-- Add EstimatedDeliveryDate column to ORDERS table
-- Run this on the MonsterASP MSSQL database (db42530)

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'estimatedDeliveryDate'
)
BEGIN
    ALTER TABLE [ORDERS]
    ADD [estimatedDeliveryDate] DATETIME NULL;
    PRINT 'Column estimatedDeliveryDate added to ORDERS table.';
END
ELSE
BEGIN
    PRINT 'Column estimatedDeliveryDate already exists.';
END
GO
