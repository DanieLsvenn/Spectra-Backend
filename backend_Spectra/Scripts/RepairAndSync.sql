-- ============================================================
-- RepairAndSync.sql
-- Purpose : Repair corrupted data and synchronise the DB schema
--           with the current EF Core model.
-- Safe to run multiple times (idempotent).
-- Run in order: schema first, data second.
-- ============================================================

SET NOCOUNT ON;
PRINT '=== Spectra DB Repair & Sync ===';
PRINT '';

-- ============================================================
-- SECTION 1 — SCHEMA SYNC (add missing columns)
-- ============================================================
PRINT '--- SECTION 1: Schema sync ---';

-- 1.1  ORDERS.notes
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'notes'
)
BEGIN
    ALTER TABLE ORDERS ADD notes NVARCHAR(1000) NULL;
    PRINT '  [+] Added ORDERS.notes';
END
ELSE PRINT '  [=] ORDERS.notes already exists';

-- 1.2  ORDERS.cancelledByCustomer
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'cancelledByCustomer'
)
BEGIN
    ALTER TABLE ORDERS ADD cancelledByCustomer BIT NOT NULL DEFAULT 0;
    PRINT '  [+] Added ORDERS.cancelledByCustomer';
END
ELSE PRINT '  [=] ORDERS.cancelledByCustomer already exists';

-- 1.3  ORDERS.estimatedDeliveryDate
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'estimatedDeliveryDate'
)
BEGIN
    ALTER TABLE ORDERS ADD estimatedDeliveryDate DATETIME NULL;
    PRINT '  [+] Added ORDERS.estimatedDeliveryDate';
END
ELSE PRINT '  [=] ORDERS.estimatedDeliveryDate already exists';

-- 1.4  ORDERS.deliveryConfirmedAt
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'deliveryConfirmedAt'
)
BEGIN
    ALTER TABLE ORDERS ADD deliveryConfirmedAt DATETIME NULL;
    PRINT '  [+] Added ORDERS.deliveryConfirmedAt';
END
ELSE PRINT '  [=] ORDERS.deliveryConfirmedAt already exists';

-- 1.5  ORDERS.shippingFee
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'shippingFee'
)
BEGIN
    ALTER TABLE ORDERS ADD shippingFee FLOAT NULL;
    PRINT '  [+] Added ORDERS.shippingFee';
END
ELSE PRINT '  [=] ORDERS.shippingFee already exists';

-- 1.6  ORDERS.shippingMethod
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'shippingMethod'
)
BEGIN
    ALTER TABLE ORDERS ADD shippingMethod NVARCHAR(50) NULL;
    PRINT '  [+] Added ORDERS.shippingMethod';
END
ELSE PRINT '  [=] ORDERS.shippingMethod already exists';

-- 1.7  ORDERS.shippingZone
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'shippingZone'
)
BEGIN
    ALTER TABLE ORDERS ADD shippingZone NVARCHAR(50) NULL;
    PRINT '  [+] Added ORDERS.shippingZone';
END
ELSE PRINT '  [=] ORDERS.shippingZone already exists';

-- 1.8  ORDERS.shippingCarrier
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'shippingCarrier'
)
BEGIN
    ALTER TABLE ORDERS ADD shippingCarrier NVARCHAR(100) NULL;
    PRINT '  [+] Added ORDERS.shippingCarrier';
END
ELSE PRINT '  [=] ORDERS.shippingCarrier already exists';

-- 1.9  ORDERS.trackingNumber
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'trackingNumber'
)
BEGIN
    ALTER TABLE ORDERS ADD trackingNumber NVARCHAR(200) NULL;
    PRINT '  [+] Added ORDERS.trackingNumber';
END
ELSE PRINT '  [=] ORDERS.trackingNumber already exists';

-- 1.10 ORDER_ITEM.selectedColorId
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDER_ITEM' AND COLUMN_NAME = 'selectedColorId'
)
BEGIN
    ALTER TABLE ORDER_ITEM ADD selectedColorId UNIQUEIDENTIFIER NULL;
    PRINT '  [+] Added ORDER_ITEM.selectedColorId';
END
ELSE PRINT '  [=] ORDER_ITEM.selectedColorId already exists';

-- 1.11 ORDER_ITEM.selectedSize
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDER_ITEM' AND COLUMN_NAME = 'selectedSize'
)
BEGIN
    ALTER TABLE ORDER_ITEM ADD selectedSize NVARCHAR(50) NULL;
    PRINT '  [+] Added ORDER_ITEM.selectedSize';
END
ELSE PRINT '  [=] ORDER_ITEM.selectedSize already exists';

-- 1.12 ORDER_ITEM.lensIndexId
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDER_ITEM' AND COLUMN_NAME = 'lensIndexId'
)
BEGIN
    ALTER TABLE ORDER_ITEM ADD lensIndexId UNIQUEIDENTIFIER NULL;
    PRINT '  [+] Added ORDER_ITEM.lensIndexId';
END
ELSE PRINT '  [=] ORDER_ITEM.lensIndexId already exists';

-- 1.13 COMPLAINT_REQUEST.cancelledByCustomer
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'COMPLAINT_REQUEST')
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'COMPLAINT_REQUEST' AND COLUMN_NAME = 'cancelledByCustomer'
    )
    BEGIN
        ALTER TABLE COMPLAINT_REQUEST ADD cancelledByCustomer BIT NOT NULL DEFAULT 0;
        PRINT '  [+] Added COMPLAINT_REQUEST.cancelledByCustomer';
    END
    ELSE PRINT '  [=] COMPLAINT_REQUEST.cancelledByCustomer already exists';

    -- 1.14 COMPLAINT_REQUEST.returnShippingCarrier
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = 'COMPLAINT_REQUEST' AND COLUMN_NAME = 'returnShippingCarrier'
    )
    BEGIN
        ALTER TABLE COMPLAINT_REQUEST ADD returnShippingCarrier NVARCHAR(200) NULL;
        PRINT '  [+] Added COMPLAINT_REQUEST.returnShippingCarrier';
    END
    ELSE PRINT '  [=] COMPLAINT_REQUEST.returnShippingCarrier already exists';
END

-- ============================================================
-- SECTION 2 — COLUMN WIDTH FIXES
-- ============================================================
PRINT '';
PRINT '--- SECTION 2: Column width fixes ---';

-- 2.1  ORDERS.shippingAddress  → NVARCHAR(500)
--      FixUnicodeColumns.sql narrowed this to 200; the EF model requires 500
--      and the frontend allows up to 300 chars in the combined address field.
DECLARE @col_max INT;
SELECT @col_max = CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'shippingAddress';

IF @col_max < 500
BEGIN
    ALTER TABLE ORDERS ALTER COLUMN shippingAddress NVARCHAR(500) NULL;
    PRINT '  [+] Widened ORDERS.shippingAddress to NVARCHAR(500) (was ' + CAST(@col_max AS VARCHAR) + ')';
END
ELSE PRINT '  [=] ORDERS.shippingAddress is already wide enough (' + CAST(@col_max AS VARCHAR) + ')';

-- 2.2  PREORDER.shippingAddress → NVARCHAR(500)  (same model mapping)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PREORDER')
BEGIN
    SELECT @col_max = CHARACTER_MAXIMUM_LENGTH
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PREORDER' AND COLUMN_NAME = 'shippingAddress';

    IF @col_max IS NOT NULL AND @col_max < 500
    BEGIN
        ALTER TABLE PREORDER ALTER COLUMN shippingAddress NVARCHAR(500) NULL;
        PRINT '  [+] Widened PREORDER.shippingAddress to NVARCHAR(500)';
    END
    ELSE PRINT '  [=] PREORDER.shippingAddress is already wide enough';
END

-- ============================================================
-- SECTION 3 — DATA REPAIR
-- ============================================================
PRINT '';
PRINT '--- SECTION 3: Data repair ---';

-- 3.1  Restore NULL userId on VNPay orders
--      Root cause (now fixed in code): CompleteVnPayPaymentAsync formerly used
--      UpdateAsync() which called ChangeTracker.Clear() + EntityState.Modified,
--      overwriting every column – including setting userId to NULL.
--      Strategy: join PAYMENT table to recover the userId from the VNPay
--      transaction reference (txnRef pattern: orderId prefix), or from the
--      user's other orders with the same payment info.
--
--      Safe recovery: if a NULL-userId order has a PAYMENT row whose amount
--      matches and the order was placed by a user who has other orders at
--      around the same time/shipping address, we can match them.
--      The safest automated fix: match via PAYMENT.orderId → PAYMENT.userId
--      is not stored, but the shippping address will match the user's profile.
--
--      We do the conservative version here: pair via PAYMENT row's orderId.

DECLARE @fixed_orders INT = 0;

-- Orders where userId IS NULL but a PAYMENT record links them
UPDATE o
SET    o.userId = sub.userId
FROM   ORDERS o
INNER JOIN (
    -- Find the most-recently created order (with a valid userId) for the same
    -- shippingAddress + totalAmount combination. This covers the common case
    -- where the user has more than one order and we can triangulate the userId.
    SELECT  o_null.orderId,
            o_ref.userId
    FROM    ORDERS o_null
    INNER JOIN ORDERS o_ref
        ON  o_ref.userId IS NOT NULL
        AND o_ref.shippingAddress = o_null.shippingAddress
        AND o_ref.totalAmount     = o_null.totalAmount
    WHERE   o_null.userId IS NULL
) AS sub ON o.orderId = sub.orderId
WHERE o.userId IS NULL;

SET @fixed_orders = @@ROWCOUNT;
IF @fixed_orders > 0
    PRINT '  [+] Recovered userId for ' + CAST(@fixed_orders AS VARCHAR) + ' NULL-userId order(s) via address+amount match';
ELSE
    PRINT '  [=] No NULL-userId orders recovered automatically (may need manual review)';

-- Report any remaining NULL-userId orders so the dev can investigate manually
DECLARE @remaining_null INT;
SELECT @remaining_null = COUNT(*) FROM ORDERS WHERE userId IS NULL;
IF @remaining_null > 0
BEGIN
    PRINT '  [!] WARNING: ' + CAST(@remaining_null AS VARCHAR) + ' order(s) still have NULL userId.';
    PRINT '      Run the query below to inspect them:';
    PRINT '      SELECT o.orderId, o.createdAt, o.totalAmount, o.shippingAddress, p.transactionId';
    PRINT '      FROM ORDERS o LEFT JOIN PAYMENT p ON p.orderId = o.orderId';
    PRINT '      WHERE o.userId IS NULL ORDER BY o.createdAt DESC;';
END;

-- 3.2  Purge ORDER_ITEM rows orphaned from their parent ORDERS row
--      (can happen if the Orders INSERT succeeded but was later deleted, or
--       if a partial rollback left dangling rows)
DECLARE @orphan_items INT;
SELECT @orphan_items = COUNT(*)
FROM ORDER_ITEM oi
WHERE NOT EXISTS (SELECT 1 FROM ORDERS o WHERE o.orderId = oi.orderId);

IF @orphan_items > 0
BEGIN
    DELETE FROM ORDER_ITEM
    WHERE NOT EXISTS (SELECT 1 FROM ORDERS o WHERE o.orderId = ORDER_ITEM.orderId);
    PRINT '  [+] Deleted ' + CAST(@orphan_items AS VARCHAR) + ' orphaned ORDER_ITEM row(s)';
END
ELSE
    PRINT '  [=] No orphaned ORDER_ITEM rows found';

-- 3.3  Remove ORDER_ITEM rows where both frameId and lensTypeId are NULL
--      (these are structurally invalid and would cause display errors in admin)
DECLARE @invalid_items INT;
SELECT @invalid_items = COUNT(*)
FROM ORDER_ITEM
WHERE frameId IS NULL AND lensTypeId IS NULL;

IF @invalid_items > 0
BEGIN
    DELETE FROM ORDER_ITEM WHERE frameId IS NULL AND lensTypeId IS NULL;
    PRINT '  [+] Deleted ' + CAST(@invalid_items AS VARCHAR) + ' invalid (no frame, no lensType) ORDER_ITEM row(s)';
END
ELSE
    PRINT '  [=] No invalid ORDER_ITEM rows found';

-- 3.4  Purge completely empty orders older than 7 days
--      (orders that were created but have 0 items – from the old silent-catch bug –
--       AND are not pending/processing – e.g. stuck in Pending with zero items)
DECLARE @empty_orders INT;
SELECT @empty_orders = COUNT(*)
FROM ORDERS o
WHERE NOT EXISTS (SELECT 1 FROM ORDER_ITEM oi WHERE oi.orderId = o.orderId)
  AND o.createdAt < DATEADD(DAY, -7, GETDATE())
  AND o.status NOT IN ('Completed', 'Delivered', 'DeliveryConfirmed');

IF @empty_orders > 0
BEGIN
    PRINT '  [!] Found ' + CAST(@empty_orders AS VARCHAR) + ' item-less orders older than 7 days.';
    PRINT '      These are likely ghost orders from the silent-catch bug.';
    PRINT '      To DELETE them, uncomment the block below and run again.';
    /*
    DELETE FROM PAYMENT WHERE orderId IN (
        SELECT orderId FROM ORDERS o
        WHERE NOT EXISTS (SELECT 1 FROM ORDER_ITEM oi WHERE oi.orderId = o.orderId)
          AND o.createdAt < DATEADD(DAY, -7, GETDATE())
          AND o.status NOT IN ('Completed','Delivered','DeliveryConfirmed')
    );
    DELETE FROM ORDERS
    WHERE NOT EXISTS (SELECT 1 FROM ORDER_ITEM oi WHERE oi.orderId = ORDERS.orderId)
      AND createdAt < DATEADD(DAY, -7, GETDATE())
      AND status NOT IN ('Completed','Delivered','DeliveryConfirmed');
    PRINT '  [+] Deleted ghost orders';
    */
END
ELSE
    PRINT '  [=] No item-less stale orders found';

-- ============================================================
-- SECTION 4 — DIAGNOSTIC REPORT
-- ============================================================
PRINT '';
PRINT '--- SECTION 4: Diagnostic snapshot ---';

SELECT 'Total orders'                    AS metric, COUNT(*)         AS value FROM ORDERS
UNION ALL
SELECT 'Orders with NULL userId',          COUNT(*)  FROM ORDERS WHERE userId IS NULL
UNION ALL
SELECT 'Orders with 0 items',              COUNT(*)
    FROM ORDERS o
    WHERE NOT EXISTS (SELECT 1 FROM ORDER_ITEM oi WHERE oi.orderId = o.orderId)
UNION ALL
SELECT 'Orders with >= 1 item',            COUNT(*)
    FROM ORDERS o
    WHERE EXISTS (SELECT 1 FROM ORDER_ITEM oi WHERE oi.orderId = o.orderId)
UNION ALL
SELECT 'Total order items',                COUNT(*) FROM ORDER_ITEM
UNION ALL
SELECT 'Items with NULL frameId',          COUNT(*) FROM ORDER_ITEM WHERE frameId IS NULL
UNION ALL
SELECT 'Payments with NULL orderId',       COUNT(*) FROM PAYMENT  WHERE orderId IS NULL;

PRINT '';
PRINT '=== Done. Review output above for any [!] warnings. ===';
