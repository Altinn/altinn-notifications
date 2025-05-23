CREATE TABLE IF NOT EXISTS notifications.statusfeed
(
  _id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  sequencenumber BIGINT NOT NULL,
  orderid BIGINT NOT NULL,
  creatorname TEXT NOT NULL,
  created TIMESTAMPTZ NOT NULL,
  orderstatus JSONB NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_statusfeed_orderid ON notifications.statusfeed(orderid);
CREATE INDEX IF NOT EXISTS idx_statusfeed_creatorname ON notifications.statusfeed (creatorname);
CREATE UNIQUE INDEX IF NOT EXISTS idx_unique_sequencenumber_creatorname ON notifications.statusfeed (sequencenumber, creatorname);
CREATE INDEX IF NOT EXISTS idx_statusfeed_created ON notifications.statusfeed (created);
CREATE INDEX IF NOT EXISTS idx_statusfeed_creatorname_created  ON notifications.statusfeed (creatorname, created DESC);

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE notifications.statusfeed TO platform_notifications;
GRANT USAGE, SELECT ON SEQUENCE notifications.statusfeed__id_seq TO platform_notifications;
