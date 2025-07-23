import http from "k6/http";
import * as config from "../../config.js";
import * as apiHelpers from "../../apiHelpers.js";

export function postNotificationOrderV2(serializedOrder, token, label) {
  const endpoint = config.notifications.orders_v2;

  const params = apiHelpers.buildHeaderWithBearerAndContentType(token);
  params.tags = { name: label };

  return http.post(endpoint, serializedOrder, params);
}

export function getShipment(orderId, token, label) {
  const params = apiHelpers.buildHeaderWithBearer(token);
  params.tags = { name: label };

  const endpoint = config.notifications.shipment_v2(orderId);

  return http.get(endpoint, params);
}

export function getStatusFeed(sequenceNumber, token, label) {
  const params = apiHelpers.buildHeaderWithBearer(token);
  params.tags = { name: label };

  const endpoint = config.notifications.statusfeed_v2(sequenceNumber);
  return http.get(endpoint, params);
}
