-- ============================================================================
-- LENS_INDEX POPULATION SCRIPT
-- Populates the LENS_INDEX table with standard optical lens indices.
--
-- This script is IDEMPOTENT - safe to run multiple times.
-- It checks by [name] before inserting to avoid duplicates.
--
-- NOTE: brandId and colorId are left NULL. Assign them afterwards if needed
--       via UPDATE statements or through the API.
-- ============================================================================

PRINT '=== Populating LENS_INDEX table ===';
PRINT '';

-- 1.50 - Standard Index (default, included with most frames)
IF NOT EXISTS (SELECT 1 FROM [LENS_INDEX] WHERE [name] = 'Standard 1.50')
BEGIN
    INSERT INTO [LENS_INDEX] ([lensIndexId], [indexValue], [name], [description], [additionalPrice], [minPrescription], [maxPrescription], [status])
    VALUES (NEWID(), 1.50, 'Standard 1.50',
            'Standard plastic lens. Best for low prescriptions. Thickest option but most affordable.',
            0.00, -2.00, 2.00, 'active');
    PRINT 'Inserted: Standard 1.50';
END
ELSE
    PRINT 'Standard 1.50 already exists - skipped';

-- 1.56 - Mid-Index
IF NOT EXISTS (SELECT 1 FROM [LENS_INDEX] WHERE [name] = 'Mid-Index 1.56')
BEGIN
    INSERT INTO [LENS_INDEX] ([lensIndexId], [indexValue], [name], [description], [additionalPrice], [minPrescription], [maxPrescription], [status])
    VALUES (NEWID(), 1.56, 'Mid-Index 1.56',
            'Slightly thinner than standard. Good balance of thickness and cost for mild prescriptions.',
            15.00, -4.00, 4.00, 'active');
    PRINT 'Inserted: Mid-Index 1.56';
END
ELSE
    PRINT 'Mid-Index 1.56 already exists - skipped';

-- 1.59 - Polycarbonate
IF NOT EXISTS (SELECT 1 FROM [LENS_INDEX] WHERE [name] = 'Polycarbonate 1.59')
BEGIN
    INSERT INTO [LENS_INDEX] ([lensIndexId], [indexValue], [name], [description], [additionalPrice], [minPrescription], [maxPrescription], [status])
    VALUES (NEWID(), 1.59, 'Polycarbonate 1.59',
            'Impact-resistant and lightweight. Ideal for sports, children, and safety eyewear. Built-in UV protection.',
            25.00, -6.00, 6.00, 'active');
    PRINT 'Inserted: Polycarbonate 1.59';
END
ELSE
    PRINT 'Polycarbonate 1.59 already exists - skipped';

-- 1.60 - Thin Lens
IF NOT EXISTS (SELECT 1 FROM [LENS_INDEX] WHERE [name] = 'Thin 1.60')
BEGIN
    INSERT INTO [LENS_INDEX] ([lensIndexId], [indexValue], [name], [description], [additionalPrice], [minPrescription], [maxPrescription], [status])
    VALUES (NEWID(), 1.60, 'Thin 1.60',
            'Thinner and lighter than standard. Recommended for moderate prescriptions. Reduces edge thickness noticeably.',
            35.00, -6.00, 6.00, 'active');
    PRINT 'Inserted: Thin 1.60';
END
ELSE
    PRINT 'Thin 1.60 already exists - skipped';

-- 1.67 - High-Index
IF NOT EXISTS (SELECT 1 FROM [LENS_INDEX] WHERE [name] = 'High-Index 1.67')
BEGIN
    INSERT INTO [LENS_INDEX] ([lensIndexId], [indexValue], [name], [description], [additionalPrice], [minPrescription], [maxPrescription], [status])
    VALUES (NEWID(), 1.67, 'High-Index 1.67',
            'Significantly thinner and lighter. Best for strong prescriptions. Great cosmetic appearance in any frame style.',
            55.00, -9.00, 9.00, 'active');
    PRINT 'Inserted: High-Index 1.67';
END
ELSE
    PRINT 'High-Index 1.67 already exists - skipped';

-- 1.74 - Ultra High-Index
IF NOT EXISTS (SELECT 1 FROM [LENS_INDEX] WHERE [name] = 'Ultra High-Index 1.74')
BEGIN
    INSERT INTO [LENS_INDEX] ([lensIndexId], [indexValue], [name], [description], [additionalPrice], [minPrescription], [maxPrescription], [status])
    VALUES (NEWID(), 1.74, 'Ultra High-Index 1.74',
            'Thinnest lens available. Ideal for very strong prescriptions. Maximum cosmetic appeal with minimal edge thickness.',
            85.00, -14.00, 14.00, 'active');
    PRINT 'Inserted: Ultra High-Index 1.74';
END
ELSE
    PRINT 'Ultra High-Index 1.74 already exists - skipped';

-- 1.53 - Trivex
IF NOT EXISTS (SELECT 1 FROM [LENS_INDEX] WHERE [name] = 'Trivex 1.53')
BEGIN
    INSERT INTO [LENS_INDEX] ([lensIndexId], [indexValue], [name], [description], [additionalPrice], [minPrescription], [maxPrescription], [status])
    VALUES (NEWID(), 1.53, 'Trivex 1.53',
            'Superior impact resistance and optical clarity. Lighter than polycarbonate with less chromatic aberration. Great for rimless frames.',
            30.00, -4.00, 4.00, 'active');
    PRINT 'Inserted: Trivex 1.53';
END
ELSE
    PRINT 'Trivex 1.53 already exists - skipped';

-- ============================================================================
-- DONE
-- ============================================================================

PRINT '';
PRINT '=== LENS_INDEX Population Complete ===';
PRINT '';
PRINT 'Inserted lens indices:';
PRINT '  1.50 - Standard         ($0    | -2.00 to +2.00)';
PRINT '  1.53 - Trivex           ($30   | -4.00 to +4.00)';
PRINT '  1.56 - Mid-Index        ($15   | -4.00 to +4.00)';
PRINT '  1.59 - Polycarbonate    ($25   | -6.00 to +6.00)';
PRINT '  1.60 - Thin             ($35   | -6.00 to +6.00)';
PRINT '  1.67 - High-Index       ($55   | -9.00 to +9.00)';
PRINT '  1.74 - Ultra High-Index ($85   | -14.00 to +14.00)';
PRINT '';
PRINT 'All brandId/colorId are NULL. Assign via the API or UPDATE statements as needed.';
GO
