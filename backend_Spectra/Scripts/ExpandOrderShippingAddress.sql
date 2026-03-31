-- Expand ORDERS.shippingAddress from nvarchar(200) to nvarchar(500)
-- to match PREORDER table and accommodate [Name - Phone - Email] prefix + structured address data
ALTER TABLE ORDERS ALTER COLUMN shippingAddress NVARCHAR(500);
