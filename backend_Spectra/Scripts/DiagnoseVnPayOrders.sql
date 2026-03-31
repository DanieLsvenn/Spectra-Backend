-- ============================================================
-- Repair VNPay orders with NULL userId
-- Root cause: CompleteVnPayPaymentAsync used UpdateAsync which
-- calls ChangeTracker.Clear() + EntityState.Modified, wiping
-- userId to NULL on every VNPay order status update.
-- ============================================================

-- 1. See all orders with NULL userId
SELECT orderId, userId, status, shippingAddress, totalAmount, createdAt
FROM ORDERS
WHERE userId IS NULL
ORDER BY createdAt DESC;

-- 2. For orders WITH [Name - Phone - Email] prefix: recover userId via email match
-- DRY RUN first — see what would be repaired
SELECT 
    o.orderId,
    o.status,
    LEFT(o.shippingAddress, 80) AS addressPreview,
    LTRIM(RTRIM(
        SUBSTRING(
            o.shippingAddress,
            CHARINDEX(' - ', o.shippingAddress, CHARINDEX(' - ', o.shippingAddress) + 3) + 3,
            CHARINDEX(']', o.shippingAddress) 
            - (CHARINDEX(' - ', o.shippingAddress, CHARINDEX(' - ', o.shippingAddress) + 3) + 3)
        )
    )) AS extractedEmail,
    u.userId AS matchedUserId,
    u.fullName AS matchedUserName
FROM ORDERS o
INNER JOIN [USER] u ON u.email = 
    LTRIM(RTRIM(
        SUBSTRING(
            o.shippingAddress,
            CHARINDEX(' - ', o.shippingAddress, CHARINDEX(' - ', o.shippingAddress) + 3) + 3,
            CHARINDEX(']', o.shippingAddress) 
            - (CHARINDEX(' - ', o.shippingAddress, CHARINDEX(' - ', o.shippingAddress) + 3) + 3)
        )
    ))
WHERE o.userId IS NULL
  AND o.shippingAddress LIKE '[[]%-%-%]%';

-- 3. REPAIR orders that have [Name - Phone - Email] prefix
--    Uncomment to execute:
/*
UPDATE o
SET o.userId = u.userId
FROM ORDERS o
INNER JOIN [USER] u ON u.email = 
    LTRIM(RTRIM(
        SUBSTRING(
            o.shippingAddress,
            CHARINDEX(' - ', o.shippingAddress, CHARINDEX(' - ', o.shippingAddress) + 3) + 3,
            CHARINDEX(']', o.shippingAddress) 
            - (CHARINDEX(' - ', o.shippingAddress, CHARINDEX(' - ', o.shippingAddress) + 3) + 3)
        )
    ))
WHERE o.userId IS NULL
  AND o.shippingAddress LIKE '[[]%-%-%]%';
*/

-- 4. For orders WITHOUT the prefix (old test orders):
--    Match by looking at PAYMENT table to find who created the payment,
--    or just assign to the known test user.
--    First check which user created most VNPay payments:
SELECT u.userId, u.fullName, u.email, COUNT(*) AS paymentCount
FROM PAYMENT p
JOIN ORDERS o ON o.orderId = p.orderId
JOIN [USER] u ON u.email = 'seizurebabe@gmail.com'
WHERE p.paymentMethod = 'vnpay'
  AND o.userId IS NULL
  AND o.shippingAddress NOT LIKE '[[]%-%-%]%'
GROUP BY u.userId, u.fullName, u.email;

-- 5. REPAIR remaining NULL-userId orders that belong to the test user
--    (all share the same "25 duong 5..." address pattern)
--    Uncomment to execute:
/*
UPDATE ORDERS
SET userId = (SELECT TOP 1 userId FROM [USER] WHERE email = 'seizurebabe@gmail.com')
WHERE userId IS NULL
  AND shippingAddress LIKE '%25 %ng 5%khu d%n c% %ng D%ng%';
*/
