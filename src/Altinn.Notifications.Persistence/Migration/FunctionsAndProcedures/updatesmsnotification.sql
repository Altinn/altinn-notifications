CREATE OR REPLACE FUNCTION notifications.updatesmsnotification(
    _result text,
    _gatewayreference text,
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
        FROM notifications.smsnotifications n
        WHERE n.alternateid = _alternateid
        FOR UPDATE;

        v_found := FOUND;

    ELSIF _gatewayreference IS NOT NULL THEN
        -- Lock the row and fetch expiry time by gateway reference
        SELECT n.alternateid, n.expirytime INTO v_alternateid, v_expirytime
        FROM notifications.smsnotifications n
        WHERE n.gatewayreference = _gatewayreference
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
        UPDATE notifications.smsnotifications
        SET result = _result::smsnotificationresulttype,
            resulttime = now(),
            gatewayreference = COALESCE(_gatewayreference, gatewayreference)
        WHERE smsnotifications.alternateid = _alternateid;

        was_updated := true;

    ELSIF _gatewayreference IS NOT NULL THEN
        UPDATE notifications.smsnotifications
        SET result = _result::smsnotificationresulttype,
            resulttime = now()
        WHERE smsnotifications.gatewayreference = _gatewayreference;

        was_updated := true;
    END IF;

    RETURN QUERY SELECT v_alternateid, was_updated, is_expired;
END;
$$;

COMMENT ON FUNCTION notifications.updatesmsnotification IS
'Updates an SMS notification''s result and resulttime by alternateid or by gatewayreference, with expiry time validation.

Precedence: If both _alternateid and _gatewayreference are non-null, only alternateid is used for lookup; _gatewayreference may still populate the row via COALESCE.

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

Uniqueness assumptions: alternateid is unique (primary key); gatewayreference uniquely identifies at most one row when non-null.
Overwrite policy: result and resulttime are always overwritten when not expired; gatewayreference is only set when a non-null _gatewayreference is supplied (existing value preserved when _gatewayreference is null).';
