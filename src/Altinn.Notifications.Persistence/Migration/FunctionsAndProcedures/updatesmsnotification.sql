-- v2 introduces expiry time validation and conditional updates
CREATE OR REPLACE FUNCTION notifications.updatesmsnotification_v2(
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
BEGIN
    -- Handle case where neither identifier is provided
    IF _alternateid IS NULL AND _gatewayreference IS NULL THEN
        RETURN QUERY SELECT NULL::uuid, false, false;
        RETURN;
    END IF;

    -- Single UPDATE with conditional logic based on expiry time
    RETURN QUERY
    UPDATE notifications.smsnotifications
    SET
        -- Update result only if not expired, otherwise keep existing value
        result = CASE
            WHEN expirytime > now() THEN _result::smsnotificationresulttype
            ELSE result
        END,
        -- Update resulttime only if not expired, otherwise keep existing value
        resulttime = CASE
            WHEN expirytime > now() THEN now()
            ELSE resulttime 
        END,
        -- Update gatewayreference only if not expired and alternateid was provided
        gatewayreference = CASE
            WHEN expirytime > now() AND _alternateid IS NOT NULL
            THEN COALESCE(_gatewayreference, gatewayreference)
            ELSE gatewayreference 
        END
    WHERE
        -- Match by alternateid (if provided) OR by gatewayreference (if provided)
        (_alternateid IS NOT NULL AND smsnotifications.alternateid = _alternateid) OR
        (_gatewayreference IS NOT NULL AND smsnotifications.gatewayreference = _gatewayreference)
    RETURNING
        smsnotifications.alternateid,
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

COMMENT ON FUNCTION notifications.updatesmsnotification_v2 IS
'Updates an SMS notification''s result and resulttime by alternateid or by gatewayreference, with expiry time validation.

Precedence: If both _alternateid and _gatewayreference are non-null, only alternateid is used for lookup; _gatewayreference may still populate the row via COALESCE.

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

Uniqueness assumptions: alternateid is unique (primary key); gatewayreference uniquely identifies at most one row when non-null.
Overwrite policy: result and resulttime are conditionally overwritten when not expired; gatewayreference is only set when a non-null _gatewayreference is supplied and notification is not expired.';