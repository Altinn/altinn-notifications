/*
    Test script of platform notifications api with application owner token
    Command:
    podman compose run k6 run /src/tests/orders_email.js `
    -e tokenGeneratorUserName=autotest `
    -e tokenGeneratorUserPwd={the password to access the token generator}`
    -e mpClientId={the id of an integration defined in maskinporten} `
    -e mpKid={the key id of the JSON web key used to sign the maskinporten token request} `
    -e encodedJwk={the encoded JSON web key used to sign the maskinporten token request} `
    -e env={environment: at21, at22, at23, at24, tt02, prod}`
    -e emailRecipient={an email address to add as a notification recipient}`
    -e ninRecipient={a national identity number of a person to include as a notification recipient}`
    -e runFullTestSet=true

    For use case tests omit environment variable runFullTestSet or set value to false
*/

import { check } from "k6";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { stopIterationOnFail } from "../errorhandler.js";

import * as setupToken from "../setup.js";
import * as notificationsApi from "../api/notifications/notifications.js";
import * as ordersApi from "../api/notifications/orders.js";

const emailOrderRequestJson = open("../data/orders/01-email-request.json");

export const options = {
  thresholds: {
    // Checks rate should be 100%. Raise error if any check has failed.
    checks: ['rate>=1']
  }
};

export function setup() {
  const token = setupToken.getAltinnTokenForOrg("altinn:serviceowner/notifications.create");
  const sendersReference = uuidv4();

  var emailOrderRequest = JSON.parse(emailOrderRequestJson);
  emailOrderRequest.sendersReference = sendersReference;
  emailOrderRequest.recipients = [
    {
      emailAddress: __ENV.emailRecipient.toLowerCase(),
    },
    {
      nationalIdentityNumber: __ENV.ninRecipient.toLowerCase()
    }
  ];

  const runFullTestSet = __ENV.runFullTestSet
    ? __ENV.runFullTestSet.toLowerCase().includes("true")
    : false;

  var data = {
    runFullTestSet: runFullTestSet,
    token: token,
    emailOrderRequest: emailOrderRequest,
    sendersReference: sendersReference,
  };

  return data;
}

function TC01_PostEmailNotificationOrderRequest(data) {
  let success;

  const response = ordersApi.postEmailNotificationOrder(
    JSON.stringify(data.emailOrderRequest),
    data.token
  );

  success = check(response, {
    "POST email notification order request. Status is 202 Accepted": 
      (r) => r.status === 202
  });

  stopIterationOnFail("POST email notification order request failed", success);

  success = check(response, {
    "POST email notification order request. Location header providedStatus is 202 Accepted":
      (r) => r.headers["Location"],
    "POST email notification order request. Recipient lookup was successful":
      (r) => JSON.parse(r.body).recipientLookup.status == 'Success'
  });

  return response.headers["Location"];
}

function TC02_GetNotificationOrderById(data, selfLink, orderId) {
  let success;

  const response = ordersApi.getByUrl(selfLink, data.token);

  success = check(response, {
    "GET notification order by id. Status is 200 OK": 
      (r) => r.status === 200,
  });

  stopIterationOnFail("GET notification order by id request failed", success);

  success = check(JSON.parse(response.body), {
    "GET notification order by id. Id property is a match": 
      (order) => order.id === orderId,
    "GET notification order by id. Creator property is a match": 
      (order) => order.creator === "ttd",
  });
}

function TC03_GetNotificationOrderBySendersReference(data) {
  let success;

  const response = ordersApi.getBySendersReference(data.sendersReference, data.token);

  success = check(response, {
    "GET notification order by senders reference. Status is 200 OK": 
      (r) => r.status === 200,
  });

  stopIterationOnFail("GET notification order by senders reference request failed", success);

  success = check(JSON.parse(response.body), {
    "GET notification order by senders reference. Count is equal to 1": 
      (orderList) => orderList.count === 1,
    "GET notification order by senders reference. Orderlist contains one element":
      (orderList) => Array.isArray(orderList.orders) && orderList.orders.length == 1,
  });
}

function TC04_GetNotificationOrderWithStatus(data, orderId) {
  let success;

  const response = ordersApi.getWithStatus(orderId, data.token);

  success = check(response, {
    "GET notification order with status. Status is 200 OK": 
      (r) => r.status === 200,
  });

  stopIterationOnFail("GET notification order with status request failed", success);

  success = check(JSON.parse(response.body), {
    "GET notification order with status. Id property is a match": 
      (order) => order.id === orderId,
    "GET notification order with status. NotificationChannel is email": 
      (order) => order.notificationChannel === "Email",
    "GET notification order with status. ProcessingStatus is defined": 
      (order) => order.processingStatus,
  });
}

function TC05_GetEmailNotificationSummary(data, orderId) {
  let success;

  const response = notificationsApi.getEmailNotifications(orderId, data.token);

  success = check(response, {
    "GET email notifications. Status is 200 OK": (r) => r.status === 200,
  });

  stopIterationOnFail("GET email notifications request failed", success);

  success = check(JSON.parse(response.body), {
    "GET email notifications. OrderId property is a match": 
      (notificationSummary) => notificationSummary.orderId === orderId,
  });
}

export default function (data) {
  var selfLink = TC01_PostEmailNotificationOrderRequest(data);
  let id = selfLink.split("/").pop();

  if (data.runFullTestSet) {
    TC02_GetNotificationOrderById(data, selfLink, id);
    TC03_GetNotificationOrderBySendersReference(data);
    TC04_GetNotificationOrderWithStatus(data, id);
  }

  TC05_GetEmailNotificationSummary(data, id);
}
