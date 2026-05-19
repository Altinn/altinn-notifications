/*
    Test script for Platform Notifications API using an application owner token.

    Command:
    podman compose run k6 run /src/tests/orders-email.js \
    --secret-source=file=/.secrets \
    -e altinn_env={environment: at22, at23, at24, tt02, prod} \
    -e emailRecipient={an email address to add as a notification recipient} \
    -e ninRecipient={a national identity number of a person to include as a notification recipient} \
    -e runFullTestSet=true

    Notes:
    - To run only use case tests, omit `runFullTestSet` or set it to `false`.
    - Setting `subscriptionKey` in .secrets is required - can be retrieved from Azure APIM.

    Command syntax for different shells:
    - Bash: Use the command as written above.
    - PowerShell: Replace `\` with a backtick (`` ` ``) at the end of each line.
    - Command Prompt (cmd.exe): Replace `\` with `^` at the end of each line.
*/

import { check, sleep } from "k6";
import { notifications } from "../config.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

import { getFromSecretSource } from "..secret-reader.js";
import * as setupToken from "../setup.js";
import { getEmailRecipient } from "../shared/functions.js";
import * as ordersApi from "../api/notifications/orders.js";
import { scopes, ninRecipient } from "../shared/variables.js";
import * as notificationsApi from "../api/notifications/notifications.js";
import { post_mail_order, get_mail_notifications, setEmptyThresholds } from "./threshold-labels.js";
import { getNotificationOrderById, getNotificationOrderBySendersReference, getNotificationOrderWithStatus } from "../api/notifications/get-notification-orders.js";

const labels = [post_mail_order, get_mail_notifications];

const emailOrderRequestJson = JSON.parse(
    open("../data/orders/01-email-request.json")
);

const emailRecipient = getEmailRecipient();

export const options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {
        // Checks rate should be 100%. Raise error if any check has failed.
        checks: ['rate>=1']
    }
};
setEmptyThresholds(labels, options);

/**
 * Initialize test data.
 * @returns {Object} The data object containing token, runFullTestSet, sendersReference, and emailOrderRequest.
 */
export async function setup() {
    const sendersReference = uuidv4();
    const token = await setupToken.getAltinnTokenForOrg(scopes);
    const subscriptionKey = await getFromSecretSource("subscriptionKey");

    const emailOrderRequest = { ...emailOrderRequestJson, sendersReference, conditionEndpoint: notifications.conditionCheck(true, subscriptionKey), recipients: [] };

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
        subscriptionKey
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
        data.token,
        post_mail_order
    );

    const success = check(response, {
        "POST email notification order request. Status is 202 Accepted": (r) => r.status === 202
    });

    stopIterationOnFail("POST email notification order request failed", success);

    const selfLink = response.headers["Location"];

    check(response, {
        "POST email notification order request. Location header provided": (_) => selfLink,
    });

    if (ninRecipient) {
        check(response, {
            "POST email notification order request. Recipient lookup was successful": (r) => JSON.parse(r.body).recipientLookup.status == 'Success'
        });
    }
    return selfLink;
}


/**
 * Gets the email notification summary.
 * @param {Object} data - The data object containing token.
 * @param {string} orderId - The ID of the order.
 */
function getEmailNotificationSummary(data, orderId) {
    const response = notificationsApi.getEmailNotifications(orderId, data.token, get_mail_notifications);

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
    const emailOrderRequest = { ...data.emailOrderRequest, conditionEndpoint: notifications.conditionCheck(false, data.subscriptionKey) };

    let response = ordersApi.postEmailNotificationOrder(
        JSON.stringify(emailOrderRequest),
        data.token,
        post_mail_order
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
export default function runTests(data) {
    const selfLink = postEmailNotificationOrderRequest(data);
    const id = selfLink.split("/").pop();

    if (data.runFullTestSet) {
        getNotificationOrderById(data, selfLink, id);
        getNotificationOrderBySendersReference(data);
        getNotificationOrderWithStatus(data, id, "Email");
        getEmailNotificationSummary(data, id);
        postEmailNotificationOrderWithNegativeConditionCheck(data);
    } else {
        getEmailNotificationSummary(data, id);
    }
}
