-- Keep the old function for backwards compatibility
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
    v_was_updated boolean := false;
    v_is_expired boolean := false;
BEGIN
    -- Early return if no identifier provided
    IF _alternateid IS NULL AND _operationid IS NULL THEN
        RETURN QUERY SELECT NULL::uuid, false, false;
        RETURN;
    END IF;

    -- Single UPDATE with conditional logic based on expiry time
    WITH update_attempt AS (
        UPDATE notifications.emailnotifications
        SET
            result = CASE
                WHEN expirytime > now() THEN _result::emailnotificationresulttype
                ELSE result
            END,
            resulttime = CASE
                WHEN expirytime > now() THEN now()
                ELSE resulttime
            END,
            operationid = CASE
                WHEN expirytime > now() AND _alternateid IS NOT NULL
                THEN COALESCE(_operationid, operationid)
                ELSE operationid
            END
        WHERE
            (_alternateid IS NOT NULL AND alternateid = _alternateid) OR
            (_operationid IS NOT NULL AND operationid = _operationid)
        RETURNING
            emailnotifications.alternateid,
            expirytime <= now() AS is_expired,
            expirytime > now() AS was_updated
    )
    SELECT
        ua.alternateid,
        ua.was_updated,
        ua.is_expired
    INTO v_alternateid, v_was_updated, v_is_expired
    FROM update_attempt ua;

    -- Return results (handle not found case)
    IF v_alternateid IS NULL THEN
        RETURN QUERY SELECT NULL::uuid, false, false;
    ELSE
        RETURN QUERY SELECT v_alternateid, v_was_updated, v_is_expired;
    END IF;
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
- Uses a single UPDATE statement with conditional CASE logic for efficiency
- Checks expirytime > now() to conditionally update fields only when not expired
- If expired: returns (alternateid, false, true) without updating the notification status
- If not found: returns (NULL, false, false)
- If found and not expired: performs update and returns (alternateid, true, false)

Uniqueness assumptions: alternateid is unique (primary key); operationid uniquely identifies at most one row when non-null.
Overwrite policy: result and resulttime are always overwritten when not expired; operationid is only set when a non-null _operationid is supplied (existing value preserved when _operationid is null).';
