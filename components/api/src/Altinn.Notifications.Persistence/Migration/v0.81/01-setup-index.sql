-- Replace single-column creatorname index with a composite index on (creatorname, _id)
-- to efficiently support the getstatusfeed() query pattern:
--   WHERE creatorname = _creatorname AND _id > _sequencenumber ORDER BY _id ASC
--
-- NOTE: This script was applied manually in production using CONCURRENTLY to avoid
-- table locks on a live database. The commands actually executed were:
--
--   CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_statusfeed_creatorname_id
--       ON notifications.statusfeed (creatorname, _id);
--
--   DROP INDEX CONCURRENTLY IF EXISTS notifications.idx_statusfeed_creatorname;
--
-- This file exists as part of the migration history so that yuniql tracks the version,
-- and to ensure the index state is reflected when provisioning new environments (e.g. dev/test)
-- where CONCURRENTLY is not required.

CREATE INDEX IF NOT EXISTS idx_statusfeed_creatorname_id
    ON notifications.statusfeed (creatorname, _id);

DROP INDEX IF EXISTS notifications.idx_statusfeed_creatorname;
