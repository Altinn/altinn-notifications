/*
    Test script of platform notifications API with org token
    Command:
    podman compose run k6 run /src/tests/orders_org_no.js \
    -e tokenGeneratorUserName=autotest \
    -e tokenGeneratorUserPwd=*** \
    -e mpClientId=*** \
    -e mpKid=altinn-usecase-events \
    -e encodedJwk=*** \
    -e env=*** \
    -e orgNoRecipient=*** \
    -e resourceId=*** \
    -e runFullTestSet=true

    For use case tests omit environment variable runFullTestSet or set value to false
*/

import { check, sleep } from "k6";
import { stopIterationOnFail } from "../errorhandler.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

import * as setupToken from "../setup.js";
import * as ordersApi from "../api/notifications/orders.js";
import * as notificationsApi from "../api/notifications/notifications.js";

const emailOrderRequestJson = JSON.parse(
    open("../data/orders/01-email-request.json")
);

const scopes = "altinn:serviceowner/notifications.create";

const resourceId = __ENV.resourceId;
const orgNoRecipient = __ENV.orgNoRecipient ? __ENV.orgNoRecipient.toLowerCase() : null;

export const options = {
    thresholds: {
        // Checks rate should be 100%. Raise error if any check has failed.
        checks: ['rate>=1']
    }
};

/**
 * Initialize test data.
 * @returns {Object} The data object containing token, runFullTestSet, sendersReference, and emailOrderRequest.
 */
export function setup() {
    const sendersReference = uuidv4();
    const token = setupToken.getAltinnTokenForOrg(scopes);

    const emailOrderRequest = { ...emailOrderRequestJson, sendersReference, resourceId, recipients: [] };

    if (orgNoRecipient) {
        emailOrderRequest.recipients.push({ organizationNumber: orgNoRecipient });
    }

    const runFullTestSet = __ENV.runFullTestSet
        ? __ENV.runFullTestSet.toLowerCase().includes("true")
        : false;

    return {
        token,
        runFullTestSet,
        sendersReference,
        emailOrderRequest,
    };
}

/**
 * Posts an email notification order request.
 * @param {Object} data - The data object containing emailOrderRequest and token.
 * @returns {string} The selfLink of the created order.
 */
function postEmailNotificationOrderRequest(data) {
    const response = ordersApi.postEmailNotificationOrder(
        JSON.stringify(data.emailOrderRequest),
        data.token
    );

    const success = check(response, {
        "POST email notification order request. Status is 202 Accepted": (r) => r.status === 202,
    });

    stopIterationOnFail("POST email notification order request failed", success);

    const selfLink = response.headers["Location"];

    check(response, {
        "POST email notification order request. Location header provided": (r) => selfLink,
        "POST email notification order request. Recipient lookup was successful": (r) => JSON.parse(r.body).recipientLookup.status == 'Success',
    });

    return selfLink;
}

/**
 * Gets the email notification summary.
 * @param {Object} data - The data object containing token.
 * @param {string} orderId - The ID of the order.
 */
function getEmailNotificationSummary(data, orderId) {
    const response = notificationsApi.getEmailNotifications(orderId, data.token);

    check(response, {
        "GET email notifications. Status is 200 OK": (r) => r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET email notifications. OrderId property is a match": (notificationSummary) => notificationSummary.orderId === orderId,
    });
}

/**
 * Gets the email notifications summary again after one minute for verification.
 * @param {Object} data - The data object containing token and emailOrderRequest.
 * @param {string} orderId - The ID of the order.
 */
function getEmailNotificationSummaryAgainAfterOneMinuteForVerification(data, orderId) {
    sleep(60); // Waiting 1 minute for the notifications to be generated

    const response = notificationsApi.getEmailNotifications(orderId, data.token);

    check(response, {
        "GET email notifications summary again after one minute for verification. Status is 200 OK": (r) => r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET email notifications summary again after one minute for verification. OrderId property is a match": (notificationSummary) => notificationSummary.orderId === orderId,
        "GET email notifications summary again after one minute for verification. At least one notification has been generated": (notificationSummary) => notificationSummary.generated > 0,
        "GET email notifications summary again after one minute for verification. At least one notification is in the notifications array": (notificationSummary) => notificationSummary.notifications.length > 0,
        "GET email notifications summary again after one minute for verification. Recipient organization number is a match": (notificationSummary) => notificationSummary.notifications[0].recipient.organizationNumber === data.emailOrderRequest.recipients[0].organizationNumber,
        "GET email notifications summary again after one minute for verification. Recipient email address found in the contact lookup for the given organization number": (notificationSummary) => notificationSummary.notifications[0].recipient.emailAddress.length > 0,
    });
}

/**
 * The main function to run the test.
 * @param {Object} data - The data object containing runFullTestSet and other test data.
 */
export default function (data) {
    const selfLink = postEmailNotificationOrderRequest(data);
    const id = selfLink.split("/").pop();

    getEmailNotificationSummary(data, id);

    if (data.runFullTestSet) {
        getEmailNotificationSummaryAgainAfterOneMinuteForVerification(data, id);
    }
}
