/*
    Test script of platform notifications api with org token
    Command:
    docker-compose run k6 run /src/tests/orders_sms.js `
    -e tokenGeneratorUserName=autotest `
    -e tokenGeneratorUserPwd=*** `
    -e mpClientId=*** `
    -e mpKid=altinn-usecase-events `
    -e encodedJwk=*** `
    -e env=*** `
    -e smsRecipient=*** `
    -e runFullTestSet=true

    For use case tests omit environment variable runFullTestSet or set value to false
*/

import { check } from "k6";
import { randomString } from "https://jslib.k6.io/k6-utils/1.2.0/index.js";
import * as setupToken from "../setup.js";
import * as notificationsApi from "../api/notifications/notifications.js";
import * as ordersApi from "../api/notifications/orders.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { generateJUnitXML, reportPath } from "../report.js";
import { addErrorCount, stopIterationOnFail } from "../errorhandler.js";
const scopes = "altinn:serviceowner/notifications.create";
const smsRecipient = __ENV.smsRecipient.toLowerCase();
export const options = {
  thresholds: {
    errors: ["count<1"],
  },
};

export function setup() {
  var token = setupToken.getAltinnTokenForOrg(scopes);
  var sendersReference = uuidv4();

  var smsOrderRequest = {
    senderNumber: "Altinn (test)",
    body: "This is an automated test: " + randomString(30) + " " + randomString(30),
    recipients: [
      {
        mobileNumber: smsRecipient,
      },
    ],
    sendersReference: sendersReference,
  };

  const runFullTestSet = __ENV.runFullTestSet
    ? __ENV.runFullTestSet.toLowerCase().includes("true")
    : false;

  var data = {
    runFullTestSet: runFullTestSet,
    token: token,
    smsOrderRequest: smsOrderRequest,
    sendersReference: sendersReference,
  };

  return data;
}

// 01 - POST sms notification order request
function TC01_PostSmsNotificationOrderRequest(data) {
  var response, success;

  response = ordersApi.postSmsNotificationOrder(
    JSON.stringify(data.smsOrderRequest),
    data.token
  );
  var selfLink = response.headers["Location"];

  success = check(response, {
    "POST sms notification order request. Status is 202 Accepted": (r) =>
      r.status === 202,
    "POST sms notification order request. Location header providedStatus is 202 Accepted":
      (r) => selfLink,
    "POST sms notification order request. Response body is not an empty string":
      (r) => r.body,
  });

  addErrorCount(success);

  if (!success) {
    stopIterationOnFail(success);
  }

  return selfLink;
}

// 02 - GET notification order by id
function TC02_GetNotificationOrderById(data, selfLink, orderId) {
  var response, success;
  response = ordersApi.getByUrl(selfLink, data.token);

  success = check(response, {
    "GET notification order by id. Status is 200 OK": (r) => r.status === 200,
  });

  addErrorCount(success);
  if (!success) {
    // only continue to parse and check content if success response code
    stopIterationOnFail(success);
  }

  success = check(JSON.parse(response.body), {
    "GET notification order by id. Id property is a match": (order) =>
      order.id === orderId,
    "GET notification order by id. Creator property is a match": (order) =>
      order.creator === "ttd",
  });
  addErrorCount(success);
}

// 03 - GET notification order by senders reference
function TC03_GetNotificationOrderBySendersReference(data) {
  var response, success;

  response = ordersApi.getBySendersReference(data.sendersReference, data.token);

  success = check(response, {
    "GET notification order by senders reference. Status is 200 OK": (r) =>
      r.status === 200,
  });

  addErrorCount(success);
  if (!success) {
    // only continue to parse and check content if success response code
    stopIterationOnFail(success);
  }

  success = check(JSON.parse(response.body), {
    "GET notification order by senders reference. Count is equal to 1": (
      orderList
    ) => orderList.count === 1,
    "GET notification order by senders reference. Order list contains one element":
      (orderList) =>
        Array.isArray(orderList.orders) && orderList.orders.length == 1,
  });
}

// 04 - GET notification order with status
function TC04_GetNotificationOrderWithStatus(data, orderId) {
  var response, success;

  response = ordersApi.getWithStatus(orderId, data.token);
  success = check(response, {
    "GET notification order with status. Status is 200 OK": (r) =>
      r.status === 200,
  });

  addErrorCount(success);
  if (!success) {
    // only continue to parse and check content if success response code
    stopIterationOnFail(success);
  }

  success = check(JSON.parse(response.body), {
    "GET notification order with status. Id property is a match": (order) =>
      order.id === orderId,
    "GET notification order with status. NotificationChannel is sms": (order) =>
      order.notificationChannel === "Sms",
    "GET notification order with status. ProcessingStatus is defined": (
      order
    ) => order.processingStatus,
  });
  addErrorCount(success);
}

// 05 - GET sms notification summary
function TC05_GetSmsNotificationSummary(data, orderId) {
  var response, success;

  response = notificationsApi.getSmsNotifications(orderId, data.token);
  success = check(response, {
    "GET sms notifications. Status is 200 OK": (r) => r.status === 200,
  });

  addErrorCount(success);
  if (!success) {
    // only continue to parse and check content if success response code
    stopIterationOnFail(success);
  }

  success = check(JSON.parse(response.body), {
    "GET sms notifications. OrderId property is a match": (
      notificationSummary
    ) => notificationSummary.orderId === orderId,
  });
}

/*
 * 01 - POST sms notification order request
 * 02 - GET notification order by id
 * 03 - GET notification order by senders reference
 * 04 - GET notification order with status
 * 05 - GET sms notification summary
 */
export default function (data) {
  try {
    if (data.runFullTestSet) {
      var selfLink = TC01_PostSmsNotificationOrderRequest(data);
      let id = selfLink.split("/").pop();
      TC02_GetNotificationOrderById(data, selfLink, id);
      TC03_GetNotificationOrderBySendersReference(data);
      TC04_GetNotificationOrderWithStatus(data, id);
      TC05_GetSmsNotificationSummary(data, id);
    } else {
      // Limited test set for use case tests
      var selfLink = TC01_PostSmsNotificationOrderRequest(data);
      let id = selfLink.split("/").pop();
      TC05_GetSmsNotificationSummary(data, id);
    }
  } catch (error) {
    addErrorCount(false);
    throw error;
  }
}

/*
export function handleSummary(data) {
  let result = {};
  result[reportPath("events.xml")] = generateJUnitXML(data, "events");

  return result;
}
*/
