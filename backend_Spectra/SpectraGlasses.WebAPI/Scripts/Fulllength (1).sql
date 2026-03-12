-- =========================
-- CREATE DATABASE
-- =========================
IF DB_ID('GlassesECommerce') IS NULL
    CREATE DATABASE GlassesECommerce;
GO

USE GlassesECommerce;
GO

-- =========================
-- USER
-- =========================
CREATE TABLE [USER] (
    userId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    fullName VARCHAR(100),
    email VARCHAR(150) UNIQUE NOT NULL,
    phone VARCHAR(20),
    address VARCHAR(200),
    role VARCHAR(50),
    passwordHash VARCHAR(255),
    status VARCHAR(20),
    createdAt DATETIME DEFAULT GETDATE()
);

-- =========================
-- ORDERS
-- =========================
CREATE TABLE ORDERS (
    orderId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    userId UNIQUEIDENTIFIER,
    totalAmount FLOAT,
    shippingAddress VARCHAR(200),
    arrivalDate DATETIME,
    status VARCHAR(50),
    createdAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (userId) REFERENCES [USER](userId)
);

-- =========================
-- PRESCRIPTION
-- =========================
CREATE TABLE PRESCRIPTION (
    prescriptionId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    userId UNIQUEIDENTIFIER,
    sphereLeft FLOAT,
    sphereRight FLOAT,
    cylinderLeft FLOAT,
    cylinderRight FLOAT,
    axisLeft INT,
    axisRight INT,
    addLeft FLOAT,
    addRight FLOAT,
    pupillaryDistance INT,
    doctorName VARCHAR(100),
    clinicName VARCHAR(100),
    expirationDate DATETIME,
    createdAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (userId) REFERENCES [USER](userId)
);

-- =========================
-- FRAME
-- =========================
CREATE TABLE FRAME (
    frameId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    frameName VARCHAR(100),
    brand VARCHAR(50),
    color VARCHAR(50),
    material VARCHAR(50),
    lensWidth INT,
    bridgeWidth INT,
    frameWidth INT,
    templeLength INT,
    shape VARCHAR(50),
    size VARCHAR(50),
    basePrice FLOAT,
    status VARCHAR(50)
);

-- =========================
-- FRAME_MEDIA
-- =========================
CREATE TABLE FRAME_MEDIA (
    mediaId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    frameId UNIQUEIDENTIFIER,
    mediaUrl VARCHAR(2048),
    mediaType VARCHAR(50),
    FOREIGN KEY (frameId) REFERENCES FRAME(frameId)
);

-- =========================
-- LENS_FEATURE
-- =========================
CREATE TABLE LENS_FEATURE (
    featureId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    lensIndex FLOAT,
    featureSpecification VARCHAR(200),
    extraPrice FLOAT
);

-- =========================
-- LENS_TYPE
-- =========================
CREATE TABLE LENS_TYPE (
    lensTypeId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    lensSpecification VARCHAR(200),
    requiresPrescription BIT,
    extraPrice FLOAT
);

-- =========================
-- ORDER_ITEM
-- =========================
CREATE TABLE ORDER_ITEM (
    orderItemId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    orderId UNIQUEIDENTIFIER,
    prescriptionId UNIQUEIDENTIFIER,
    frameId UNIQUEIDENTIFIER,
    featureId UNIQUEIDENTIFIER,
    lensTypeId UNIQUEIDENTIFIER,
    quantity INT,
    orderPrice FLOAT,
    FOREIGN KEY (orderId) REFERENCES ORDERS(orderId),
    FOREIGN KEY (prescriptionId) REFERENCES PRESCRIPTION(prescriptionId),
    FOREIGN KEY (frameId) REFERENCES FRAME(frameId),
    FOREIGN KEY (featureId) REFERENCES LENS_FEATURE(featureId),
    FOREIGN KEY (lensTypeId) REFERENCES LENS_TYPE(lensTypeId)
);

-- =========================
-- PREORDER
-- =========================
CREATE TABLE PREORDER (
    preorderId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    userId UNIQUEIDENTIFIER,
    expectedDate DATETIME,
    status VARCHAR(50),
    createdAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (userId) REFERENCES [USER](userId)
);

-- =========================
-- PREORDER_ITEM
-- =========================
CREATE TABLE PREORDER_ITEM (
    preorderItemId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    preorderId UNIQUEIDENTIFIER,
    prescriptionId UNIQUEIDENTIFIER,
    frameId UNIQUEIDENTIFIER,
    featureId UNIQUEIDENTIFIER,
    lensTypeId UNIQUEIDENTIFIER,
    quantity INT,
    preorderPrice FLOAT,
    FOREIGN KEY (preorderId) REFERENCES PREORDER(preorderId),
    FOREIGN KEY (prescriptionId) REFERENCES PRESCRIPTION(prescriptionId),
    FOREIGN KEY (frameId) REFERENCES FRAME(frameId),
    FOREIGN KEY (featureId) REFERENCES LENS_FEATURE(featureId),
    FOREIGN KEY (lensTypeId) REFERENCES LENS_TYPE(lensTypeId)
);

-- =========================
-- PAYMENT
-- =========================
CREATE TABLE PAYMENT (
    paymentId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    orderId UNIQUEIDENTIFIER NULL,
    preorderId UNIQUEIDENTIFIER NULL,
    amount FLOAT,
    paymentMethod VARCHAR(100),
    paymentStatus VARCHAR(50),
    paidAt DATETIME,
    FOREIGN KEY (orderId) REFERENCES ORDERS(orderId),
    FOREIGN KEY (preorderId) REFERENCES PREORDER(preorderId)
);

-- =========================
-- COMPLAINT_REQUEST
-- =========================
CREATE TABLE COMPLAINT_REQUEST (
    requestId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    userId UNIQUEIDENTIFIER,
    orderItemId UNIQUEIDENTIFIER,
    requestType VARCHAR(100),
    reason VARCHAR(250),
    mediaUrl TEXT,
    status VARCHAR(50),
    createdAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (userId) REFERENCES [USER](userId),
    FOREIGN KEY (orderItemId) REFERENCES ORDER_ITEM(orderItemId)
);

ALTER TABLE FRAME
ALTER COLUMN color VARCHAR(50) NULL;

ALTER TABLE ORDER_ITEM
ADD selectedColor VARCHAR(50) NULL;

ALTER TABLE PREORDER_ITEM
ADD selectedColor VARCHAR(50) NULL;

INSERT INTO [USER] (userId, fullName, email, phone, address, role, passwordHash, status, createdAt)
VALUES
(NEWID(), 'Danny', 'a@mail.com', '090000001', '123 Hanoi', 'customer', 'hashA', 'active', GETDATE()),
(NEWID(), 'Ren', 'b@mail.com', '090000002', '123 HCM', 'customer', 'hashB', 'active', GETDATE()),
(NEWID(), 'Cally', 'c@mail.com', '090000003', '123 Da Nang', 'staff', 'hashC', 'active', GETDATE()),
(NEWID(), 'Josh', 'd@mail.com', '090000004', '123 Can Tho', 'manager', 'hashD', 'active', GETDATE()),
(NEWID(), 'Admin', 'admin@mail.com', '090000005', '123 Hanoi', 'admin', 'hashE', 'active', GETDATE());

INSERT INTO FRAME (
    frameId, frameName, brand, color, material,
    lensWidth, bridgeWidth, frameWidth, templeLength,
    shape, size, basePrice, status
)
VALUES
(NEWID(), 'Classic Metal', 'Rayban', 'Black', 'Metal', 52, 18, 140, 145, 'Square', 'M', 120, 'available'),
(NEWID(), 'Retro Round', 'Gucci', 'Gold', 'Metal', 50, 19, 138, 140, 'Round', 'S', 260, 'available'),
(NEWID(), 'Sport Flex', 'Oakley', NULL, 'Plastic', 56, 20, 145, 150, 'Rectangle', 'L', 180, 'available'),
(NEWID(), 'Minimal Edge', 'OwnBrand', NULL, 'Titanium', 51, 17, 139, 142, 'Oval', 'M', 95, 'available'),
(NEWID(), 'Vintage Acetate', 'Persol', 'Brown', 'Acetate', 53, 18, 140, 145, 'Square', 'M', 210, 'out_of_stock');

INSERT INTO LENS_FEATURE (featureId, lensIndex, featureSpecification, extraPrice)
VALUES
(NEWID(), 1.56, 'Anti-reflective coating', 15),
(NEWID(), 1.60, 'Blue light filter', 25),
(NEWID(), 1.67, 'Ultra thin lens', 40),
(NEWID(), 1.74, 'Super thin premium', 60),
(NEWID(), 1.56, 'UV protection', 10);

INSERT INTO LENS_TYPE (lensTypeId, lensSpecification, requiresPrescription, extraPrice)
VALUES
(NEWID(), 'Single vision', 1, 0),
(NEWID(), 'Progressive', 1, 80),
(NEWID(), 'Bifocal', 1, 50),
(NEWID(), 'Plano fashion lens', 0, 0),
(NEWID(), 'Office lens', 1, 60);

INSERT INTO PRESCRIPTION (
    prescriptionId, userId,
    sphereLeft, sphereRight,
    cylinderLeft, cylinderRight,
    axisLeft, axisRight,
    addLeft, addRight,
    pupillaryDistance,
    doctorName, clinicName,
    expirationDate, createdAt
)
SELECT TOP 5
    NEWID(), userId,
    -2.00, -2.50,
    -0.50, -0.75,
    90, 85,
    1.25, 1.25,
    62,
    'Dr. Nguyen', 'Vision Care Clinic',
    DATEADD(YEAR, 1, GETDATE()), GETDATE()
FROM [USER]
WHERE role = 'customer';

INSERT INTO ORDERS (
    orderId, userId, totalAmount,
    shippingAddress, arrivalDate,
    status, createdAt
)
SELECT TOP 5
    NEWID(), userId, 320,
    address, DATEADD(DAY, 7, GETDATE()),
    'processing', GETDATE()
FROM [USER]
WHERE role = 'customer';

INSERT INTO ORDER_ITEM (
    orderItemId, orderId, prescriptionId,
    frameId, featureId, lensTypeId,
    quantity, orderPrice, selectedColor
)
SELECT TOP 5
    NEWID(),
    o.orderId,
    p.prescriptionId,
    f.frameId,
    lf.featureId,
    lt.lensTypeId,
    1,
    f.basePrice + lf.extraPrice + lt.extraPrice,
    CASE
        WHEN f.color IS NULL THEN 'Matte Red'
        ELSE NULL
    END
FROM ORDERS o
JOIN PRESCRIPTION p ON o.userId = p.userId
CROSS JOIN FRAME f
CROSS JOIN LENS_FEATURE lf
CROSS JOIN LENS_TYPE lt;

INSERT INTO PREORDER (
    preorderId, userId,
    expectedDate, status, createdAt
)
SELECT TOP 5
    NEWID(), userId,
    DATEADD(DAY, 30, GETDATE()),
    'pending', GETDATE()
FROM [USER]
WHERE role = 'customer';

INSERT INTO PREORDER_ITEM (
    preorderItemId, preorderId,
    prescriptionId, frameId,
    featureId, lensTypeId,
    quantity, preorderPrice,
    selectedColor
)
SELECT TOP 5
    NEWID(),
    pr.preorderId,
    p.prescriptionId,
    f.frameId,
    lf.featureId,
    lt.lensTypeId,
    1,
    f.basePrice + lf.extraPrice + lt.extraPrice - 20,
    'Transparent Blue'
FROM PREORDER pr
JOIN PRESCRIPTION p ON pr.userId = p.userId
CROSS JOIN FRAME f
CROSS JOIN LENS_FEATURE lf
CROSS JOIN LENS_TYPE lt;

INSERT INTO PAYMENT (
    paymentId, orderId,
    amount, paymentMethod,
    paymentStatus, paidAt
)
SELECT TOP 5
    NEWID(),
    orderId,
    totalAmount,
    'card',
    'paid',
    GETDATE()
FROM ORDERS;

INSERT INTO COMPLAINT_REQUEST (
    requestId, userId,
    orderItemId, requestType,
    reason, mediaUrl,
    status, createdAt
)
SELECT TOP 5
    NEWID(),
    u.userId,
    oi.orderItemId,
    'exchange',
    'Color slightly different from expectation',
    'https://example.com/photo.jpg',
    'pending',
    GETDATE()
FROM [USER] u
JOIN ORDERS o ON u.userId = o.userId
JOIN ORDER_ITEM oi ON o.orderId = oi.orderId;
