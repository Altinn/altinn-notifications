-- FUNCTION: notifications.delete_old_test_data()

-- DROP FUNCTION IF EXISTS notifications.delete_old_test_data();

CREATE OR REPLACE FUNCTION notifications.delete_old_test_data()
RETURNS void
LANGUAGE 'plpgsql'
AS $BODY$
DECLARE
    deleted_count INTEGER := 0;
BEGIN
    -- Delete for orders
    DELETE FROM notifications.orders
    WHERE creatorname = 'ttd' 
    AND requestedsendtime < NOW() - INTERVAL '90 days'
    AND (
        notificationorder -> 'Recipients' -> 0 -> 'AddressInfo' -> 0 ->> 'MobileNumber' IN ('+4799999999', '+4792395437')
        OR notificationorder -> 'Recipients' -> 0 -> 'AddressInfo' -> 0 ->> 'EmailAddress' = 'wvjckqug@sharklasers.com'
        OR notificationorder -> 'Recipients' -> 0 ->> 'OrganizationNumber' IN ('910026623', '910058789', '810889802')
    );
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % old test orders', deleted_count;

    -- Delete for orderschain
    DELETE FROM notifications.orderschain
    WHERE creatorname = 'ttd' 
    AND created < NOW() - INTERVAL '90 days'
    AND (
        orderchain -> 'Recipient' -> 'RecipientOrganization' ->> 'OrgNumber' IN ('910058789', '910026623', '810889802')
        OR orderchain -> 'Recipient' -> 'RecipientSms' ->> 'PhoneNumber' IN ('+4799999999', '+4792395437')
        OR orderchain -> 'Recipient' -> 'RecipientEmail' ->> 'EmailAddress' = 'wvjckqug@sharklasers.com'
    );
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RAISE NOTICE 'Deleted % old test order chains', deleted_count;
END;
$BODY$;

ALTER FUNCTION notifications.delete_old_test_data()
    OWNER TO platform_notifications_admin;

COMMENT ON FUNCTION notifications.delete_old_test_data()
    IS 'This function performs cleanup of test-specific data based on hard-coded criteria used in use case tests. 
	It removes records from notifications.orders, notifications.emailnotifications (cascading delete), and notifications.smsnotifications (cascading delete), 
	using email addresses, mobile numbers, and synthetic organizations used only for testing. This ensures test data does not accumulate.';
