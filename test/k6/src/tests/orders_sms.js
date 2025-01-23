/*
    Test script for Platform Notifications API using an organization token.

    Command:
    podman compose run k6 run /src/tests/orders_sms.js \
        -e tokenGeneratorUserName=autotest \
        -e tokenGeneratorUserPwd=*** \
        -e mpClientId=*** \
        -e mpKid=altinn-usecase-events \
        -e encodedJwk=*** \
        -e env=*** \
        -e smsRecipient=*** \
        -e runFullTestSet=true

    Notes:
    - To run only use case tests, omit `runFullTestSet` or set it to `false`.

    Command syntax for different shells:
    - Bash: Use the command as written above.
    - PowerShell: Replace `\` with a backtick (`` ` ``) at the end of each line.
    - Command Prompt (cmd.exe): Replace `\` with `^` at the end of each line.
*/

import { check } from "k6";
import { stopIterationOnFail } from "../errorhandler.js";
import { randomString, uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

import * as setupToken from "../setup.js";
import * as ordersApi from "../api/notifications/orders.js";
import * as notificationsApi from "../api/notifications/notifications.js";

const scopes = "altinn:serviceowner/notifications.create";

const smsRecipient = __ENV.smsRecipient ? __ENV.smsRecipient.toLowerCase() : null;

export const options = {
    thresholds: {
        // Checks rate should be 100%. Raise error if any check has failed.
        checks: ['rate>=1']
    }
};

/**
 * Initialize test data.
 * @returns {Object} The data object containing token, runFullTestSet, sendersReference, and smsOrderRequest.
 */
export function setup() {
    const sendersReference = uuidv4();
    const token = setupToken.getAltinnTokenForOrg(scopes);

    const smsOrderRequest = {
        senderNumber: "Altinn",
        body: "This is an automated test: " + randomString(30) + " " + randomString(30),
        recipients: [
            {
                mobileNumber: smsRecipient,
            },
        ],
        sendersReference: sendersReference
    };

    const runFullTestSet = __ENV.runFullTestSet
        ? __ENV.runFullTestSet.toLowerCase().includes("true")
        : false;

    return {
        token,
        runFullTestSet,
        sendersReference,
        smsOrderRequest,
    };
}

/**
 * Posts an SMS notification order request.
 * @param {Object} data - The data object containing smsOrderRequest and token.
 * @returns {string} The selfLink of the created order.
 */
function postSmsNotificationOrderRequest(data) {
    const response = ordersApi.postSmsNotificationOrder(
        JSON.stringify(data.smsOrderRequest),
        data.token
    );

    const success = check(response, {
        "POST SMS notification order request. Status is 202 Accepted": (r) => r.status === 202
    });

    stopIterationOnFail("POST SMS notification order request failed", success);

    const selfLink = response.headers["Location"];

    check(response, {
        "POST SMS notification order request. Location header provided": (_) => selfLink,
        "POST SMS notification order request. Response body is not an empty string": (r) => r.body
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
        "GET notification order by senders reference. Order list contains one element": (orderList) => Array.isArray(orderList.orders) && orderList.orders.length == 1,
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
        "GET notification order with status. NotificationChannel is sms": (order) => order.notificationChannel === "Sms",
        "GET notification order with status. ProcessingStatus is defined": (order) => order.processingStatus,
    });
}

/**
 * Gets the SMS notification summary.
 * @param {Object} data - The data object containing token.
 * @param {string} orderId - The ID of the order.
 */
function getSmsNotificationSummary(data, orderId) {
    const response = notificationsApi.getSmsNotifications(orderId, data.token);

    check(response, {
        "GET SMS notifications. Status is 200 OK": (r) => r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET SMS notifications. OrderId property is a match": (notificationSummary) => notificationSummary.orderId === orderId,
    });
}

/**
 * The main function to run the test.
 * @param {Object} data - The data object containing runFullTestSet and other test data.
 */
export default function (data) {
    const selfLink = postSmsNotificationOrderRequest(data);
    const id = selfLink.split("/").pop();

    if (data.runFullTestSet) {
        getNotificationOrderById(data, selfLink, id);
        getNotificationOrderBySendersReference(data);
        getNotificationOrderWithStatus(data, id);
    }

    getSmsNotificationSummary(data, id);
}
