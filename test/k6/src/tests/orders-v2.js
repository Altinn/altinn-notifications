/*
    Test script for Platform Notifications API V2 using an application owner token.

    Command:
    podman compose run k6 run /src/tests/orders-v2.js \
    -e tokenGeneratorUserName={the user name to access the token generator} \
    -e tokenGeneratorUserPwd={the password to access the token generator} \
    -e mpClientId={the id of an integration defined in maskinporten} \
    -e mpKid={the key id of the JSON web key used to sign the maskinporten token request} \
    -e encodedJwk={the encoded JSON web key used to sign the maskinporten token request} \
    -e env={environment: at22, at23, at24, tt02, prod} \
    -e emailRecipient={an email address to add as a notification recipient} \
    -e ninRecipient={a national identity number of a person to include as a notification recipient} \
    -e smsRecipient={a mobile number to include as a notification recipient} \
    -e subscriptionKey={the subscription key with access to the automated tests product} \

    Notes:
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
import * as futureOrdersApi from "../api/notifications/v2.js";
import { post_sms_order_v2, post_email_order_v2, setEmptyThresholds, get_email_shipment, get_sms_shipment, get_status_feed } from "./threshold-labels.js";

const labels = [];

const emailOrderRequestJson = JSON.parse(
    open("../data/orders/order-v2-email.json")
);
const smsOrderRequestJson = JSON.parse(
    open("../data/orders/order-v2-sms.json")
);

const environment = __ENV.env;
const scopes = "altinn:serviceowner/notifications.create";

function getEmailRecipient() {
    if (__ENV.emailRecipient) {
        return __ENV.emailRecipient.toLowerCase();
    }
    if (environment === "yt01") {
        return "noreply@altinn.no";
    }
    return null;
}

function getSmsRecipient() {
    if (__ENV.smsRecipient) {
        return __ENV.smsRecipient.toLowerCase();
    }
    if (environment === "yt01") {
        return "+4799999999";
    }
    return null;
}

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
    const emailRecipient = getEmailRecipient();
    const smsRecipient = getSmsRecipient();

    // used with notification email orders if applicable
    const ninRecipient = __ENV.ninRecipient ? __ENV.ninRecipient.toLowerCase() : null;

    const token = setupToken.getAltinnTokenForOrg(scopes);
    const idempotencyIdEmail = uuidv4();
    const idempotencyIdSms = uuidv4();
    const sendersReference = uuidv4();

    if (emailRecipient) {
        emailOrderRequestJson.recipient.recipientEmail.emailAddress = emailRecipient;
    }
    else {
        // unset recipientEmail object when no email recipient is provided
        delete emailOrderRequestJson.recipient["recipientEmail"];
    }

    if (ninRecipient) {
        emailOrderRequestJson.recipient.recipientPerson.nationalIdentityNumber = ninRecipient;
    }
    else {
        // unset recipientPerson object when no national identity number is provided
        delete emailOrderRequestJson.recipient["recipientPerson"];
    }

    if (smsRecipient) {
        smsOrderRequestJson.recipient.recipientSms.phoneNumber = smsRecipient;
    }

    const emailOrderRequest = { ...emailOrderRequestJson, idempotencyId: idempotencyIdEmail };

    const runFullTestSet = __ENV.runFullTestSet
        ? __ENV.runFullTestSet.toLowerCase().includes("true")
        : false;

    const smsOrderRequest = { ...smsOrderRequestJson, idempotencyId: idempotencyIdSms, sendersReference };

    return {
        token,
        runFullTestSet,
        sendersReference,
        emailOrderRequest,
        smsOrderRequest
    };
}

/**
 * Posts an email notification order request using the v2 API.
 * @param {Object} data - The data object containing emailOrderRequest and token.
 * @returns {string} The response body of the created order.
 */
function postEmailNotificationOrderRequest(data) {
    const response = futureOrdersApi.postNotificationOrderV2(
        JSON.stringify(data.emailOrderRequest),
        data.token,
        post_email_order_v2
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
 * Posts an SMS notification order request using the v2 API.
 * @param {Object} data - The data object containing smsOrderRequest and token.
 * @returns {string} The response body of the created order.
 */
function postSmsNotificationOrderRequest(data) {
    const response = futureOrdersApi.postNotificationOrderV2(
        JSON.stringify(data.smsOrderRequest),
        data.token,
        post_sms_order_v2
    );

    const success = check(response, {
        "POST SMS notification order request. Status is 201 Created": (r) => r.status === 201
    });

    console.log(response);

    stopIterationOnFail("POST SMS notification order request failed", success);

    const selfLink = response.headers["Location"];

    check(response, {
        "POST SMS notification order request. Location header provided": (_) => selfLink,
        "POST SMS notification order request. Response body is not an empty string": (r) => r.body
    });

    return response.body;
}


/**
 * Gets the notification shipment status for email or SMS.
 * @param {Object} data - The data object containing token.
 * @param {string} shipmentId - The ID of the order.
 * @param {string} label - The label for the request.
 * @param {string} type - The type of notification (e.g., "Email" or "SMS").
 */
function getShipmentStatus(data, shipmentId, label, type) {
    const response = futureOrdersApi.getShipment(shipmentId, data.token, label);

    switch (type) {
        case "Email": 
            check(response, {
                "GET shipment details for Email. Status is 200 OK": (r) => r.status === 200,
            });
            check(JSON.parse(response.body), {
                "GET shipment details for Email. ShipmentId property is a match": (shipmentResponse) => shipmentResponse.shipmentId === shipmentId,
            });
            break;
        case "SMS":
            check(response, {
                "GET SMS shipment details for SMS. Status is 200 OK": (r) => r.status === 200,
            });
            check(JSON.parse(response.body), {
                "GET SMS shipment details for SMS. ShipmentId property is a match": (shipmentResponse) => shipmentResponse.shipmentId === shipmentId,
            });
            break;
        default:
            throw new Error(`Unknown notification type: ${type}`);
    }
}

/**
 * Gets the status feed for notifications.
 * @param {Object} data - data object containing token.
 * @param {string} label - the label for the request.
 * @description This function retrieves the status feed for notifications using the sequence number query string start position.
 */
function getStatusFeed(data, label) {
    const sequenceNumber = 0; // starting position for the status feed
    const response = futureOrdersApi.getStatusFeed(sequenceNumber, data.token, label);

    check(response, {
        "GET status feed. Status is 200 OK": (r) => r.status === 200,
    });

    const body = JSON.parse(response.body);

    check(body, {
        "GET status feed. Response body is not empty": (r) => Object.keys(r).length > 0,
    });
}

/**
 * The main function to run the test.
 * @param {Object} data - The data object containing runFullTestSet and other test data.
 */
export default function (data) {
    let response = postEmailNotificationOrderRequest(data);
    let responseObject = JSON.parse(response);

    // checking shipment details for the email order
    getShipmentStatus(data, responseObject.notification.shipmentId, get_email_shipment, "Email");

    response = postSmsNotificationOrderRequest(data);
    responseObject = JSON.parse(response);

    // checking shipment details for the SMS order
    getShipmentStatus(data, responseObject.notification.shipmentId, get_sms_shipment, "SMS");
    
    getStatusFeed(data, get_status_feed);
}
