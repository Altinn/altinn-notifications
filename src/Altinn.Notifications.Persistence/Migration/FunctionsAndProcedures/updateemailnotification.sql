CREATE OR REPLACE FUNCTION notifications.updateemailnotification(
    _result text,
    _operationid text,
    _alternateid uuid
)
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    v_alternateid uuid;
BEGIN
    IF _alternateid IS NOT NULL THEN
        UPDATE notifications.emailnotifications
        SET result = _result::emailnotificationresulttype,
            resulttime = now(),
            operationid = COALESCE(_operationid, operationid)
        WHERE alternateid = _alternateid
        RETURNING alternateid INTO v_alternateid;


    ELSIF _operationid IS NOT NULL THEN
        UPDATE notifications.emailnotifications
        SET result = _result::emailnotificationresulttype,
            resulttime = now()
        WHERE operationid = _operationid
        RETURNING alternateid INTO v_alternateid;
    END IF;

    RETURN v_alternateid; -- null => not found
END;
$$;

COMMENT ON FUNCTION notifications.updateemailnotification IS
'Updates an email notification''s result and resulttime by alternateid or by operationid.
Precedence: If both _alternateid and _operationid are non-null, only alternateid is used for lookup; _operationid may still populate the row via COALESCE.
Null return: NULL when neither identifier is provided OR no matching row exists (no update performed).
Uniqueness assumptions: alternateid is unique (primary key); operationid uniquely identifies at most one row when non-null.
Overwrite policy: result and resulttime are always overwritten (no transition guarding); operationid is only set when a non-null _operationid is supplied (existing value preserved when _operationid is null).';

-- New v2 function with expiry time validation
CREATE OR REPLACE FUNCTION notifications.updateemailnotification_v2(
    _result text,
    _operationid text,
    _alternateid uuid
)
RETURNS TABLE (
    alternateid uuid,
    was_updated boolean,
    is_expired boolean
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_alternateid uuid;
    v_expirytime timestamptz;
    v_found boolean;
BEGIN
    -- Initialize flags
    was_updated := false;
    is_expired := false;

    -- Determine which identifier to use
    IF _alternateid IS NOT NULL THEN
        -- Lock the row and fetch expiry time
        SELECT n.alternateid, n.expirytime INTO v_alternateid, v_expirytime
        FROM notifications.emailnotifications n
        WHERE n.alternateid = _alternateid
        FOR UPDATE;

        v_found := FOUND;

    ELSIF _operationid IS NOT NULL THEN
        -- Lock the row and fetch expiry time by operation ID
        SELECT n.alternateid, n.expirytime INTO v_alternateid, v_expirytime
        FROM notifications.emailnotifications n
        WHERE n.operationid = _operationid
        FOR UPDATE;

        v_found := FOUND;
    ELSE
        -- Neither identifier provided
        RETURN QUERY SELECT NULL::uuid, false, false;
        RETURN;
    END IF;

    -- Check if notification was found
    IF NOT v_found THEN
        -- Not found
        RETURN QUERY SELECT NULL::uuid, false, false;
        RETURN;
    END IF;

    -- Check if notification has expired
    IF v_expirytime <= now() THEN
        -- Expired - don't update
        RETURN QUERY SELECT v_alternateid, false, true;
        RETURN;
    END IF;

    -- Not expired - proceed with update
    IF _alternateid IS NOT NULL THEN
        UPDATE notifications.emailnotifications
        SET result = _result::emailnotificationresulttype,
            resulttime = now(),
            operationid = COALESCE(_operationid, operationid)
        WHERE emailnotifications.alternateid = _alternateid;

        was_updated := true;

    ELSIF _operationid IS NOT NULL THEN
        UPDATE notifications.emailnotifications
        SET result = _result::emailnotificationresulttype,
            resulttime = now()
        WHERE emailnotifications.operationid = _operationid;

        was_updated := true;
    END IF;

    RETURN QUERY SELECT v_alternateid, was_updated, is_expired;
END;
$$;

COMMENT ON FUNCTION notifications.updateemailnotification_v2 IS
'Updates an email notification''s result and resulttime by alternateid or by operationid, with expiry time validation.

Precedence: If both _alternateid and _operationid are non-null, only alternateid is used for lookup; _operationid may still populate the row via COALESCE.

Return values:
- alternateid: The UUID of the notification (NULL if not found)
- was_updated: true if the update was performed, false otherwise
- is_expired: true if the notification has passed its expiry time (expirytime <= now())

Behavior:
- Uses SELECT ... FOR UPDATE to lock the row and prevent race conditions
- Checks expirytime <= now() to determine if notification has expired
- If expired: returns (alternateid, false, true) without updating
- If not found: returns (NULL, false, false)
- If found and not expired: performs update and returns (alternateid, true, false)

Uniqueness assumptions: alternateid is unique (primary key); operationid uniquely identifies at most one row when non-null.
Overwrite policy: result and resulttime are always overwritten when not expired; operationid is only set when a non-null _operationid is supplied (existing value preserved when _operationid is null).';