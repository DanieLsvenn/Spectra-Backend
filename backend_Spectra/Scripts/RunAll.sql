-- =====================================================
-- RUN ALL: Execute this single script to apply ALL
-- required database changes for the Spectra app.
-- Safe to run multiple times (all use IF NOT EXISTS).
-- =====================================================

-- 1. Add EstimatedDeliveryDate to ORDERS
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'estimatedDeliveryDate'
)
BEGIN
    ALTER TABLE [ORDERS] ADD [estimatedDeliveryDate] DATETIME NULL;
    PRINT 'Added estimatedDeliveryDate to ORDERS';
END
GO

-- 2. Add DeliveryConfirmedAt to ORDERS
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'deliveryConfirmedAt'
)
BEGIN
    ALTER TABLE ORDERS ADD deliveryConfirmedAt DATETIME NULL;
    PRINT 'Added deliveryConfirmedAt to ORDERS';
END
GO

-- 3. Add CancelledByCustomer to ORDERS
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'cancelledByCustomer'
)
BEGIN
    ALTER TABLE ORDERS ADD cancelledByCustomer BIT NULL;
    PRINT 'Added cancelledByCustomer to ORDERS';
END
GO

-- 4. Add Notes to ORDERS
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ORDERS' AND COLUMN_NAME = 'notes'
)
BEGIN
    ALTER TABLE ORDERS ADD notes NVARCHAR(MAX) NULL;
    PRINT 'Added notes to ORDERS';
END
GO

-- 5. Add CancelledByCustomer to COMPLAINT_REQUEST
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'COMPLAINT_REQUEST' AND COLUMN_NAME = 'cancelledByCustomer'
)
BEGIN
    ALTER TABLE COMPLAINT_REQUEST ADD cancelledByCustomer BIT NULL;
    PRINT 'Added cancelledByCustomer to COMPLAINT_REQUEST';
END
GO

-- 6. Add ReturnShippingCarrier to COMPLAINT_REQUEST
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'COMPLAINT_REQUEST' AND COLUMN_NAME = 'returnShippingCarrier'
)
BEGIN
    ALTER TABLE COMPLAINT_REQUEST ADD returnShippingCarrier NVARCHAR(100) NULL;
    PRINT 'Added returnShippingCarrier to COMPLAINT_REQUEST';
END
GO

-- 7. Create BusinessRule table + seed data
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BusinessRule')
BEGIN
    CREATE TABLE BusinessRule (
        RuleId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        RuleKey NVARCHAR(100) NOT NULL UNIQUE,
        RuleValue NVARCHAR(500) NOT NULL,
        Description NVARCHAR(500) NULL,
        Category NVARCHAR(100) NOT NULL DEFAULT N'general',
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedBy NVARCHAR(200) NULL
    );

    INSERT INTO BusinessRule (RuleKey, RuleValue, Description, Category) VALUES
    (N'shipping.base_fee_vnd', N'20000', N'Phí cơ bản cho 3km đầu (VND)', N'shipping'),
    (N'shipping.base_distance_km', N'3', N'Quãng đường cơ bản tính phí (km)', N'shipping'),
    (N'shipping.per_km_fee_vnd', N'5000', N'Phí mỗi km vượt quãng đường cơ bản (VND)', N'shipping'),
    (N'shipping.max_fee_vnd', N'150000', N'Phí vận chuyển tối đa (VND)', N'shipping'),
    (N'shipping.free_threshold_vnd', N'1500000', N'Miễn phí vận chuyển cho đơn hàng trên (VND)', N'shipping'),
    (N'shipping.express_multiplier', N'1.5', N'Hệ số nhân phí giao nhanh', N'shipping'),
    (N'complaint.time_limit_days', N'7', N'Số ngày cho phép khiếu nại sau khi giao hàng', N'complaint'),
    (N'exchange_rate.usd_to_vnd', N'25400', N'Tỷ giá USD sang VND', N'exchange_rate');

    PRINT 'Created BusinessRule table with seed data';
END
GO

PRINT '=== ALL MIGRATIONS COMPLETE ===';
GO
