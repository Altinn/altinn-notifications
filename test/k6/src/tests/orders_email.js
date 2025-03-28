/*
    Test script for Platform Notifications API using an application owner token.

    Command:
    podman compose run k6 run /src/tests/orders_email.js \
    -e tokenGeneratorUserName={the user name to access the token generator} \
    -e tokenGeneratorUserPwd={the password to access the token generator} \
    -e mpClientId={the id of an integration defined in maskinporten} \
    -e mpKid={the key id of the JSON web key used to sign the maskinporten token request} \
    -e encodedJwk={the encoded JSON web key used to sign the maskinporten token request} \
    -e env={environment: at22, at23, at24, tt02, prod} \
    -e emailRecipient={an email address to add as a notification recipient} \
    -e ninRecipient={a national identity number of a person to include as a notification recipient} \
    -e subscriptionKey={the subscription key with access to the automated tests product} \
    -e runFullTestSet=true

    Notes:
    - To run only use case tests, omit `runFullTestSet` or set it to `false`.
    - The `subscriptionKey` is required and can be retrieved from API management in Azure.

    Command syntax for different shells:
    - Bash: Use the command as written above.
    - PowerShell: Replace `\` with a backtick (`` ` ``) at the end of each line.
    - Command Prompt (cmd.exe): Replace `\` with `^` at the end of each line.
*/

import { check, sleep } from "k6";
import { notifications } from "../config.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

import * as setupToken from "../setup.js";
import * as ordersApi from "../api/notifications/orders.js";
import * as notificationsApi from "../api/notifications/notifications.js";

const emailOrderRequestJson = JSON.parse(
    open("../data/orders/01-email-request.json")
);

const scopes = "altinn:serviceowner/notifications.create";
const ninRecipient = __ENV.ninRecipient ? __ENV.ninRecipient.toLowerCase() : null;
const emailRecipient = __ENV.emailRecipient ? __ENV.emailRecipient.toLowerCase() : null;

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

    const emailOrderRequest = { ...emailOrderRequestJson, sendersReference, conditionEndpoint: notifications.conditionCheck(true), recipients: [] };

    if (ninRecipient) {
        emailOrderRequest.recipients.push({ nationalIdentityNumber: ninRecipient });
    }

    if (emailRecipient) {
        emailOrderRequest.recipients.push({ emailAddress: emailRecipient });
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
        "POST email notification order request. Status is 202 Accepted": (r) => r.status === 202
    });

    stopIterationOnFail("POST email notification order request failed", success);

    const selfLink = response.headers["Location"];

    check(response, {
        "POST email notification order request. Location header provided": (_) => selfLink,
        "POST email notification order request. Recipient lookup was successful": (r) => JSON.parse(r.body).recipientLookup.status == 'Success'
    });

    return selfLink;
}

/**
 * Gets a notification order by its ID.
 * @param {Object} data - The data object containing token.
 * @param {string} selfLink - The selfLink of the order.
 * @param {string} orderId - The order identifier.
 */
function getNotificationOrderById(data, selfLink, orderId) {
    const response = ordersApi.getByUrl(selfLink, data.token);

    check(response, {
        "GET notification order by id. Status is 200 OK": (r) => r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET notification order by id. Id property is a match": (order) => order.id === orderId,
        "GET notification order by id. Creator property is a match": (order) => order.creator === "ttd",
    });
}

/**
 * Gets a notification order by the sender's reference.
 * @param {Object} data - The data object containing sendersReference and token.
 */
function getNotificationOrderBySendersReference(data) {
    const response = ordersApi.getBySendersReference(data.sendersReference, data.token);

    check(response, {
        "GET notification order by senders reference. Status is 200 OK": (r) => r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET notification order by senders reference. Count is equal to 1": (orderList) => orderList.count === 1,
        "GET notification order by senders reference. Orderlist contains one element": (orderList) => Array.isArray(orderList.orders) && orderList.orders.length == 1,
    });
}

/**
 * Gets a notification order with its status.
 * @param {Object} data - The data object containing token.
 * @param {string} orderId - The ID of the order.
 */
function getNotificationOrderWithStatus(data, orderId) {
    const response = ordersApi.getWithStatus(orderId, data.token);

    check(response, {
        "GET notification order with status. Status is 200 OK": (r) => r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET notification order with status. Id property is a match": (order) => order.id === orderId,
        "GET notification order with status. NotificationChannel is email": (order) => order.notificationChannel === "Email",
        "GET notification order with status. ProcessingStatus is defined": (order) => order.processingStatus,
    });
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
 * Posts an email notification order request with a negative condition check.
 * @param {Object} data - The data object containing emailOrderRequest and token.
 */
function postEmailNotificationOrderWithNegativeConditionCheck(data) {
    const emailOrderRequest = { ...data.emailOrderRequest, conditionEndpoint: notifications.conditionCheck(false) };

    let response = ordersApi.postEmailNotificationOrder(
        JSON.stringify(emailOrderRequest),
        data.token
    );

    let success = check(response, {
        "POST email notification order request with condition. Status is 202 Accepted": (r) => r.status === 202
    });

    stopIterationOnFail("POST email notification order request with condition check failed", success);

    sleep(60); // Waiting 1 minute for the notifications to be generated

    const order = JSON.parse(response.body);

    response = ordersApi.getWithStatus(order.orderId, data.token);
    check(response, {
        "GET notification order with condition check status after one minute. Status is 200 OK": (r) => r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET notification order with condition check status. Status is condition not met": (order) => order.processingStatus.status == "SendConditionNotMet",
    });
}

/**
 * The main function to run the test.
 * @param {Object} data - The data object containing runFullTestSet and other test data.
 */
export default function (data) {
    const selfLink = postEmailNotificationOrderRequest(data);
    const id = selfLink.split("/").pop();

    if (data.runFullTestSet) {
        getNotificationOrderById(data, selfLink, id);
        getNotificationOrderBySendersReference(data);
        getNotificationOrderWithStatus(data, id);
        getEmailNotificationSummary(data, id);
        postEmailNotificationOrderWithNegativeConditionCheck(data);
    } else {
        getEmailNotificationSummary(data, id);
    }
}
