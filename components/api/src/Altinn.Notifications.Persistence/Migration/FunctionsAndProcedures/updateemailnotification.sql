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

-- v2 introduces expiry time validation and conditional updates
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
BEGIN
    -- Handle case where neither identifier is provided
    IF _alternateid IS NULL AND _operationid IS NULL THEN
        RETURN QUERY SELECT NULL::uuid, false, false;
        RETURN;
    END IF;

    -- Single UPDATE with conditional logic based on expiry time
    RETURN QUERY
    UPDATE notifications.emailnotifications
    SET
        -- Update result only if not expired, otherwise keep existing value
        result = CASE
            WHEN expirytime > now() THEN _result::emailnotificationresulttype
            ELSE result 
        END,
        -- Update resulttime only if not expired, otherwise keep existing value
        resulttime = CASE
            WHEN expirytime > now() THEN now()
            ELSE resulttime
        END,
        -- Update operationid only if not expired and alternateid was provided
        operationid = CASE
            WHEN expirytime > now() AND _alternateid IS NOT NULL
            THEN COALESCE(_operationid, operationid) 
            ELSE operationid 
        END
    WHERE
        -- Match by alternateid (takes priority) OR by operationid (fallback)
        -- Strict precedence: if alternateid is provided, only use that
        (_alternateid IS NOT NULL AND emailnotifications.alternateid = _alternateid) OR
        (_alternateid IS NULL AND _operationid IS NOT NULL AND emailnotifications.operationid = _operationid)
    RETURNING
        emailnotifications.alternateid,
        -- was_updated is true only if the notification was not expired at UPDATE time
        (expirytime > now()) AS was_updated,
        -- is_expired is true if the notification was expired at UPDATE time
        (expirytime <= now()) AS is_expired;

    -- If RETURNING didn't return any rows, the notification was not found
    IF NOT FOUND THEN
        RETURN QUERY SELECT NULL::uuid, false, false;
    END IF;
END;
$$;

COMMENT ON FUNCTION notifications.updateemailnotification_v2 IS
'Updates an email notification''s result and resulttime by alternateid or by operationid, with expiry time validation.

Precedence: If both _alternateid and _operationid are non-null, only alternateid is used for lookup; _operationid may still populate the row via COALESCE.

Return values:
- alternateid: The UUID of the notification (NULL if not found)
- was_updated: true if values were modified (notification not expired), false otherwise
- is_expired: true if the notification has passed its expiry time (expirytime <= now())

Behavior:
- Single UPDATE operation with implicit row-level locking
- CASE expressions conditionally modify fields only when expirytime > now()
- If expired: UPDATE executes but keeps existing values, returns (alternateid, false, true)
- If not found: returns (NULL, false, false)
- If found and not expired: modifies values and returns (alternateid, true, false)

Uniqueness assumptions: alternateid is unique (primary key); operationid uniquely identifies at most one row when non-null.
Overwrite policy: result and resulttime are conditionally overwritten when not expired; operationid is only set when a non-null _operationid is supplied and notification is not expired.';