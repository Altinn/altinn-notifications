-- Add a nullable foreign key from orders back to the orderschain entry that owns it.
-- Follows the same pattern as emailnotifications._orderid -> orders._id.
-- Not all orders belong to a chain, so the column is nullable.
ALTER TABLE notifications.orders
    ADD COLUMN IF NOT EXISTS _orderchainid BIGINT NULL
        REFERENCES notifications.orderschain(_id);
