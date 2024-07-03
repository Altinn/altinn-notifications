/*
    Test script of platform notifications api with org token
    Command:
    docker-compose run k6 run /src/tests/orders_org_no.js `
    -e tokenGeneratorUserName=autotest `
    -e tokenGeneratorUserPwd=*** `
    -e mpClientId=*** `
    -e mpKid=altinn-usecase-events `
    -e encodedJwk=*** `
    -e env=*** `
    -e orgNoRecipient=*** `
    -e resourceId=*** `
    -e runFullTestSet=true

    For use case tests omit environment variable runFullTestSet or set value to false
*/

import { check, sleep } from "k6";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { stopIterationOnFail } from "../errorhandler.js";

import * as setupToken from "../setup.js";
import * as notificationsApi from "../api/notifications/notifications.js";
import * as ordersApi from "../api/notifications/orders.js";

const emailOrderRequestJson = JSON.parse(
  open("../data/orders/01-email-request.json")
);

const scopes = "altinn:serviceowner/notifications.create";
const orgNoRecipient = __ENV.orgNoRecipient.toLowerCase();
const resourceId = __ENV.resourceId;

export const options = {
  thresholds: {
    // Checks rate should be 100%. Raise error if any check has failed.
    checks: ['rate>=1']
  }
};


export function setup() {
  var token = setupToken.getAltinnTokenForOrg(scopes);
  var sendersReference = uuidv4();

  var emailOrderRequest = emailOrderRequestJson;
  emailOrderRequest.recipients = [
    {
      organizationNumber: orgNoRecipient
    }
  ];
  emailOrderRequest.sendersReference = sendersReference;
  emailOrderRequest.resourceId = resourceId;

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

// 01 - POST email notification order request
function TC01_PostEmailNotificationOrderRequest(data) {
  var response, success;

  response = ordersApi.postEmailNotificationOrder(
    JSON.stringify(data.emailOrderRequest),
    data.token
  );
  var selfLink = response.headers["Location"];

  success = check(response, {
    "POST email notification order request. Status is 202 Accepted": (r) =>
      r.status === 202,
    "POST email notification order request. Location header providedStatus is 202 Accepted":
      (r) => selfLink,
    "POST email notification order request. Recipient lookup was successful":
      (r) => JSON.parse(r.body).recipientLookup.status == 'Success'
  });

  stopIterationOnFail("POST email notification order request failed", success);
  
  return selfLink;
}

// 02 - GET email notification summary
function TC02_GetEmailNotificationSummary(data, orderId) {
  var response, success;

  response = notificationsApi.getEmailNotifications(orderId, data.token);
  success = check(response, {
    "GET email notifications. Status is 200 OK": (r) => r.status === 200,
  });

  stopIterationOnFail("GET email notification summary request failed", success);

  success = check(JSON.parse(response.body), {
    "GET email notifications. OrderId property is a match": (
      notificationSummary
    ) => notificationSummary.orderId === orderId,
  });
}

// 03 - GET email notifications summary again after one minute for verification
function TC03_GetEmailNotificationSummaryAgainAfterOneMinuteForVerification(data, orderId) {
  sleep(60); // Waiting 1 minute for the notifications to be generated
  var response, success;
  response = notificationsApi.getEmailNotifications(orderId, data.token);
  success = check(response, {
    "GET email notifications summary again after one minute for verification. Status is 200 OK": (r) => r.status === 200,
  });

  stopIterationOnFail("Get email notification summary request after a delay failed", success);
  
  success = check(JSON.parse(response.body), {
    "GET email notifications summary again after one minute for verification. OrderId property is a match": (
      notificationSummary
    ) => notificationSummary.orderId === orderId,
    "GET email notifications summary again after one minute for verification. At least one notification has been generated": (
      notificationSummary
    ) => notificationSummary.generated > 0,
    "GET email notifications summary again after one minute for verification. At least one notification is in the notifications array": (
      notificationSummary
    ) => notificationSummary.notifications.length > 0,
    "GET email notifications summary again after one minute for verification. Recipient organization number is a match": (
      notificationSummary
    ) => notificationSummary.notifications[0].recipient.organizationNumber === data.emailOrderRequest.recipients[0].organizationNumber,
    "GET email notifications summary again after one minute for verification. Recipient email address found in the contact lookup for the given organization number": (
      notificationSummary
    ) => notificationSummary.notifications[0].recipient.emailAddress.length > 0,
  });
}

/*
 * 01 - POST email notification order request
 * 02 - GET email notification summary
 * 03 - GET email notifications summary again after one minute for verification
 */
export default function (data) {
  var selfLink = TC01_PostEmailNotificationOrderRequest(data);
  let id = selfLink.split("/").pop();

  if (data.runFullTestSet) {
    TC02_GetEmailNotificationSummary(data, id);
    TC03_GetEmailNotificationSummaryAgainAfterOneMinuteForVerification(data, id);
  } else {
    TC02_GetEmailNotificationSummary(data, id);
  }
}
