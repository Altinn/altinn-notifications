import http from "k6/http";
import * as config from "../../config.js";
import * as apiHelpers from "../../apiHelpers.js";

export function postEmailNotificationOrder(serializedOrder, token) {
  const endpoint = config.notifications.orders_email;

  const params = apiHelpers.buildHeaderWithBearerAndContentType(token);

  return http.post(endpoint, serializedOrder, params);
}

export function postSmsNotificationOrder(serializedOrder, token) {
  const endpoint = config.notifications.orders_sms;

  const params = apiHelpers.buildHeaderWithBearerAndContentType(token);

  return http.post(endpoint, serializedOrder, params);
}

export function getById(id, token) {
  const endpoint = config.notifications.orders_fromId(id);

  return getByUrl(endpoint, token);
}

export function getByUrl(url, token) {
  const params = apiHelpers.buildHeaderWithBearer(token);

  return http.get(url, params);
}

export function getBySendersReference(sendersReference, token) {
  const endpoint = config.notifications.orders_fromSendersRef(sendersReference);

  const params = apiHelpers.buildHeaderWithBearer(token);

  return http.get(endpoint, params);
}

export function getWithStatus(orderId, token) {
  const endpoint = config.notifications.orders_status(orderId);

  const params = apiHelpers.buildHeaderWithBearer(token);

  return http.get(endpoint, params);
}
