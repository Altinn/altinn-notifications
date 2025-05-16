CREATE TABLE IF NOT EXISTS notifications.statusfeed
(
  _id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  orderid BIGINT NOT NULL,
  creatorname TEXT NOT NULL,
  created TIMESTAMPTZ NOT NULL,
  orderstatus JSONB NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_statusfeed_orderid ON notifications.statusfeed(orderid);
CREATE INDEX IF NOT EXISTS idx_statusfeed_creatorname ON notifications.statusfeed (creatorname);

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE notifications.statusfeed TO platform_notifications;
GRANT USAGE, SELECT ON SEQUENCE notifications.statusfeed__id_seq TO platform_notifications;
