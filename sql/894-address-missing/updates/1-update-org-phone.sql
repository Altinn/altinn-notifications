-- fk_registry_organization_id: 74941
-- registry_organization_number: 313935914
-- notification_address_id: 136621

-- Set my contact info for testing
UPDATE organization_notification_address.notifications_address
SET address = '98491711',
    domain = '+47',
    full_address = '+4798491711'
WHERE notification_address_id = 136621;

-- Remove contact info while sending a notification
UPDATE organization_notification_address.notifications_address
SET address = '',
    domain = '',
    full_address = ''
WHERE notification_address_id = 136621;

-- Restore contact info after testing
UPDATE organization_notification_address.notifications_address
SET address = '99999999',
    domain = '+47',
    full_address = '+4799999999'
WHERE notification_address_id = 136621;

-- Check the current state
SELECT address,
       domain,
       full_address,
       address_type,
       notification_address_id
FROM organization_notification_address.notifications_address
WHERE notification_address_id = 136621;
