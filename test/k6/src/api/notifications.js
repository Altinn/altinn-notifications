import http from "k6/http";

import * as config from "../config.js";

import * as apiHelpers from "../apiHelpers.js";

export function postEmailNotificationOrder(serializedOrder, token) {
  var endpoint = config.notifications.orders_email;

  var params = apiHelpers.buildHeaderWithBearerAndContentType(token);

  var response = http.post(endpoint, serializedOrder, params);

  return response;
}

export function getOrderById(id, token) {
  var endpoint = config.notifications.orders_fromId(id);
  return getOrderByUrl(endpoint, queryParams, token);
}

export function getOrderByUrl(url, token) {
  var params = apiHelpers.buildHeaderWithBearer(token);
  var response = http.get(url, params);

  return response;
}

export function getOrderBySendersReference(sendersReference, token) {
  var endpoint =
    config.notifications.orders_fromSendersRef(sendersReference);
  var params = apiHelpers.buildHeaderWithBearer(token);
  var response = http.get(endpoint, params);
  return response;
}

export function getOrderWithStatus(orderId, token) {
  var endpoint = config.notifications.orders_status(orderId);
  var params = apiHelpers.buildHeaderWithBearer(token);
  var response = http.get(endpoint, params);
  return response;
}
