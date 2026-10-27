CREATE OR REPLACE FUNCTION notifications.getstatusfeed_v2(
    _sequencenumber BIGINT,
    _creatorname TEXT,
    _order TEXT,
    _limit INTEGER
)
RETURNS TABLE(_id BIGINT, orderstatus jsonb) AS $$
BEGIN
    /*
     * This function retrieves recent status feed entries for a specific creator.
     *
     * PARAMETERS:
     * _sequencenumber: Cursor for pagination.
     *                  asc:  returns rows where _id > _sequencenumber (forward pagination).
     *                  desc: returns rows where _id < _sequencenumber (backward/tail pagination),
     *                        unless _sequencenumber = 0, in which case no lower bound is applied
     *                        and the most recent entries are returned.
     * _creatorname:    The name of the creator to filter by.
     * _order:          The sort order for results. Must be 'asc' or 'desc'.
     * _limit:          The maximum number of rows to return.
     *
     * RETURNS:
     * A table with two columns: _id (BIGINT) and orderstatus (JSONB).
     */
    IF _order IS NULL OR _order NOT IN ('asc', 'desc') THEN
        RAISE EXCEPTION 'Invalid value for _order: %. Must be ''asc'' or ''desc''.', _order;
    END IF;

    RETURN QUERY
    SELECT
        sf._id,
        sf.orderstatus
    FROM
        notifications.statusfeed AS sf
    WHERE
        (
            (_order = 'asc' AND sf._id > _sequencenumber)
            OR (_order = 'desc' AND (_sequencenumber = 0 OR sf._id < _sequencenumber))
        )
        AND sf.creatorname = _creatorname
        AND sf.created < (NOW() - INTERVAL '2 seconds')
    ORDER BY
        CASE WHEN _order = 'asc' THEN sf._id END ASC,
        CASE WHEN _order = 'desc' THEN sf._id END DESC
    LIMIT
        _limit;
END;
$$ LANGUAGE plpgsql SECURITY INVOKER;

COMMENT ON FUNCTION notifications.getstatusfeed_v2(BIGINT, TEXT, TEXT, INTEGER) IS 'Retrieves a limited number of statusfeed entries created more than 2 seconds ago for a specific creator, starting after a given sequence number, ordered by sequence number in the specified direction.';
