-- Creates or replaces a function to delete statusfeed records older than 90 days
CREATE OR REPLACE FUNCTION notifications.delete_old_status_feed_records()
RETURNS bigint -- Returns the number of rows deleted
LANGUAGE 'plpgsql'
VOLATILE
AS $$
DECLARE
    -- Variable to hold the count of deleted rows
    deleted_count bigint;
BEGIN
    -- The DELETE operation is performed within a Common Table Expression (CTE)
    -- to capture the deleted rows using the RETURNING clause.
    WITH deleted_rows AS (
        DELETE FROM notifications.statusfeed 
        WHERE created <= NOW() - INTERVAL '90 days'
        RETURNING * -- Returns all columns of the deleted rows
    )
    -- Count the rows that were captured in the CTE
    SELECT count(*) INTO deleted_count FROM deleted_rows;

    -- Return the final count
    RETURN deleted_count;
END;
$$;

-- Add a comment to describe the function's purpose
COMMENT ON FUNCTION notifications.delete_old_status_feed_records() IS
'Deletes records from notifications.statusfeed where the "created" timestamp is 90 days or older. Returns the count of deleted records.';
