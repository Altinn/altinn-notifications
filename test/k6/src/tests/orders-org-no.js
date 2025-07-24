/*
    Test script for Platform Notifications API using an organization token.

    Command:
    podman compose run k6 run /src/tests/orders-org-no.js \
        -e tokenGeneratorUserName={the user name to access the token generator} \
        -e tokenGeneratorUserPwd={the password to access the token generator} \
        -e mpClientId={the id of an integration defined in maskinporten} \
        -e mpKid={the key id of the JSON web key used to sign the maskinporten token request} \
        -e encodedJwk={the encoded JSON web key used to sign the maskinporten token request} \
        -e env={environment: at22, at23, at24, tt02, prod} \
        -e orgNoRecipient={an organization number to include as a notification recipient} \
        -e resourceId={the resource ID associated with the notification order} \
        -e runFullTestSet=true

    Notes:
    - To run only use case tests, omit `runFullTestSet` or set it to `false`.
    - The `resourceId` is required and should be a valid resource identifier.
    - The `orgNoRecipient` is required for sending notifications to an organization, _unless_ set environment = yt01 .

    Command syntax for different shells:
    - Bash: Use the command as written above.
    - PowerShell: Replace `\` with a backtick (`` ` ``) at the end of each line.
    - Command Prompt (cmd.exe): Replace `\` with `^` at the end of each line.
*/

import { check, sleep } from "k6";
import { stopIterationOnFail } from "../errorhandler.js";
import { randomString, randomItem, uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

import * as setupToken from "../setup.js";
import { orgNosYt01 } from "../data/orgnos.js";
import * as ordersApi from "../api/notifications/orders.js";
import * as notificationsApi from "../api/notifications/notifications.js";
import { post_mail_order, get_mail_notifications, post_sms_order, get_sms_notifications, setEmptyThresholds } from "./threshold-labels.js";
import { scopes, resourceId, environment, yt01Environment, options } from "../shared/variables.js";

const emailOrderRequestJson = JSON.parse(
    open("../data/orders/01-email-request.json")
);

export const options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {
        // Checks rate should be 100%. Raise error if any check has failed.
        checks: ['rate>=1']
    }
};

const labels = [post_mail_order, get_mail_notifications, post_sms_order, get_sms_notifications];

setEmptyThresholds(labels, options);

/**
 * Gets the recipient based on environment variables
 */
function getOrgNoRecipient() {
    if (!__ENV.orgNoRecipient && environment === yt01Environment) {
        return randomItem(orgNosYt01);
    }
    else {
        return __ENV.orgNoRecipient ? __ENV.orgNoRecipient.toLowerCase() : null;
    }
}

/**
 * Initialize test data.
 * @returns {Object} The data object containing token, runFullTestSet, sendersReference, and emailOrderRequest.
 */
export function setup() {
    const sendersReference = uuidv4();
    const token = setupToken.getAltinnTokenForOrg(scopes);

    const emailOrderRequest = { ...emailOrderRequestJson, sendersReference, resourceId, recipients: [] };

    const smsOrderRequest = {
        senderNumber: "Altinn",
        body: "This is an automated test: " + randomString(30) + " " + randomString(30),
        recipients: [],
        sendersReference: sendersReference
    };

    const orgNoRecipient = getOrgNoRecipient();

    if (orgNoRecipient) {
        smsOrderRequest.recipients.push({ organizationNumber: orgNoRecipient });
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
        smsOrderRequest,
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
        "POST email notification order request. Status is 202 Accepted": (r) => r.status === 202,
    });

    stopIterationOnFail("POST email notification order request failed", success);

    const selfLink = response.headers["Location"];
    if (environment !== yt01Environment) {
        check(response, {
            "POST email notification order request. Location header provided": (r) => selfLink,
            "POST email notification order request. Recipient lookup was successful": (r) => JSON.parse(r.body).recipientLookup.status == 'Success' 
        });
    }
    else {
        check(response, {
            "POST email notification order request. Location header provided": (r) => selfLink,
            "POST email notification order request. Recipient lookup was successful or no recipients found": (r) => JSON.parse(r.body).recipientLookup.status == 'Success' 
            || (JSON.parse(r.body).recipientLookup.status == 'Failed' && JSON.parse(r.body).recipientLookup.missingContact.length > 0)
        });
    }   

    return selfLink;
}

/**
 * Posts an SMS notification order request.
 * @param {Object} data - The data object containing smsOrderRequest and token.
 * @returns {string} The selfLink of the created order.
 */
function postSmsNotificationOrderRequest(data) {
    const response = ordersApi.postSmsNotificationOrder(
        JSON.stringify(data.smsOrderRequest),
        data.token,
        post_sms_order
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
 * Gets the email notifications summary again after one minute for verification.
 * @param {Object} data - The data object containing token and emailOrderRequest.
 * @param {string} orderId - The ID of the order.
 */
function getEmailNotificationSummaryAgainAfterOneMinuteForVerification(data, orderId) {
    sleep(60); // Waiting 1 minute for the notifications to be generated

    const response = notificationsApi.getEmailNotifications(orderId, data.token, get_mail_notifications);

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
 * Gets the SMS notification summary.
 * @param {Object} data - The data object containing token.
 * @param {string} orderId - The ID of the order.
 */
function getSmsNotificationSummary(data, orderId) {
    const response = notificationsApi.getSmsNotifications(orderId, data.token, get_sms_notifications);

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
    // Get a random organization number from the list.
    // For all other envs than yt01, the list only contains one number
    const orgNoRecipient = getOrgNoRecipient();
    data.emailOrderRequest.recipients[0].organizationNumber = orgNoRecipient;
    const selfLink = postEmailNotificationOrderRequest(data);
    const id = selfLink.split("/").pop();

    data.smsOrderRequest.recipients[0].organizationNumber = orgNoRecipient; 
    const smsSelfLink = postSmsNotificationOrderRequest(data);
    const smsId = smsSelfLink.split("/").pop();

    getEmailNotificationSummary(data, id);
    getSmsNotificationSummary(data, smsId);

    if (data.runFullTestSet) {
        getEmailNotificationSummaryAgainAfterOneMinuteForVerification(data, id);
    }
}
