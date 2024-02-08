import http from "k6/http";

import * as config from "../../config.js";

import * as apiHelpers from "../../apiHelpers.js";

export function getEmailNotifications(orderId, token) {
  var endpoint = config.notifications.notifications_email(orderId);
  var params = apiHelpers.buildHeaderWithBearer(token);
  var response = http.get(endpoint, params);
  return response;
  }

  export function getSmsNotifications(orderId, token) {
    var endpoint = config.notifications.notifications_sms(orderId);
    var params = apiHelpers.buildHeaderWithBearer(token);
    var response = http.get(endpoint, params);
    return response;
    }
