-- =============================================
-- Fix Vietnamese Unicode Support
-- Changes all varchar columns to nvarchar
-- so Vietnamese diacritical marks (ễ, ư, ạ, ổ)
-- are stored correctly instead of becoming '?'
--
-- Safe & idempotent: drops constraints before
-- altering each column, then re-creates them.
-- Can be re-run if it fails partway.
-- =============================================
SET NOCOUNT ON;

-- Helper: drops ALL default/check constraints on a column before altering
-- Then alters the column, then re-adds any default.
-- Usage: EXEC #AlterToNvarchar 'TABLE', 'column', 100, 1
--   @notNull = 1 means NOT NULL, 0 means NULL

IF OBJECT_ID('tempdb..#AlterToNvarchar') IS NOT NULL DROP PROCEDURE #AlterToNvarchar;
GO

CREATE PROCEDURE #AlterToNvarchar
    @tableName NVARCHAR(128),
    @columnName NVARCHAR(128),
    @maxLen INT,
    @notNull BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @sql NVARCHAR(MAX);
    DECLARE @constraintName NVARCHAR(256);
    DECLARE @defaultDef NVARCHAR(MAX);
    DECLARE @name NVARCHAR(256);

    -- 1) Save default definition (if any) before dropping
    SELECT @defaultDef = dc.definition, @constraintName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    JOIN sys.tables t ON dc.parent_object_id = t.object_id
    WHERE t.name = @tableName AND c.name = @columnName;

    -- 2) Drop all default constraints on this column
    DECLARE csr CURSOR LOCAL FAST_FORWARD FOR
        SELECT dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        JOIN sys.tables t ON dc.parent_object_id = t.object_id
        WHERE t.name = @tableName AND c.name = @columnName;
    OPEN csr;
    FETCH NEXT FROM csr INTO @name;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = 'ALTER TABLE [' + @tableName + '] DROP CONSTRAINT [' + @name + ']';
        EXEC sp_executesql @sql;
        FETCH NEXT FROM csr INTO @name;
    END
    CLOSE csr; DEALLOCATE csr;

    -- 3) Drop all check constraints on this column
    DECLARE csr2 CURSOR LOCAL FAST_FORWARD FOR
        SELECT cc.name
        FROM sys.check_constraints cc
        JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
        JOIN sys.tables t ON cc.parent_object_id = t.object_id
        WHERE t.name = @tableName AND c.name = @columnName;
    OPEN csr2;
    FETCH NEXT FROM csr2 INTO @name;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = 'ALTER TABLE [' + @tableName + '] DROP CONSTRAINT [' + @name + ']';
        EXEC sp_executesql @sql;
        FETCH NEXT FROM csr2 INTO @name;
    END
    CLOSE csr2; DEALLOCATE csr2;

    -- 4) Save & drop unique constraints/indexes that include this column
    IF OBJECT_ID('tempdb..#UniqueBackup') IS NOT NULL DROP TABLE #UniqueBackup;
    CREATE TABLE #UniqueBackup (IndexName NVARCHAR(256), IsConstraint BIT, IsUnique BIT, Cols NVARCHAR(MAX));

    INSERT INTO #UniqueBackup (IndexName, IsConstraint, IsUnique, Cols)
    SELECT DISTINCT i.name, i.is_unique_constraint, i.is_unique,
        STUFF((
            SELECT ',[' + c2.name + ']' + CASE WHEN ic2.is_descending_key = 1 THEN ' DESC' ELSE ' ASC' END
            FROM sys.index_columns ic2
            JOIN sys.columns c2 ON ic2.object_id = c2.object_id AND ic2.column_id = c2.column_id
            WHERE ic2.object_id = i.object_id AND ic2.index_id = i.index_id AND ic2.is_included_column = 0
            ORDER BY ic2.key_ordinal
            FOR XML PATH('')
        ), 1, 1, '')
    FROM sys.indexes i
    JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    JOIN sys.tables t ON i.object_id = t.object_id
    WHERE t.name = @tableName AND c.name = @columnName
      AND (i.is_unique_constraint = 1 OR (i.is_unique = 1 AND i.is_primary_key = 0));

    DECLARE csr3 CURSOR LOCAL FAST_FORWARD FOR
        SELECT IndexName, IsConstraint FROM #UniqueBackup;
    OPEN csr3;
    DECLARE @isConstraint BIT;
    FETCH NEXT FROM csr3 INTO @name, @isConstraint;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @isConstraint = 1
            SET @sql = 'ALTER TABLE [' + @tableName + '] DROP CONSTRAINT [' + @name + ']';
        ELSE
            SET @sql = 'DROP INDEX [' + @name + '] ON [' + @tableName + ']';
        EXEC sp_executesql @sql;
        FETCH NEXT FROM csr3 INTO @name, @isConstraint;
    END
    CLOSE csr3; DEALLOCATE csr3;

    -- 5) Save & drop foreign key constraints that reference this column
    IF OBJECT_ID('tempdb..#FKBackup') IS NOT NULL DROP TABLE #FKBackup;
    CREATE TABLE #FKBackup (
        FKName NVARCHAR(256),
        ParentTable NVARCHAR(256),
        ParentCol NVARCHAR(256),
        RefTable NVARCHAR(256),
        RefCol NVARCHAR(256),
        DeleteAction NVARCHAR(20),
        UpdateAction NVARCHAR(20)
    );

    INSERT INTO #FKBackup (FKName, ParentTable, ParentCol, RefTable, RefCol, DeleteAction, UpdateAction)
    SELECT fk.name, OBJECT_NAME(fk.parent_object_id), pc.name, OBJECT_NAME(fk.referenced_object_id), rc.name,
        CASE fk.delete_referential_action WHEN 1 THEN 'CASCADE' WHEN 2 THEN 'SET NULL' WHEN 3 THEN 'SET DEFAULT' ELSE 'NO ACTION' END,
        CASE fk.update_referential_action WHEN 1 THEN 'CASCADE' WHEN 2 THEN 'SET NULL' WHEN 3 THEN 'SET DEFAULT' ELSE 'NO ACTION' END
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
    JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
    WHERE (OBJECT_NAME(fk.parent_object_id) = @tableName AND pc.name = @columnName)
       OR (OBJECT_NAME(fk.referenced_object_id) = @tableName AND rc.name = @columnName);

    DECLARE @fkName NVARCHAR(256), @fkParent NVARCHAR(256);
    DECLARE csrFK CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT FKName, ParentTable FROM #FKBackup;
    OPEN csrFK;
    FETCH NEXT FROM csrFK INTO @fkName, @fkParent;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = 'ALTER TABLE [' + @fkParent + '] DROP CONSTRAINT [' + @fkName + ']';
        EXEC sp_executesql @sql;
        FETCH NEXT FROM csrFK INTO @fkName, @fkParent;
    END
    CLOSE csrFK; DEALLOCATE csrFK;

    -- 6) Alter the column
    SET @sql = 'ALTER TABLE [' + @tableName + '] ALTER COLUMN [' + @columnName + '] NVARCHAR(' + CAST(@maxLen AS NVARCHAR(10)) + ') ' + CASE WHEN @notNull = 1 THEN 'NOT NULL' ELSE 'NULL' END;
    EXEC sp_executesql @sql;

    -- 7) Re-add foreign key constraints
    DECLARE @fkRefTable NVARCHAR(256), @fkParentCol NVARCHAR(256), @fkRefCol NVARCHAR(256);
    DECLARE @fkDelAction NVARCHAR(20), @fkUpdAction NVARCHAR(20);
    DECLARE csrFK2 CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT FKName, ParentTable, ParentCol, RefTable, RefCol, DeleteAction, UpdateAction FROM #FKBackup;
    OPEN csrFK2;
    FETCH NEXT FROM csrFK2 INTO @fkName, @fkParent, @fkParentCol, @fkRefTable, @fkRefCol, @fkDelAction, @fkUpdAction;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = 'ALTER TABLE [' + @fkParent + '] ADD CONSTRAINT [' + @fkName + '] FOREIGN KEY ([' + @fkParentCol + ']) REFERENCES [' + @fkRefTable + '] ([' + @fkRefCol + ']) ON DELETE ' + @fkDelAction + ' ON UPDATE ' + @fkUpdAction;
        EXEC sp_executesql @sql;
        FETCH NEXT FROM csrFK2 INTO @fkName, @fkParent, @fkParentCol, @fkRefTable, @fkRefCol, @fkDelAction, @fkUpdAction;
    END
    CLOSE csrFK2; DEALLOCATE csrFK2;
    DROP TABLE #FKBackup;

    -- 8) Re-add unique constraints/indexes
    DECLARE csr4 CURSOR LOCAL FAST_FORWARD FOR
        SELECT IndexName, IsConstraint, Cols FROM #UniqueBackup;
    DECLARE @cols NVARCHAR(MAX);
    OPEN csr4;
    FETCH NEXT FROM csr4 INTO @name, @isConstraint, @cols;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @isConstraint = 1
            SET @sql = 'ALTER TABLE [' + @tableName + '] ADD CONSTRAINT [' + @name + '] UNIQUE (' + @cols + ')';
        ELSE
            SET @sql = 'CREATE UNIQUE INDEX [' + @name + '] ON [' + @tableName + '] (' + @cols + ')';
        EXEC sp_executesql @sql;
        FETCH NEXT FROM csr4 INTO @name, @isConstraint, @cols;
    END
    CLOSE csr4; DEALLOCATE csr4;

    DROP TABLE #UniqueBackup;

    -- 9) Re-add default constraint if there was one
    IF @defaultDef IS NOT NULL AND @constraintName IS NOT NULL
    BEGIN
        SET @sql = 'ALTER TABLE [' + @tableName + '] ADD CONSTRAINT [' + @constraintName + '] DEFAULT ' + @defaultDef + ' FOR [' + @columnName + ']';
        EXEC sp_executesql @sql;
    END

    PRINT '  [' + @tableName + '].[' + @columnName + '] -> NVARCHAR(' + CAST(@maxLen AS NVARCHAR(10)) + ') OK';
END
GO

-- ============================================
-- Now alter all columns (safe to re-run)
-- ============================================

PRINT 'Converting columns to NVARCHAR...';
PRINT '';

PRINT 'BRAND';
EXEC #AlterToNvarchar 'BRAND', 'brandName', 100, 1;
EXEC #AlterToNvarchar 'BRAND', 'status', 50, 0;

PRINT 'MATERIAL';
EXEC #AlterToNvarchar 'MATERIAL', 'materialName', 100, 1;
EXEC #AlterToNvarchar 'MATERIAL', 'status', 50, 0;

PRINT 'COLOR';
EXEC #AlterToNvarchar 'COLOR', 'colorName', 50, 1;
EXEC #AlterToNvarchar 'COLOR', 'hexCode', 7, 0;
EXEC #AlterToNvarchar 'COLOR', 'status', 50, 0;

PRINT 'SHAPE';
EXEC #AlterToNvarchar 'SHAPE', 'shapeName', 100, 1;
EXEC #AlterToNvarchar 'SHAPE', 'status', 50, 0;

PRINT 'FRAME_SIZE';
EXEC #AlterToNvarchar 'FRAME_SIZE', 'size', 50, 0;
EXEC #AlterToNvarchar 'FRAME_SIZE', 'isDefault', 50, 0;

PRINT 'LENS_INDEX';
EXEC #AlterToNvarchar 'LENS_INDEX', 'name', 100, 0;
EXEC #AlterToNvarchar 'LENS_INDEX', 'description', 500, 0;
EXEC #AlterToNvarchar 'LENS_INDEX', 'additionalPrice', 500, 0;
EXEC #AlterToNvarchar 'LENS_INDEX', 'minPrescription', 500, 0;
EXEC #AlterToNvarchar 'LENS_INDEX', 'status', 50, 0;

PRINT 'COMPLAINT_REQUEST';
EXEC #AlterToNvarchar 'COMPLAINT_REQUEST', 'reason', 250, 0;
EXEC #AlterToNvarchar 'COMPLAINT_REQUEST', 'requestType', 100, 0;
EXEC #AlterToNvarchar 'COMPLAINT_REQUEST', 'status', 50, 0;
EXEC #AlterToNvarchar 'COMPLAINT_REQUEST', 'staffNote', 500, 0;

PRINT 'FRAME';
EXEC #AlterToNvarchar 'FRAME', 'frameName', 100, 1;
EXEC #AlterToNvarchar 'FRAME', 'size', 50, 0;
EXEC #AlterToNvarchar 'FRAME', 'status', 50, 0;

PRINT 'FRAME_MEDIA';
EXEC #AlterToNvarchar 'FRAME_MEDIA', 'mediaType', 50, 0;
EXEC #AlterToNvarchar 'FRAME_MEDIA', 'mediaUrl', 2048, 0;

PRINT 'LENS_FEATURE';
EXEC #AlterToNvarchar 'LENS_FEATURE', 'featureSpecification', 200, 0;

PRINT 'LENS_TYPE';
EXEC #AlterToNvarchar 'LENS_TYPE', 'lensSpecification', 200, 0;
EXEC #AlterToNvarchar 'LENS_TYPE', 'description', 500, 0;
EXEC #AlterToNvarchar 'LENS_TYPE', 'category', 50, 0;
EXEC #AlterToNvarchar 'LENS_TYPE', 'status', 50, 0;

PRINT 'ORDERS';
EXEC #AlterToNvarchar 'ORDERS', 'shippingAddress', 200, 0;
EXEC #AlterToNvarchar 'ORDERS', 'status', 50, 0;
EXEC #AlterToNvarchar 'ORDERS', 'shippingMethod', 50, 0;
EXEC #AlterToNvarchar 'ORDERS', 'trackingNumber', 100, 0;
EXEC #AlterToNvarchar 'ORDERS', 'shippingCarrier', 50, 0;

PRINT 'ORDER_ITEM';
EXEC #AlterToNvarchar 'ORDER_ITEM', 'selectedSize', 50, 0;

PRINT 'PAYMENT';
EXEC #AlterToNvarchar 'PAYMENT', 'paymentMethod', 100, 0;
EXEC #AlterToNvarchar 'PAYMENT', 'paymentStatus', 50, 0;

PRINT 'PREORDER';
EXEC #AlterToNvarchar 'PREORDER', 'status', 50, 0;
EXEC #AlterToNvarchar 'PREORDER', 'shippingAddress', 500, 0;

PRINT 'PREORDER_ITEM';
EXEC #AlterToNvarchar 'PREORDER_ITEM', 'selectedSize', 50, 0;

PRINT 'PRESCRIPTION';
EXEC #AlterToNvarchar 'PRESCRIPTION', 'clinicName', 100, 0;
EXEC #AlterToNvarchar 'PRESCRIPTION', 'doctorName', 100, 0;

PRINT 'USER';
EXEC #AlterToNvarchar 'USER', 'address', 200, 0;
EXEC #AlterToNvarchar 'USER', 'email', 150, 0;
EXEC #AlterToNvarchar 'USER', 'fullName', 100, 0;
EXEC #AlterToNvarchar 'USER', 'passwordHash', 255, 0;
EXEC #AlterToNvarchar 'USER', 'phone', 20, 0;
EXEC #AlterToNvarchar 'USER', 'role', 50, 0;
EXEC #AlterToNvarchar 'USER', 'status', 20, 0;

PRINT 'PRODUCT_REVIEW';
EXEC #AlterToNvarchar 'PRODUCT_REVIEW', 'title', 200, 0;
EXEC #AlterToNvarchar 'PRODUCT_REVIEW', 'comment', 200, 0;
EXEC #AlterToNvarchar 'PRODUCT_REVIEW', 'status', 50, 0;

PRINT 'PREORDER_CAMPAIGN';
EXEC #AlterToNvarchar 'PREORDER_CAMPAIGN', 'campaignName', 200, 0;
EXEC #AlterToNvarchar 'PREORDER_CAMPAIGN', 'description', 200, 0;
EXEC #AlterToNvarchar 'PREORDER_CAMPAIGN', 'status', 50, 0;

PRINT 'PREORDER_STATUS_LOG';
EXEC #AlterToNvarchar 'PREORDER_STATUS_LOG', 'previousStatus', 50, 0;
EXEC #AlterToNvarchar 'PREORDER_STATUS_LOG', 'newStatus', 50, 0;
EXEC #AlterToNvarchar 'PREORDER_STATUS_LOG', 'message', 50, 0;
EXEC #AlterToNvarchar 'PREORDER_STATUS_LOG', 'createdBy', 50, 0;

-- ============================================
-- Fix TEXT columns -> NVARCHAR(MAX)
-- These had .HasColumnType("text") in EF Core
-- which is a non-Unicode legacy type
-- ============================================
PRINT '';
PRINT 'Converting TEXT columns to NVARCHAR(MAX)...';

PRINT 'COMPLAINT_REQUEST.mediaUrl';
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='COMPLAINT_REQUEST' AND COLUMN_NAME='mediaUrl' AND DATA_TYPE='text')
    ALTER TABLE [COMPLAINT_REQUEST] ALTER COLUMN [mediaUrl] NVARCHAR(MAX) NULL;
PRINT '  OK';

PRINT 'PREORDER.adminNotes';
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PREORDER' AND COLUMN_NAME='adminNotes' AND DATA_TYPE='text')
    ALTER TABLE [PREORDER] ALTER COLUMN [adminNotes] NVARCHAR(MAX) NULL;
PRINT '  OK';

PRINT 'PRODUCT_REVIEW.comment';
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PRODUCT_REVIEW' AND COLUMN_NAME='comment' AND DATA_TYPE='text')
    ALTER TABLE [PRODUCT_REVIEW] ALTER COLUMN [comment] NVARCHAR(MAX) NULL;
PRINT '  OK';

PRINT 'PREORDER_CAMPAIGN.description';
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PREORDER_CAMPAIGN' AND COLUMN_NAME='description' AND DATA_TYPE='text')
    ALTER TABLE [PREORDER_CAMPAIGN] ALTER COLUMN [description] NVARCHAR(MAX) NULL;
PRINT '  OK';

PRINT 'PREORDER_STATUS_LOG.message';
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PREORDER_STATUS_LOG' AND COLUMN_NAME='message' AND DATA_TYPE='text')
    ALTER TABLE [PREORDER_STATUS_LOG] ALTER COLUMN [message] NVARCHAR(MAX) NULL;
PRINT '  OK';

PRINT '';
PRINT 'Done! All columns converted to NVARCHAR. Vietnamese Unicode is now supported.';
GO

DROP PROCEDURE #AlterToNvarchar;
GO
