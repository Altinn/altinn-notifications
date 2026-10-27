CREATE OR REPLACE FUNCTION notifications.updateemailnotification_v4(
    _result text,
    _operationid text,
    _alternateid uuid,
    _deliveryreport jsonb,
    _total_attachment_size_bytes BIGINT
)
RETURNS TABLE (
    alternateid uuid,
    was_updated boolean,
    is_expired boolean
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_now TIMESTAMPTZ := now();
BEGIN
    -- Sentinel row returned for both "no identifier provided" and "notification not found" cases.
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
            WHEN expirytime > v_now THEN _result::emailnotificationresulttype
            ELSE result
        END,
        -- Update resulttime only if not expired, otherwise keep existing value
        resulttime = CASE
            WHEN expirytime > v_now THEN v_now
            ELSE resulttime
        END,
        -- Update operationid only if not expired and alternateid was provided
        operationid = CASE
            WHEN expirytime > v_now AND _alternateid IS NOT NULL
            THEN COALESCE(_operationid, operationid)
            ELSE operationid
        END,
        -- Always overwritten, including with NULL: observational gateway data, not subject to expiry
        deliveryreport = _deliveryreport,
        -- Preserve previously stored composed size when later updates pass 0 (e.g. delivery reports)
        total_attachment_size_bytes = CASE
            WHEN _total_attachment_size_bytes IS NOT NULL THEN _total_attachment_size_bytes
            ELSE emailnotifications.total_attachment_size_bytes
        END
    WHERE
        -- Match by alternateid (takes priority) OR by operationid (fallback)
        -- Strict precedence: if alternateid is provided, only use that
        (_alternateid IS NOT NULL AND emailnotifications.alternateid = _alternateid) OR
        (_alternateid IS NULL AND _operationid IS NOT NULL AND emailnotifications.operationid = _operationid)
    RETURNING
        emailnotifications.alternateid,
        -- was_updated reflects whether status fields were modified; deliveryreport and total_attachment_size_bytes writes do not affect this flag
        (expirytime > v_now) AS was_updated,
        -- is_expired is true if the notification was expired at UPDATE time
        (expirytime <= v_now) AS is_expired;

    IF NOT FOUND THEN
        RETURN QUERY SELECT NULL::uuid, false, false;
    END IF;
END;
$$;

ALTER FUNCTION notifications.updateemailnotification_v4(
    _result text,
    _operationid text,
    _alternateid uuid,
    _deliveryreport jsonb,
    _total_attachment_size_bytes BIGINT
)
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.updateemailnotification_v4 IS
'Updates an email notification result, resulttime, operationid, deliveryreport,
and total_attachment_size_bytes by alternateid or operationid, with expiry validation.
Extends v3 by adding _total_attachment_size_bytes (BIGINT).
Standard email results pass 0; composed email results pass the computed value.
When _total_attachment_size_bytes is NULL, the existing value is preserved.

Return values:
- alternateid: UUID of the notification (NULL if not found)
- was_updated: true if status fields were modified (notification not expired)
- is_expired:  true if expirytime <= now()

Precedence: alternateid takes priority over operationid when both are non-null.
deliveryreport is always overwritten regardless of expiry.
total_attachment_size_bytes is overwritten only when _total_attachment_size_bytes IS NOT NULL; otherwise the existing value is preserved.';
