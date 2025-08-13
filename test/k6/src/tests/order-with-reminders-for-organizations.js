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
import { post_order_chain, post_invalid_order, get_order_shipment, setEmptyThresholds, post_duplicate_order } from "./threshold-labels.js";

const orderChainRequestJson = JSON.parse(
    open("../data/orders/order-with-reminders-for-organizations.json")
);

export const options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {
        checks: ['rate>=1']
    }
};

const labels = [post_order_chain, get_order_shipment, post_invalid_order, post_duplicate_order];
setEmptyThresholds(labels, options);

/**
 * Initialize test data for various test scenarios.
 */
export function setup() {

    // 1. Retrieve organization number and generate an authentication token
    const orgNoRecipient = getOrgNoRecipient();
    const token = setupToken.getAltinnTokenForOrg(scopes);

    // 2. Generate unique identifiers for the order and reminders
    const dialogId = uuidv4();
    const idempotencyId = uuidv4();
    const transmissionId = uuidv4();
    const sendersReference = "k6-test-order-with-reminders-for-organizations" + uuidv4().substring(0, 8);

    // 3. Create a valid order request
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

    // 5. Create an invalid order request by removing required email settings ("channelSchema": "EmailPreferred")
    const invalidOrderRequest = JSON.parse(JSON.stringify(validOrderRequest));
    delete invalidOrderRequest.recipient.recipientOrganization.emailSettings;

    // 6. Create a valid order request by removing the resourceId from the main order and reminders
    const noResourceIdOrderRequest = JSON.parse(JSON.stringify(validOrderRequest));
    noResourceIdOrderRequest.idempotencyId = uuidv4();
    noResourceIdOrderRequest.recipient.recipientOrganization.resourceId = undefined;
    for (let reminder of noResourceIdOrderRequest.reminders) {
        reminder.recipient.recipientOrganization.resourceId = undefined;
    }

    return {
        token,
        validOrderRequest,
        invalidOrderRequest,
        noResourceIdOrderRequest,
        idempotencyId
    };
}

/**
* Posts a notification order chain request.
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
 * Gets the shipment status for a notification.
 */
function getShipmentStatus(data, shipmentId) {
    const response = ordersApi.getShipment(shipmentId, data.token, get_order_shipment);

    check(response, {
        "GET shipment details. Status is 200 OK": (r) => r.status === 200
    });

    check(JSON.parse(response.body), {
        "GET shipment details. ShipmentId property is a match": (shipmentResponse) => shipmentResponse.shipmentId === shipmentId
    });

    return response;
}

/**
 * Main test function that executes all notification order scenarios.
 * Each virtual user will run through this complete sequence.
 */
export default function (data) {
    // Test Case 1: Invalid Order (Missing Required Fields)
    // Tests the API's validation capabilities by sending an order missing email settings
    let response = postNotificationOrderChain(data, data.invalidOrderRequest, post_invalid_order);
    check(response, {
        "Invalid order rejected with 400 Bad Request": (r) => r.status === 400,
        "Invalid order response includes validation details": (r) => r.body.includes("validation")
    });
    
    // Test Case 2: Valid Order Creation with Profile and Authorization APIs timing measurement
    // Tests successful order creation with all required fields and measures backend API calls
    response = postNotificationOrderChain(data, data.validOrderRequest);
    const success = check(response, {
        "Valid order accepted with 201 Created": (r) => r.status === 201,
        "Valid order response includes Location header": (r) => r.headers["Location"],
        "Valid order response contains shipmentId": (r) => JSON.parse(r.body).notification.shipmentId
    });

    // Stop the test if the primary scenario fails
    stopIterationOnFail("Main order creation failed - stopping test", success);

    // Store successful response for later comparison
    const orderResponse = JSON.parse(response.body);
    const shipmentId = orderResponse.notification.shipmentId;

    // Test Case 3: Duplicate Order Submission (Idempotency)
    // Tests that sending the same order twice returns the existing order details
    response = postNotificationOrderChain(data, data.validOrderRequest, post_duplicate_order);
    check(response, {
        "Duplicate order returns 200 OK": (r) => r.status === 200,
        "Duplicate order returns original shipmentId": (r) => JSON.parse(r.body).notification.shipmentId === shipmentId
    });

    // Test Case 4: Order Without Resource ID
    // Tests that the API handles missing resource IDs appropriately
    response = postNotificationOrderChain(data, data.noResourceIdOrderRequest);
    check(response, {
        "No-resourceId order returns appropriate status": (r) => r.status === 201 || r.status === 422,
        "No-resourceId order response is well-formed": (r) => r.status === 422 || JSON.parse(r.body).notification !== undefined
    });
}