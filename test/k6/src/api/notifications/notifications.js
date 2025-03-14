import http from "k6/http";
import * as config from "../../config.js";
import * as apiHelpers from "../../apiHelpers.js";

export function getEmailNotifications(orderId, token, label) {
  const params = apiHelpers.buildHeaderWithBearer(token);
  params.tags = { name: label };

  const endpoint = config.notifications.notifications_email(orderId);

  return http.get(endpoint, params);
}

export function getSmsNotifications(orderId, token, label) {
  const params = apiHelpers.buildHeaderWithBearer(token);
  params.tags = { name: label };

  const endpoint = config.notifications.notifications_sms(orderId);

  return http.get(endpoint, params);
}
