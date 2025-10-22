SELECT
    notification_address_id,
    registry_id,
    address_type,
    domain,
    address,
    full_address,
    created_date_time,
    registry_updated_date_time,
    update_source,
    has_registry_accepted,
    is_soft_deleted,
    notification_name
FROM organization_notification_address.notifications_address
WHERE fk_registry_organization_id = (
    SELECT registry_organization_id
    FROM organization_notification_address.organizations
    WHERE registry_organization_number = '313935914'
)
  AND address_type = 1
ORDER BY registry_updated_date_time DESC;