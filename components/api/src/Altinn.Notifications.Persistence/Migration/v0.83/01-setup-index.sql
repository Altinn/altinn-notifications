-- Support efficient lookups of all orders belonging to a given chain
CREATE INDEX IF NOT EXISTS idx_orders_orderchainid
    ON notifications.orders (_orderchainid);
