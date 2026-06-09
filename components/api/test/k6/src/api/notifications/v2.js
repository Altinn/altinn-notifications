import http from "k6/http";
import * as config from "../../config.js";
import * as apiHelpers from "../../apiHelpers.js";

export function postNotificationOrderV2(serializedOrder, token, label) {
  const endpoint = config.notifications.orders_v2;

  const params = apiHelpers.buildHeaderWithBearerAndContentType(token);
  params.tags = { name: label };

  return http.post(endpoint, serializedOrder, params);
}

export function postSmsInstantNotificationOrderRequest(data, token, label) {
  const endpoint = config.notifications.orders_sms_instant_v2;

  const params = apiHelpers.buildHeaderWithBearerAndContentType(token);
  params.tags = { name: label };

  return http.post(endpoint, data, params);
}

export function postSmsInstantNotificationOrderRequestOld(data, token, label) {
  const endpoint = config.notifications.orders_sms_instant_old_v2;

  const params = apiHelpers.buildHeaderWithBearerAndContentType(token);
  params.tags = { name: label };

  return http.post(endpoint, data, params);
}

export function postEmailInstantNotificationOrderRequest(data, token, label) {
  const endpoint = config.notifications.orders_email_instant_v2;

  const params = apiHelpers.buildHeaderWithBearerAndContentType(token);
  params.tags = { name: label };

  return http.post(endpoint, data, params);
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
