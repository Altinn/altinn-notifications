import http from "k6/http";

import * as config from "../../config.js";

import * as apiHelpers from "../../apiHelpers.js";

export function postEmailNotificationOrder(serializedOrder, token) {
  var endpoint = config.notifications.orders_email;

  var params = apiHelpers.buildHeaderWithBearerAndContentType(token);

  var response = http.post(endpoint, serializedOrder, params);

  return response;
}