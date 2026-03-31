-- =====================================================
-- Create BusinessRules table for configurable settings
-- =====================================================

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

    -- Shipping rules
    INSERT INTO BusinessRule (RuleKey, RuleValue, Description, Category) VALUES
    (N'shipping.base_fee_vnd', N'20000', N'Phí cơ bản cho 3km đầu (VND)', N'shipping'),
    (N'shipping.base_distance_km', N'3', N'Quãng đường cơ bản tính phí (km)', N'shipping'),
    (N'shipping.per_km_fee_vnd', N'5000', N'Phí mỗi km vượt quãng đường cơ bản (VND)', N'shipping'),
    (N'shipping.max_fee_vnd', N'150000', N'Phí vận chuyển tối đa (VND)', N'shipping'),
    (N'shipping.free_threshold_vnd', N'1500000', N'Miễn phí vận chuyển cho đơn hàng trên (VND)', N'shipping'),
    (N'shipping.express_multiplier', N'1.5', N'Hệ số nhân phí giao nhanh', N'shipping');

    -- Complaint rules
    INSERT INTO BusinessRule (RuleKey, RuleValue, Description, Category) VALUES
    (N'complaint.time_limit_days', N'7', N'Số ngày cho phép khiếu nại sau khi giao hàng', N'complaint');

    -- Exchange rate
    INSERT INTO BusinessRule (RuleKey, RuleValue, Description, Category) VALUES
    (N'exchange.usd_vnd_fallback', N'25400', N'Tỷ giá USD/VND dự phòng', N'exchange');

    PRINT 'BusinessRule table created and seeded successfully.';
END
ELSE
BEGIN
    PRINT 'BusinessRule table already exists.';
END
