import http from "k6/http";

import * as config from "../../config.js";

import * as apiHelpers from "../../apiHelpers.js";

export function postEmailNotificationOrder(serializedOrder, token) {
  var endpoint = config.notifications.orders_email;

  var params = apiHelpers.buildHeaderWithBearerAndContentType(token);

  var response = http.post(endpoint, serializedOrder, params);

  return response;
}

export function postSmsNotificationOrder(serializedOrder, token) {
  var endpoint = config.notifications.orders_sms;

  var params = apiHelpers.buildHeaderWithBearerAndContentType(token);

  var response = http.post(endpoint, serializedOrder, params);

  return response;
}

export function getById(id, token) {
  var endpoint = config.notifications.orders_fromId(id);
  return getByUrl(endpoint, token);
}

export function getByUrl(url, token) {
  var params = apiHelpers.buildHeaderWithBearer(token);
  var response = http.get(url, params);

  return response;
}

export function getBySendersReference(sendersReference, token) {
  var endpoint =
    config.notifications.orders_fromSendersRef(sendersReference);
  var params = apiHelpers.buildHeaderWithBearer(token);
  var response = http.get(endpoint, params);
  return response;
}

export function getWithStatus(orderId, token) {
  var endpoint = config.notifications.orders_status(orderId);
  var params = apiHelpers.buildHeaderWithBearer(token);
  var response = http.get(endpoint, params);
  return response;
}
