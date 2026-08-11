DROP FUNCTION IF EXISTS notifications.cancelorder(uuid, text);

DROP FUNCTION IF EXISTS notifications.delete_old_status_feed_records();

DROP FUNCTION IF EXISTS notifications.getemails_statusnew_updatestatus();

DROP FUNCTION IF EXISTS notifications.getmetrics(integer, integer);

DROP FUNCTION IF EXISTS notifications.get_notifications_by_nin(text, timestamp with time zone, timestamp with time zone);

DROP FUNCTION IF EXISTS notifications.getorder_includestatus_v4(uuid, text);

DROP FUNCTION IF EXISTS notifications.getshipmentforstatusfeed(uuid);

DROP FUNCTION IF EXISTS notifications.getshipmentforstatusfeed_v2(uuid);

DROP FUNCTION IF EXISTS notifications.get_shipment_tracking(uuid, text);

DROP FUNCTION IF EXISTS notifications.get_shipment_tracking_v2(uuid, text);

DROP FUNCTION IF EXISTS notifications.getsms_statusnew_updatestatus();

DROP FUNCTION IF EXISTS notifications.getsms_statusnew_updatestatus(integer);

DROP FUNCTION IF EXISTS notifications.updatesmsnotification_v2(text, text, uuid);

DROP PROCEDURE IF EXISTS notifications.updateemailstatus(uuid, text, text);

DROP FUNCTION IF EXISTS notifications.updateemailnotification(text, text, uuid);          -- v1

DROP FUNCTION IF EXISTS notifications.updateemailnotification_v2(text, text, uuid);       -- v2

DROP FUNCTION IF EXISTS notifications.updateemailnotification_v3(text, text, uuid, jsonb); -- v3

DROP FUNCTION IF EXISTS notifications.updatesmsnotification_v2(text, text, uuid); -- v2
