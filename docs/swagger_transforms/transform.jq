.openapi = "3.0.3"
| del(.security)
| del(.components.securitySchemes)
| .paths |= map_values(map_values(del(.security)))
| .paths |= with_entries(.key |= sub("/notifications/api/v1"; ""))
| .paths |= map_values(map_values(.tags |= map(if . == "InstantOrders" then "Instant Orders" elif . == "ComposedEmailOrders" then "Order" else . end)))
| .servers |= map(select(.url | contains("localhost") | not))
| .servers |= map(.url |= sub("altinn.no/"; "altinn.no/notifications/api/v1"))
| del(.paths."/tests/sendcondition")
| del(.paths."/metrics", .paths."/metrics/sms", .paths."/metrics/email")
| .paths |= with_entries(select(.key | startswith("/future/dashboard") | not))
| .components.schemas |= with_entries(select(.key | startswith("Dashboard") | not))
| del(.tags)
| .tags += [{"name": "Deprecated", "description": "Legacy endpoints. Still supported, but going forward please use '/future/*' endpoints instead."}]
| .tags += [{"name": "Order", "description": "Create notifications (queued processing)"}]
| .tags += [{"name": "Status", "description": "Retrieve delivery status for notification orders"}]
| .tags += [{"name": "Instant Orders", "description": "Create and immediately send a notification to a single recipient"}]
| .paths |= with_entries(
    if (.key | startswith("/orders")) or .key == "/future/orders/instant" then .value |= map_values(.tags = ["Deprecated"])
    elif .key == "/future/orders" then .value |= map_values(.tags = ["Order"])
    elif .key | startswith("/future/shipment") then .value |= map_values(.tags = ["Status"])
    else . end)
