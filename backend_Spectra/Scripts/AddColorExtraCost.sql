-- Add colorExtraCost column to FRAME_COLOR table
-- This allows each frame-color combination to have a case-by-case extra cost

IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('FRAME_COLOR') AND name = 'colorExtraCost'
)
BEGIN
    ALTER TABLE FRAME_COLOR ADD colorExtraCost float NULL DEFAULT 0;
    PRINT 'Added colorExtraCost column to FRAME_COLOR table';
END
ELSE
BEGIN
    PRINT 'colorExtraCost column already exists in FRAME_COLOR table';
END
GO
