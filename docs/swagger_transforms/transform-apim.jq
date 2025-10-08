.openapi = "3.0.1"
| del(.servers)
| del(.security)
| del(.components.securitySchemes)
| .paths |= map_values(map_values(del(.security)))
| .paths |= with_entries(.key |= sub("/notifications/api/v1"; ""))
| .paths |= map_values(map_values(.tags |= map(if . == "FutureOrders" then "Orders" elif . == "Shipment" then "Orders" elif . == "InstantOrders" then "Instant Orders" else . end)))