-- Creates or replaces a function to delete statusfeed records older than 90 days
CREATE OR REPLACE FUNCTION notifications.delete_old_status_feed_records()
RETURNS bigint -- Returns the number of rows deleted
LANGUAGE 'plpgsql'
VOLATILE
AS $$
DECLARE
    -- Variable to hold the count of deleted rows
    deleted_count bigint := 0;
    -- Variable to hold the lock acquisition status
    lock_acquired boolean;
    -- Generate lock ID from function name using hashtext
    lock_id bigint := hashtext('notifications.delete_old_status_feed_records');
BEGIN
    -- Acquire a advisory lock to prevent concurrent cleanup operations
    SELECT pg_try_advisory_lock(lock_id) INTO lock_acquired;
    
    IF NOT lock_acquired THEN
        RETURN 0; -- Another cleanup is running
    END IF;
    
    WITH deleted_rows AS (
        DELETE FROM notifications.statusfeed 
        WHERE created <= NOW() - INTERVAL '90 days'
        RETURNING _id
    )
    -- Count the rows that were captured in the CTE
    SELECT count(*) INTO deleted_count FROM deleted_rows;
    
    PERFORM pg_advisory_unlock(lock_id);
    
    RETURN deleted_count;
END;
$$;

-- Add a comment to describe the function's purpose
COMMENT ON FUNCTION notifications.delete_old_status_feed_records() IS
'Deletes records from notifications.statusfeed where the "created" timestamp is 90 days or older. Returns the count of deleted records. Uses an advisory lock to prevent concurrent executions.';

-- Creates a new version of the function that accepts a batch_size parameter,
-- deleting at most batch_size rows per invocation. Intended to be called
-- repeatedly by a scheduled cron job instead of deleting all rows in one transaction.
CREATE OR REPLACE FUNCTION notifications.deleteoldstatusfeedrecords_v2(batch_size int DEFAULT 10000)
RETURNS bigint -- Returns the number of rows deleted in this invocation
LANGUAGE 'plpgsql'
VOLATILE
AS $$
DECLARE
    -- Variable to hold the count of deleted rows
    deleted_count bigint := 0;
    -- Variable to hold the lock acquisition status
    lock_acquired boolean;
    -- Generate lock ID from function name using hashtext
    lock_id bigint := hashtext('notifications.deleteoldstatusfeedrecords_v2');
BEGIN
    -- Acquire an advisory lock to prevent concurrent cleanup operations
    SELECT pg_try_advisory_lock(lock_id) INTO lock_acquired;

    IF NOT lock_acquired THEN
        RETURN 0; -- Another cleanup is running
    END IF;

    WITH deleted_rows AS (
        DELETE FROM notifications.statusfeed
        WHERE _id IN (
            SELECT _id FROM notifications.statusfeed
            WHERE created <= NOW() - INTERVAL '90 days'
            LIMIT batch_size
        )
        RETURNING _id
    )
    -- Count the rows that were captured in the CTE
    SELECT count(*) INTO deleted_count FROM deleted_rows;

    PERFORM pg_advisory_unlock(lock_id);

    RETURN deleted_count;
END;
$$;

-- Add a comment to describe the function's purpose
COMMENT ON FUNCTION notifications.deleteoldstatusfeedrecords_v2(int) IS
'Deletes up to batch_size records from notifications.statusfeed where the "created" timestamp is 90 days or older. Returns the count of deleted records. Uses an advisory lock to prevent concurrent executions. Intended to be called repeatedly by a scheduled cron job.';
