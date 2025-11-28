/*
    Test script for Platform Notifications API V2 using an organization token.

    Command:
    podman compose run k6 run /src/tests/orders-org-no-v2.js \
        -e tokenGeneratorUserName={the user name to access the token generator} \
        -e tokenGeneratorUserPwd={the password to access the token generator} \
        -e mpClientId={the id of an integration defined in maskinporten} \
        -e mpKid={the key id of the JSON web key used to sign the maskinporten token request} \
        -e encodedJwk={the encoded JSON web key used to sign the maskinporten token request} \
        -e env={environment: at22, at23, at24, tt02, prod} \
        -e orgNoRecipient={an organization number to include as a notification recipient} \
        -e resourceId={the resource ID associated with the notification order} \

    Notes:
    - The `resourceId` is required for email, and should be a valid resource identifier. 
    - However, the field is not used for the SMS notification order request use case test below. 
    - The `orgNoRecipient` is required for sending notifications to an organization, _unless_ set environment = yt01 .

    Command syntax for different shells:
    - Bash: Use the command as written above.
    - PowerShell: Replace `\` with a backtick (`` ` ``) at the end of each line.
    - Command Prompt (cmd.exe): Replace `\` with `^` at the end of each line.
*/

import { check } from "k6";
import { stopIterationOnFail } from "../errorhandler.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

import * as setupToken from "../setup.js";
import * as ordersApi from "../api/notifications/v2.js";
import { post_email_order_v2, get_email_shipment, post_sms_order_v2, get_sms_shipment, setEmptyThresholds } from "./threshold-labels.js";
import { getShipmentStatus } from "./orders-v2.js";
import { scopes, resourceId } from "../shared/variables.js";
import { getOrgNoRecipient } from "../shared/functions.js";

const orderRequestJson = JSON.parse(
    open("../data/orders/order-v2-org.json")
);

export const options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {
        // Checks rate should be 100%. Raise error if any check has failed.
        checks: ['rate>=1']
    }
};

const labels = [post_email_order_v2, get_email_shipment, post_sms_order_v2, get_sms_shipment];

setEmptyThresholds(labels, options);

/**
 * Initialize test data.
 * @returns {Object} The data object containing token, sendersReference, and emailOrderRequest.
 */
export function setup() {
    const sendersReference = uuidv4();
    const idempotencyIdEmail = uuidv4();
    const idempotencyIdSms = uuidv4();  
    const token = setupToken.getAltinnTokenForOrg(scopes);

    const orgNoRecipient = getOrgNoRecipient();

    const emailOrderRequest = {
        ...orderRequestJson,
        idempotencyId: idempotencyIdEmail,
        sendersReference,
        recipient: {
            ...orderRequestJson.recipient,
            recipientOrganization: {
                ...orderRequestJson.recipient.recipientOrganization,
                orgNumber: orgNoRecipient,
                resourceId: resourceId,
                channelSchema: "Email"
            }
        }
    };

    const smsOrderRequest = {
        ...orderRequestJson,
        idempotencyId: idempotencyIdSms,
        sendersReference,
        recipient: {
            ...orderRequestJson.recipient,
            recipientOrganization: {
                ...orderRequestJson.recipient.recipientOrganization,
                orgNumber: orgNoRecipient,
                resourceId: undefined,
                channelSchema: "SMS"
            }
        }
    };

    return {
        token,
        sendersReference,
        emailOrderRequest,
        smsOrderRequest
    };
}

/**
 * Posts an email notification order request.
 * @param {Object} data - The data object containing emailOrderRequest and token.
 * @returns {string} The response body of the created order.
 */
function postEmailNotificationOrderRequest(data) {
    const response = ordersApi.postNotificationOrderV2(
        JSON.stringify(data.emailOrderRequest),
        data.token,
        post_email_order_v2
    );

    const success = check(response, {
        "POST email notification order request. Status is 201 Created": (r) => r.status === 201,
    });

    stopIterationOnFail("POST email notification order request failed", success);

    const selfLink = response.headers["Location"];

    check(response, {
        "POST email notification order request. Location header provided": (_) => selfLink,
        "POST email notification order request. Response body is not an empty string": (r) => r.body
    });

    return response.body;
}

/**
 * Posts an SMS notification order request.
 * @param {Object} data - The data object containing smsOrderRequest and token.
 * @returns {string} The response body of the created order.
 */
function postSmsNotificationOrderRequest(data) {
    const response = ordersApi.postNotificationOrderV2(
        JSON.stringify(data.smsOrderRequest),
        data.token,
        post_sms_order_v2
    );

    const success = check(response, {
        "POST SMS notification order request. Status is 201 Created": (r) => r.status === 201
    });

    stopIterationOnFail("POST SMS notification order request failed", success);

    const selfLink = response.headers["Location"];

    check(response, {
        "POST SMS notification order request. Location header provided": (_) => selfLink,
        "POST SMS notification order request. Response body is not an empty string": (r) => r.body
    });

    return response.body;
}

/**
 * The main function to run the test.
 * @param {Object} data - The data object containing test data.
 */
export default function runTests(data) {
    let response = postEmailNotificationOrderRequest(data);
    getShipmentStatus(data, JSON.parse(response).notification.shipmentId, get_email_shipment, "Email");

    // Disable Sms notifications order request until missing contact information in test data is resolved.
    // response = postSmsNotificationOrderRequest(data);
    // getShipmentStatus(data, JSON.parse(response).notification.shipmentId, get_sms_shipment, "SMS");
}
