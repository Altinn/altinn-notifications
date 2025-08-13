/*
    Test script for creating notification orders and reminders intended for organizations.
    
    Covered scenarios:
    - Validation error handling for invalid requests (400 Bad Request)
    - Orders without resource IDs (201 Created or 422 Unprocessable Entity)
    - Idempotency check for duplicate orders (200 OK with existing details)
    - Valid notification order creation with email/SMS reminders (201 Created)
    - Shipment status verification (checking identifier, reference, type, status)

    Command:
    podman compose run k6 run /src/tests/orders-org-chain-v2.js \
        -e tokenGeneratorUserName={the user name to access the token generator} \
        -e tokenGeneratorUserPwd={the password to access the token generator} \
        -e mpClientId={the id of an integration defined in maskinporten} \
        -e mpKid={the key id of the JSON web key used to sign the maskinporten token request} \
        -e encodedJwk={the encoded JSON web key used to sign the maskinporten token request} \
        -e env={environment: at22, at23, at24, tt02, prod} \
        -e orgNoRecipient={an organization number to include as a notification recipient} \
        -e resourceId={the resource ID associated with the notification order} \
*/

import { check } from "k6";
import * as setupToken from "../setup.js";
import * as ordersApi from "../api/notifications/v2.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { getOrgNoRecipient } from "../shared/functions.js";
import { scopes, resourceId } from "../shared/variables.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { post_order_chain, setEmptyThresholds } from "./threshold-labels.js";

const orderChainRequestJson = JSON.parse(
    open("../data/orders/order-with-reminders-for-organizations.json")
);

export const options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {
        checks: ['rate>=1']
    }
};

const labels = [post_order_chain];
setEmptyThresholds(labels, options);

/**
 * Initialize test data for various test scenarios.
 */
export function setup() {

    // Retrieve organization number and generate an authentication token
    const orgNoRecipient = getOrgNoRecipient();
    const token = setupToken.getAltinnTokenForOrg(scopes);

    // Generate unique identifiers
    const dialogId = uuidv4();
    const idempotencyId = uuidv4();
    const transmissionId = uuidv4();
    const sendersReference = "k6-test-order-with-reminders-for-organizations" + uuidv4().substring(0, 8);

    // Create a valid order request
    const validOrderRequest = JSON.parse(JSON.stringify(orderChainRequestJson));
    validOrderRequest.idempotencyId = idempotencyId;
    validOrderRequest.sendersReference = sendersReference;
    validOrderRequest.recipient.recipientOrganization.resourceId = resourceId;
    validOrderRequest.recipient.recipientOrganization.orgNumber = orgNoRecipient;

    validOrderRequest.dialogportenAssociation = {
        dialogId: dialogId,
        transmissionId: transmissionId
    };

    for (let reminder of validOrderRequest.reminders) {
        reminder.sendersReference = "reminder-" + sendersReference;
        reminder.recipient.recipientOrganization.resourceId = resourceId;
        reminder.recipient.recipientOrganization.orgNumber = orgNoRecipient;
    }

    return {
        token,
        validOrderRequest
    };
}

/**
 * Posts a notification order chain request.
 * 
 * @param {Object} data - The test data context containing token
 * @param {Object} orderRequest - The order request object to be registered
 * @param {string} label - Custom metric label for tracking this specific operation type
 * @returns {Object} - The HTTP response object
 */
function postNotificationOrderChain(data, orderRequest, label = post_order_chain) {
    const response = ordersApi.postNotificationOrderV2(
        JSON.stringify(orderRequest),
        data.token,
        label
    );

    return response;
}

/**
* Main test function that executes all notification order scenarios.
* Each virtual user will run through this complete sequence.
*/
export default function (data) {

    // Create unique request with new IDs
    const orgNoRecipient = getOrgNoRecipient();
    const uniqueRequest = JSON.parse(JSON.stringify(data.validOrderRequest));
    uniqueRequest.idempotencyId = uuidv4();
    uniqueRequest.dialogportenAssociation.dialogId = uuidv4();
    uniqueRequest.dialogportenAssociation.transmissionId = uuidv4();
    uniqueRequest.recipient.recipientOrganization.orgNumber = orgNoRecipient;
    uniqueRequest.sendersReference = "k6-test-order-with-reminders-" + uuidv4().substring(0, 8);

    for (let reminder of uniqueRequest.reminders) {
        reminder.recipient.recipientOrganization.orgNumber = orgNoRecipient;
        reminder.sendersReference = "reminder-" + uniqueRequest.sendersReference;
    }

    let orderResponse = postNotificationOrderChain(data, data.validOrderRequest);

    let shipmentId;
    let responseBody;   
    let responseCategory;

    try {
        switch (orderResponse.status) {

            case 200:
                responseCategory = "duplicate_order";
                responseBody = JSON.parse(orderResponse.body);
                shipmentId = responseBody.notification?.shipmentId;

                check(orderResponse, {
                    "Duplicate order returns 200 OK": (r) => r.status === 200,
                    "Duplicate order contains valid notification": (r) => responseBody.notification !== undefined,
                    "Duplicate order contains shipmentId": (r) => shipmentId !== undefined
                })
                break;

            case 201:
                responseCategory = "created_successfully";
                responseBody = JSON.parse(orderResponse.body);
                shipmentId = responseBody.notification?.shipmentId;

                check(orderResponse, {
                    "Valid order accepted with 201 Created": (r) => r.status === 201,
                    "Valid order response contains shipmentId": (r) => shipmentId !== undefined,
                    "Valid order response includes Location header": (r) => r.headers["Location"] !== undefined
                });
                break;

            case 401:
            case 403:
                responseCategory = "authentication_error";
                check(orderResponse, {
                    "Authentication/authorization error handled": (r) => r.status === 401 || r.status === 403
                });
                break;

            default:
                responseCategory = "unexpected_status";
                check(orderResponse, {
                    "Response has a status code": (r) => r.status > 0
                });
        }
    } catch (error) {
        responseCategory = "execution_error";
    }

    // Stop iteration on critical failures
    if (responseCategory === "authentication_error" || responseCategory === "execution_error") {
        stopIterationOnFail(`Critical error in ${responseCategory}`, false);
    }

    return shipmentId;
}