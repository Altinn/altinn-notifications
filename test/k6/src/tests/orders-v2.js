/*
    Test script for Platform Notifications API V2 using an application owner token.

    Command:
    podman compose run k6 run /src/tests/v2-orders-email.js \
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

import { check } from "k6";
import { stopIterationOnFail } from "../errorhandler.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

import * as setupToken from "../setup.js";
import * as futureOrdersApi from "../api/notifications/future.js";
import { post_mail_order, get_mail_notifications, setEmptyThresholds } from "./threshold-labels.js";

const labels = [post_mail_order, get_mail_notifications];

const emailOrderRequestJson = JSON.parse(
    open("../data/orders/order-v2-email.json")
);

const environment = __ENV.env;
const scopes = "altinn:serviceowner/notifications.create";
const emailRecipient = __ENV.emailRecipient ? __ENV.emailRecipient.toLowerCase() : environment === "yt01"? "noreply@altinn.no" : null;

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
export function setup() {
    const token = setupToken.getAltinnTokenForOrg(scopes);
    const idempotencyId = uuidv4();
    const sendersReference = uuidv4();

    if (emailRecipient) {
        emailOrderRequestJson.recipient.recipientEmail.emailAddress = emailRecipient;
    }

    const emailOrderRequest = { ...emailOrderRequestJson, idempotencyId };

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
    const response = futureOrdersApi.postEmailNotificationOrderV2(
        JSON.stringify(data.emailOrderRequest),
        data.token,
        post_mail_order
    );

    const success = check(response, {
        "POST email notification order request. Status is 201 Created": (r) => r.status === 201
    });

    stopIterationOnFail("POST email notification order request failed", success);

    const selfLink = response.headers["Location"];

    check(response, {
        "POST email notification order request. Location header provided": (_) => selfLink,
    });

    return response.body;
}


/**
 * Gets the email notification shipment status.
 * @param {Object} data - The data object containing token.
 * @param {string} shipmentId - The ID of the order.
 */
function getShipmentStatus(data, shipmentId) {
    const response = futureOrdersApi.getShipment(shipmentId, data.token, get_mail_notifications);

    check(response, {
        "GET email shipment. Status is 200 OK": (r) => r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET email shipment status. ShipmentId property is a match": (shipmentResponse) => shipmentResponse.shipmentId === shipmentId,
    });
}


/**
 * The main function to run the test.
 * @param {Object} data - The data object containing runFullTestSet and other test data.
 */
export default function (data) {
    const response = postEmailNotificationOrderRequest(data);
    const responseObject = JSON.parse(response);

    getShipmentStatus(data, responseObject.notification.shipmentId);

    // if (data.runFullTestSet) {
    //     getNotificationOrderById(data, selfLink, id);
    //     getNotificationOrderBySendersReference(data);
    //     getNotificationOrderWithStatus(data, id, "Email");
    //     getEmailNotificationSummary(data, id);
    //     postEmailNotificationOrderWithNegativeConditionCheck(data);
    // } else {
    // }
}
