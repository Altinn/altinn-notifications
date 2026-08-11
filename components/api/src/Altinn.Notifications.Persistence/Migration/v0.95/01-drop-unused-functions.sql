DROP FUNCTION IF EXISTS notifications.cancelorder(uuid, text);

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
