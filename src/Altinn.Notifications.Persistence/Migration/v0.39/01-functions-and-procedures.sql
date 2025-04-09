-- This script is autogenerated from the tool DbTools. Do not edit manually.

-- cancelorder.sql:
CREATE OR REPLACE FUNCTION notifications.cancelorder(
    _alternateid uuid,
    _creatorname text
)
RETURNS TABLE(
    cancelallowed boolean,
    alternateid uuid,
    creatorname text,
    sendersreference text,
    created timestamp with time zone,                  
    requestedsendtime timestamp with time zone,
    processed timestamp with time zone,
    processedstatus orderprocessingstate,
    notificationchannel text,
    ignorereservation boolean,
    resourceid text,
    conditionendpoint text,
    generatedemailcount bigint,
    succeededemailcount bigint,
    generatedsmscount bigint, 
    succeededsmscount bigint
) 
LANGUAGE plpgsql
AS $$
DECLARE
    order_record RECORD;
BEGIN
    -- Retrieve the order and its status
    SELECT o.requestedsendtime, o.processedstatus
    INTO order_record
    FROM notifications.orders o
    WHERE o.alternateid = _alternateid AND o.creatorname = _creatorname;

    -- If no order is found, return an empty result set
    IF NOT FOUND THEN
        RETURN;
    END IF;
    
     -- Check if order is already cancelled
     IF order_record.processedstatus = 'Cancelled' THEN
        RETURN QUERY 
        SELECT TRUE AS cancelallowed,
           order_details.*
        FROM notifications.getorder_includestatus_v4(_alternateid, _creatorname) AS order_details;
     ELSIF (order_record.requestedsendtime <= NOW() + INTERVAL '5 minutes' or order_record.processedstatus != 'Registered') THEN
        RETURN QUERY 
        SELECT FALSE AS cancelallowed, NULL::uuid, NULL::text, NULL::text, NULL::timestamp with time zone, NULL::timestamp with time zone, NULL::timestamp with time zone, NULL::orderprocessingstate, NULL::text, NULL::boolean, NULL::text, NULL::text, NULL::bigint, NULL::bigint, NULL::bigint, NULL::bigint;
     ELSE 
        -- Cancel the order by updating its status
        UPDATE notifications.orders
        SET processedstatus = 'Cancelled', processed = NOW()
        WHERE notifications.orders.alternateid = _alternateid;

        -- Retrieve the updated order details
        RETURN QUERY 
        SELECT TRUE AS cancelallowed,
               order_details.*
        FROM notifications.getorder_includestatus_v4(_alternateid, _creatorname) AS order_details;
    END IF;      
END;
$$;


-- getemailrecipients.sql:
CREATE OR REPLACE FUNCTION notifications.getemailrecipients_v2(_alternateid uuid)
RETURNS TABLE(
    recipientorgno text, 
    recipientnin text, 
    toaddress text
) 
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _alternateid);
BEGIN
RETURN query 
	SELECT e.recipientorgno, e.recipientnin, e.toaddress
	FROM notifications.emailnotifications e
	WHERE e._orderid = __orderid;
END;
$BODY$;

-- getemailsstatusnewupdatestatus.sql:
CREATE OR REPLACE FUNCTION notifications.getemails_statusnew_updatestatus()
    RETURNS TABLE(alternateid uuid, subject text, body text, fromaddress text, toaddress text, contenttype text) 
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    latest_email_timeout TIMESTAMP WITH TIME ZONE;
BEGIN
    SELECT emaillimittimeout 
    INTO latest_email_timeout 
    FROM notifications.resourcelimitlog 
    WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog);

    -- Check if the latest email timeout is set and valid
    IF latest_email_timeout IS NOT NULL THEN
        IF latest_email_timeout >= NOW() THEN
            RETURN QUERY 
            SELECT NULL::uuid AS alternateid, 
                   NULL::text AS subject, 
                   NULL::text AS body, 
                   NULL::text AS fromaddress, 
                   NULL::text AS toaddress, 
                   NULL::text AS contenttype 
            WHERE FALSE;
            RETURN;
        ELSE 
            UPDATE notifications.resourcelimitlog 
            SET emaillimittimeout = NULL 
            WHERE id = (SELECT MAX(id) FROM notifications.resourcelimitlog);
        END IF;
    END IF;
    
    RETURN QUERY 
    WITH updated AS (
        UPDATE notifications.emailnotifications
        SET result = 'Sending', resulttime = now()
        WHERE result = 'New' 
        RETURNING notifications.emailnotifications.alternateid, 
                  _orderid, 
                  notifications.emailnotifications.toaddress, 
                  notifications.emailnotifications.customizedsubject, 
                  notifications.emailnotifications.customizedbody
    )
    SELECT u.alternateid, 
           CASE WHEN u.customizedsubject IS NOT NULL AND u.customizedsubject <> '' THEN u.customizedsubject ELSE et.subject END AS subject, 
           CASE WHEN u.customizedbody IS NOT NULL AND u.customizedbody <> '' THEN u.customizedbody ELSE et.body END AS body, 
           et.fromaddress, 
           u.toaddress, 
           et.contenttype 
    FROM updated u
    JOIN notifications.emailtexts et ON u._orderid = et._orderid;    
END;
$BODY$;


-- getemailsummary.sql:
CREATE OR REPLACE FUNCTION notifications.getemailsummary_v2(
	_alternateorderid uuid,
	_creatorname text)
    RETURNS TABLE(
        sendersreference text, 
        alternateid uuid, 
        recipientorgno text, 
        recipientnin text, 
        toaddress text, 
        result emailnotificationresulttype, 
        resulttime timestamptz) 
    LANGUAGE 'plpgsql'
AS $BODY$

	BEGIN
		RETURN QUERY
		   SELECT o.sendersreference, n.alternateid, n.recipientorgno, n.recipientnin, n.toaddress, n.result, n.resulttime
			FROM notifications.emailnotifications n
            LEFT JOIN notifications.orders o ON n._orderid = o._id
			WHERE o.alternateid = _alternateorderid
			and o.creatorname = _creatorname;
        IF NOT FOUND THEN
            RETURN QUERY
            SELECT o.sendersreference, NULL::uuid, NULL::text, NULL::text, NULL::text, NULL::emailnotificationresulttype, NULL::timestamptz
            FROM notifications.orders o
            WHERE o.alternateid = _alternateorderid
            and o.creatorname = _creatorname;
        END IF;
	END;
$BODY$;

-- getmetrics.sql:
CREATE OR REPLACE FUNCTION notifications.getmetrics(
    month_input int,
    year_input int
)
RETURNS TABLE (
    org text,
    placed_orders bigint,
    sent_emails bigint,
    succeeded_emails bigint,
    sent_sms bigint,
    succeeded_sms bigint
)
AS $$
BEGIN
    RETURN QUERY
    SELECT
        o.creatorname,
        COUNT(DISTINCT o._id) AS placed_orders,
        SUM(CASE WHEN e._id IS NOT NULL THEN 1 ELSE 0 END) AS sent_emails,
        SUM(CASE WHEN e.result IN ('Delivered', 'Succeeded') THEN 1 ELSE 0 END) AS succeeded_emails, 
        SUM(CASE WHEN s._id IS NOT NULL THEN s.smscount ELSE 0 END) AS sent_sms,
        SUM(CASE WHEN s.result = 'Accepted' THEN 1 ELSE 0 END) AS succeeded_sms
    FROM notifications.orders o
    LEFT JOIN notifications.emailnotifications e ON o._id = e._orderid
    LEFT JOIN notifications.smsnotifications s ON o._id = s._orderid
    WHERE EXTRACT(MONTH FROM o.requestedsendtime) = month_input
        AND EXTRACT(YEAR FROM o.requestedsendtime) = year_input
    GROUP BY o.creatorname;
END;
$$ LANGUAGE plpgsql;


-- getorderincludestatus.sql:
CREATE OR REPLACE FUNCTION notifications.getorder_includestatus_v4(
    _alternateid uuid,
    _creatorname text
)
RETURNS TABLE(
    alternateid uuid,
    creatorname text,
    sendersreference text,
    created timestamp with time zone,                  
    requestedsendtime timestamp with time zone,
    processed timestamp with time zone,
    processedstatus orderprocessingstate,
    notificationchannel text,
    ignorereservation boolean,
    resourceid text,
    conditionendpoint text,
    generatedemailcount bigint,
    succeededemailcount bigint,
    generatedsmscount bigint, 
    succeededsmscount bigint
) 
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    _target_orderid INTEGER;
    _succeededEmailCount BIGINT;
    _generatedEmailCount BIGINT;
    _succeededSmsCount BIGINT;
    _generatedSmsCount BIGINT;
BEGIN
    SELECT _id INTO _target_orderid 
    FROM notifications.orders
    WHERE orders.alternateid = _alternateid 
    AND orders.creatorname = _creatorname;
    
    SELECT
        SUM(CASE WHEN result IN ('Delivered', 'Succeeded') THEN 1 ELSE 0 END), 
        COUNT(1) AS generatedEmailCount
    INTO _succeededEmailCount, _generatedEmailCount
    FROM notifications.emailnotifications
    WHERE _orderid = _target_orderid;
    
    SELECT      
        SUM(CASE WHEN result = 'Accepted' THEN 1 ELSE 0 END), 
        COUNT(1) AS generatedSmsCount
    INTO _succeededSmsCount, _generatedSmsCount
    FROM notifications.smsnotifications
    WHERE _orderid = _target_orderid;

    RETURN QUERY
    SELECT 
        orders.alternateid,
        orders.creatorname,
        orders.sendersreference,
        orders.created,
        orders.requestedsendtime,
        orders.processed,
        orders.processedstatus,
        orders.notificationorder->>'NotificationChannel',
        CASE 
            WHEN orders.notificationorder->>'IgnoreReservation' IS NULL THEN NULL
            ELSE (orders.notificationorder->>'IgnoreReservation')::BOOLEAN
        END AS IgnoreReservation,
        orders.notificationorder->>'ResourceId',
        orders.notificationorder->>'ConditionEndpoint',
        _generatedEmailCount,
        _succeededEmailCount,
        _generatedSmsCount, 
        _succeededSmsCount
    FROM
        notifications.orders AS orders
    WHERE 
        orders.alternateid = _alternateid;
END;
$BODY$;


-- getorderschaintracking.sql:
-- Retrieves tracking information for a notification order chain using the creator's short name and idempotency identifier.
CREATE OR REPLACE FUNCTION notifications.get_orders_chain_tracking
(
    _creatorname text,
    _idempotencyid text
)
RETURNS TABLE (
    orders_chain_id uuid,
    shipment_id uuid,
    senders_reference text,
    reminders jsonb
) 
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    v_record_exists boolean;
BEGIN
    -- Check if record exists first to provide better error handling
    SELECT EXISTS (
        SELECT 1 
        FROM notifications.orderschain 
        WHERE creatorname = _creatorname
        AND idempotencyid = _idempotencyid
    ) INTO v_record_exists;
    
    IF NOT v_record_exists THEN
        -- Return empty result set with no rows
        RETURN;
    END IF;

    RETURN QUERY
    SELECT 
        orders_chain.orderid AS orders_chain_id,
        (orders_chain.orderchain->>'OrderId')::uuid AS shipment_id,
        orders_chain.orderchain->>'SendersReference' AS senders_reference,
        -- Extract only OrderId and SendersReference from each reminder
        COALESCE(
            (SELECT jsonb_agg(
                jsonb_build_object(
                    'ShipmentId', reminder->>'OrderId',
                    'SendersReference', reminder->>'SendersReference'
                )
            )
            FROM jsonb_array_elements(
                CASE 
                    WHEN jsonb_typeof(orders_chain.orderchain->'Reminders') = 'array' AND 
                         (orders_chain.orderchain->'Reminders') IS NOT NULL AND
                         (orders_chain.orderchain->'Reminders') <> 'null'::jsonb
                    THEN orders_chain.orderchain->'Reminders'
                    ELSE '[]'::jsonb
                END
            ) AS reminder),
            '[]'::jsonb
        ) AS reminders
    FROM 
        notifications.orderschain orders_chain
    WHERE 
        orders_chain.creatorname = _creatorname
        AND orders_chain.idempotencyid = _idempotencyid;
END;
$BODY$;

COMMENT ON FUNCTION notifications.get_orders_chain_tracking IS 
'Retrieves tracking information for a notification order chain using the creator''s short name and idempotency identifier.

This function enables idempotent operations by allowing clients to retrieve previously submitted
notification chain information without creating duplicates.

Parameters:
- _creatorname: The short name of the creator that originally submitted the notification order chain
- _idempotencyid: The idempotency identifier that was defined when the order chain was created

Returns a table with the following columns:
- orders_chain_id: The unique identifier for the entire notification order chain
- shipment_id: The unique identifier for the main notification order
- senders_reference: The sender''s reference for the main notification order (may be null)
- reminders: A JSON array containing tracking information for any reminder notifications

The reminders JSON array contains objects with the following structure:
- OrderId: The unique identifier for the reminder notification order
- SendersReference: The sender''s reference for the reminder notification (may be null).';


-- getorderspastsendtimeupdatestatus.sql:
CREATE OR REPLACE FUNCTION notifications.getorders_pastsendtime_updatestatus()
    RETURNS TABLE(notificationorders jsonb)
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
RETURN QUERY
	UPDATE notifications.orders
	SET processedstatus = 'Processing'
	WHERE _id IN (select _id
				 from notifications.orders
				 where processedstatus = 'Registered'
				 and requestedsendtime <= now() + INTERVAL '1 minute'
				 limit 50)
	RETURNING notificationorder AS notificationorders;
END;
$BODY$;

-- getsmsrecipients.sql:
CREATE OR REPLACE FUNCTION notifications.getsmsrecipients_v2(_orderid uuid)
RETURNS TABLE(
  recipientorgno text, 
  recipientnin text,
  mobilenumber text
) 
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
__orderid BIGINT := (SELECT _id from notifications.orders
			where alternateid = _orderid);
BEGIN
RETURN query 
	SELECT s.recipientorgno, s.recipientnin, s.mobilenumber
	FROM notifications.smsnotifications s
	WHERE s._orderid = __orderid;
END;
$BODY$;

-- getsmsstatusnewupdatestatus.sql:
CREATE OR REPLACE FUNCTION notifications.getsms_statusnew_updatestatus()
    RETURNS TABLE(alternateid uuid, sendernumber text, mobilenumber text, body text) 
    LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    RETURN QUERY 
    WITH updated AS (
        UPDATE notifications.smsnotifications
        SET result = 'Sending', resulttime = now()
        WHERE result = 'New' 
        RETURNING notifications.smsnotifications.alternateid, 
                  _orderid, 
                  notifications.smsnotifications.mobilenumber,
                  notifications.smsnotifications.customizedbody
    )
    SELECT u.alternateid, 
           st.sendernumber, 
           u.mobilenumber, 
           CASE WHEN u.customizedbody IS NOT NULL AND u.customizedbody <> '' THEN u.customizedbody ELSE st.body END AS body
    FROM updated u
    JOIN notifications.smstexts st ON u._orderid = st._orderid;        
END;
$BODY$;


-- getsmssummary.sql:
CREATE OR REPLACE FUNCTION notifications.getsmssummary_v2(
	_alternateorderid uuid,
	_creatorname text)
    RETURNS TABLE(
        sendersreference text, 
        alternateid uuid, 
        recipientorgno text, 
        recipientnin text, 
        mobilenumber text, 
        result smsnotificationresulttype, 
        resulttime timestamptz) 
    LANGUAGE 'plpgsql'
AS $BODY$

	BEGIN
		RETURN QUERY
		   SELECT o.sendersreference, n.alternateid, n.recipientorgno, n.recipientnin, n.mobilenumber, n.result, n.resulttime
			FROM notifications.smsnotifications n
            LEFT JOIN notifications.orders o ON n._orderid = o._id
			WHERE o.alternateid = _alternateorderid
			and o.creatorname = _creatorname;
        IF NOT FOUND THEN
            RETURN QUERY
            SELECT o.sendersreference, NULL::uuid, NULL::text, NULL::text, NULL::text, NULL::smsnotificationresulttype, NULL::timestamptz
            FROM notifications.orders o
            WHERE o.alternateid = _alternateorderid
            and o.creatorname = _creatorname;
        END IF;
	END;
$BODY$;

-- insertemailnotification.sql:
CREATE OR REPLACE PROCEDURE notifications.insertemailnotification(
    _orderid uuid,
    _alternateid uuid,
    _recipientorgno TEXT,
    _recipientnin TEXT,
    _toaddress TEXT,
    _customizedbody TEXT,
    _customizedsubject TEXT,
    _result TEXT,
    _resulttime timestamptz,
    _expirytime timestamptz
)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    __orderid BIGINT;
BEGIN
    SELECT _id INTO __orderid 
    FROM notifications.orders
    WHERE alternateid = _orderid;

    INSERT INTO notifications.emailnotifications(
        _orderid,
        alternateid,
        recipientorgno,
        recipientnin,
        toaddress,
        customizedbody,
        customizedsubject,
        result,
        resulttime,
        expirytime
    )
    VALUES (
        __orderid,
        _alternateid,
        _recipientorgno,
        _recipientnin,
        _toaddress,
        _customizedbody,
        _customizedsubject,
        _result::emailnotificationresulttype,
        _resulttime,
        _expirytime
    );
END;
$BODY$;

-- insertemailtext.sql:
CREATE OR REPLACE PROCEDURE notifications.insertemailtext(__orderid BIGINT, _fromaddress TEXT, _subject TEXT, _body TEXT, _contenttype TEXT)
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
INSERT INTO notifications.emailtexts(_orderid, fromaddress, subject, body, contenttype)
	VALUES (__orderid, _fromaddress, _subject, _body, _contenttype);
END;
$BODY$;


-- insertorder.sql:
CREATE OR REPLACE FUNCTION notifications.insertorder(_alternateid UUID, _creatorname TEXT, _sendersreference TEXT, _created TIMESTAMPTZ, _requestedsendtime TIMESTAMPTZ, _notificationorder JSONB)
RETURNS BIGINT
    LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
_orderid BIGINT;
BEGIN
	INSERT INTO notifications.orders(alternateid, creatorname, sendersreference, created, requestedsendtime, processed, notificationorder) 
	VALUES (_alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _created, _notificationorder)
   RETURNING _id INTO _orderid;
   
   RETURN _orderid;
END;
$BODY$;

-- Postgres supports function overloading - so this function has the same name, but one additional parameter: sendingTimePolicy
CREATE OR REPLACE FUNCTION notifications.insertorder(
	_alternateid uuid,
	_creatorname text,
	_sendersreference text,
	_created timestamp with time zone,
	_requestedsendtime timestamp with time zone,
	_notificationorder jsonb,
	_sendingtimepolicy integer)
    RETURNS bigint
    LANGUAGE 'plpgsql'
    COST 100
    VOLATILE PARALLEL UNSAFE
AS $BODY$

DECLARE
_orderid BIGINT;
BEGIN
	INSERT INTO notifications.orders(alternateid, creatorname, sendersreference, created, requestedsendtime, processed, notificationorder, sendingtimepolicy) 
	VALUES (_alternateid, _creatorname, _sendersreference, _created, _requestedsendtime, _created, _notificationorder, _sendingtimepolicy)
   RETURNING _id INTO _orderid;
   
   RETURN _orderid;
END;
$BODY$;


-- insertorderchain.sql:
CREATE OR REPLACE PROCEDURE notifications.insertorderchain(
    _orderid UUID,
    _idempotencyid TEXT,
    _creatorname TEXT,
    _created TIMESTAMP with time zone,
    _orderchain JSONB
)
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
    INSERT INTO notifications.orderschain(
        orderid,
        idempotencyid,
        creatorname,
        created,
        processed,
        orderchain
    )
    VALUES (
        _orderid,
        _idempotencyid,
        _creatorname,
        _created,
        _created,
        _orderchain
    );
END;
$BODY$;


-- insertsmsnotification.sql:
CREATE OR REPLACE PROCEDURE notifications.insertsmsnotification(
    _orderid uuid,
    _alternateid uuid,
    _recipientorgno TEXT,
    _recipientnin TEXT,
    _mobilenumber TEXT,
    _customizedbody TEXT,
    _result TEXT,
    _smscount integer,
    _resulttime timestamptz,
    _expirytime timestamptz
)
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    __orderid BIGINT;
BEGIN
    SELECT _id INTO __orderid 
    FROM notifications.orders
    WHERE alternateid = _orderid;

    INSERT INTO notifications.smsnotifications(
        _orderid,
        alternateid,
        recipientorgno,
        recipientnin,
        mobilenumber,
        customizedbody,
        result,
        smscount,
        resulttime,
        expirytime
    )
    VALUES (
        __orderid,
        _alternateid,
        _recipientorgno,
        _recipientnin,
        _mobilenumber,
        _customizedbody,
        _result::smsnotificationresulttype,
        _smscount,
        _resulttime,
        _expirytime
    );
END;
$BODY$;

-- updateemailstatus.sql:
CREATE OR REPLACE PROCEDURE notifications.updateemailstatus(_alternateid UUID, _result text, _operationid text)
LANGUAGE 'plpgsql'
AS $BODY$
BEGIN
	UPDATE notifications.emailnotifications 
	SET result = _result::emailnotificationresulttype, resulttime = now(), operationid = _operationid
	WHERE alternateid = _alternateid;
END;
$BODY$;

