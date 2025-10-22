-- fk_registry_organization_id: 102275
-- registry_organization_number: 312705931
-- notification_address_id: 171486

-- Set my contact info for testing
UPDATE organization_notification_address.notifications_address
SET address = 'martin.vagseter.jakobsen',
    domain = 'digdir.no',
    full_address = 'martin.vagseter.jakobsen@digdir.no'
WHERE notification_address_id = 171486;

-- Remove contact info while sending a notification
UPDATE organization_notification_address.notifications_address
SET address = '',
    domain = '',
    full_address = ''
WHERE notification_address_id = 171486;

-- Restore contact info after testing
UPDATE organization_notification_address.notifications_address
SET address = 'nullstillt',
    domain = 'default.digdir.no',
    full_address = 'nullstillt@default.digdir.no'
WHERE notification_address_id = 171486;

-- Check the current state
SELECT address,
       domain,
       full_address,
       address_type,
       notification_address_id
FROM organization_notification_address.notifications_address
WHERE notification_address_id = 171486;
