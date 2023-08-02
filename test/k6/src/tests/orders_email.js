/*
    Test script of platform notifications api with org token
    Command:
    docker-compose run k6 run /src/tests/orders_email.js `
    -e tokenGeneratorUserName=autotest `
    -e tokenGeneratorUserPwd=*** `
    -e env=*** `
    -e toAddress=*** `
    -e runFullTestSet=true

    For use case tests omit environment variable runFullTestSet or set value to false
*/

import { check } from "k6";
import * as setupToken from "../setup.js";
import * as notificationsApi from "../api/notifications.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
const orderRequestJson = JSON.parse(
  open("../data/orders/01-email-request.json")
);
import { generateJUnitXML, reportPath } from "../report.js";
import { addErrorCount, stopIterationOnFail } from "../errorhandler.js";
const scopes = "none";
const toAddress = __ENV.toAddress.toLowerCase();

export const options = {
  thresholds: {
    errors: ["count<1"],
  },
};

export function setup() {
  var token = setupToken.getAltinnTokenForOrg(scopes);
  var sendersReference = uuidv4();

  var orderRequest = orderRequestJson;
  orderRequest.recipients = [
    {
      emailAddress: toAddress,
    },
  ];
  orderRequest.sendersReference = sendersReference;

  const runFullTestSet = __ENV.runFullTestSet
    ? __ENV.runFullTestSet.toLowerCase().includes("true")
    : false;

  var data = {
    runFullTestSet: runFullTestSet,
    token: token,
    orderRequest: orderRequest,
    sendersReference: sendersReference,
  };

  return data;
}

// 01 - POST email notification order request
function TC01_PostEmailNotificationOrderRequest(data) {
  var response, success;

  response = notificationsApi.postEmailNotificationOrder(
    JSON.stringify(data.orderRequest),
    data.token
  );
  var selfLink = response.headers["Location"];

  success = check(response, {
    "POST valid email notification order request. Status is 202 Accepted":
      (r) => r.status === 202,
    "POST valid email notification order request. Location header providedStatus is 202 Accepted":
      (r) => selfLink,
    "POST valid email notification order request. Response body is not an empty string":
      (r) => r.body
  });

  addErrorCount(success);

  if (!success) {
    stopIterationOnFail(success);
  }

  return selfLink;
}

// 02 - GET notification order by id
function TC02_GetNotificationOrderById(data, selfLink) {
  var response, success;

  response = notificationsApi.getOrderByUrl(selfLink, data.token);

  success = check(response, {
    "GET notification order by id. Status is 200 OK": (r) => r.status === 200
    });

  addErrorCount(success);
}

// 03 - GET notification order by senders reference
function TC03_GetNotificationOrderBySendersReference(data) {
  var response, success;

  response = notificationsApi.getOrderBySendersReference(
    data.sendersReference,
    data.token
  );

  success = check(response, {
    "GET notification order by senders reference. Status is 200 OK": (r) =>
      r.status === 200,
  });

  addErrorCount(success);
}

/*
 * 01 - POST email notification order request
 * 02 - GET notification order by id
 * 03 - GET notification order by senders reference
 */
export default function (data) {
  try {
    if (data.runFullTestSet) {

      var selfLink = TC01_PostEmailNotificationOrderRequest(data);
      TC02_GetNotificationOrderById(data, selfLink);
      TC03_GetNotificationOrderBySendersReference(data);
    } else {
      // Limited test set for use case tests
      var selfLink = TC01_PostEmailNotificationOrderRequest(data);
      TC02_GetNotificationOrderById(data, selfLink);
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
