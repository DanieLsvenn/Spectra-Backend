-- Add inventory management columns to FRAME table
-- Run this script on your database to add stock tracking capabilities

-- Add stockQuantity column (defaults to 0)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'stockQuantity')
BEGIN
    ALTER TABLE FRAME ADD stockQuantity INT NULL;
    PRINT 'Added stockQuantity column to FRAME table';
END
ELSE
BEGIN
    PRINT 'stockQuantity column already exists';
END
GO

-- Add reorderLevel column (defaults to 5)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'FRAME') AND name = 'reorderLevel')
BEGIN
    ALTER TABLE FRAME ADD reorderLevel INT NULL;
    PRINT 'Added reorderLevel column to FRAME table';
END
ELSE
BEGIN
    PRINT 'reorderLevel column already exists';
END
GO

-- Update existing frames to have default stock values
-- This runs in a separate batch after columns are created
UPDATE FRAME 
SET stockQuantity = 10, reorderLevel = 5 
WHERE stockQuantity IS NULL OR reorderLevel IS NULL;

PRINT 'Inventory columns updated successfully!';
PRINT 'All existing frames have been set to stockQuantity=10, reorderLevel=5';
GO
