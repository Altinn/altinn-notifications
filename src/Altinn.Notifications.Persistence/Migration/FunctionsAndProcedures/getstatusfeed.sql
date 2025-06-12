CREATE OR REPLACE FUNCTION notifications.getstatusfeed(
    _sequencenumber BIGINT,
    _creatorname TEXT,
    _limit INTEGER
)
RETURNS TABLE(_id BIGINT, orderstatus jsonb) AS $$
BEGIN
    /*
     * This function retrieves recent status feed entries for a specific creator.
     *
     * PARAMETERS:
     * _sequencenumber: The ID to look after. The function will return rows where _id > this value.
     * _creator_name:   The name of the creator to filter by.
     * _limit:          The maximum number of rows to return.
     *
     * RETURNS:
     * A table with two columns: _id (BIGINT) and orderstatus (TEXT).
     */
    RETURN QUERY
    SELECT
        sf._id,
        sf.orderstatus
    FROM
        notifications.statusfeed AS sf
    WHERE
        sf._id > _sequencenumber
        AND sf.creatorname = _creatorname
        AND sf.created < (NOW() - INTERVAL '2 seconds')
    ORDER BY
        sf._id ASC
    LIMIT
        _limit;
END;
$$ LANGUAGE plpgsql SECURITY INVOKER;

COMMENT ON FUNCTION notifications.getstatusfeed(BIGINT, TEXT, INTEGER) IS 'Retrieves a limited number of statusfeed entries created more than 2 seconds ago for a specific creator, starting after a given sequence number.';
