/*
    Test script for creating notification orders and reminders intended for organizations.

    Scenarios exercised in a single iteration (without aborting unless critical auth/execution error):
    - 400 Bad Request (validation failure)
    - 201 Created (valid order)
    - 200 OK (idempotent duplicate)
    - 201 Created or 422 Unprocessable Entity (missing resourceId path)
    - 422 Unprocessable Entity (invalid/unknown org contact path attempt)

    The script creates a unique idempotency identifier per "valid" scenario, and reuses it to test idempotency.
    Organization number: uses provided __ENV.orgNoRecipient or a random 9-digit number (fallback).
*/

import { check } from "k6";
import * as setupToken from "../setup.js";
import * as ordersApi from "../api/notifications/v2.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { getOrgNoRecipient } from "../shared/functions.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { scopes, resourceId, orderTypes } from "../shared/variables.js";
import { post_order_chain, post_invalid_order, post_duplicate_order, setEmptyThresholds } from "./threshold-labels.js";

const orderChainRequestJson = JSON.parse(open("../data/orders/order-with-reminders-for-organizations.json"));

export const options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {
        checks: ['rate>=1']
    }
};

const labels = [post_order_chain, post_invalid_order, post_duplicate_order];
setEmptyThresholds(labels, options);

/**
 * Initialize test data.
 */
export function setup() {
    const token = setupToken.getAltinnTokenForOrg(scopes);

    const randomIdentifier = uuidv4().substring(0, 8);
    const mainOrderSendersReference = `k6-order-${randomIdentifier}`;
    const reminderSendersReference = `k6-order-rem-${randomIdentifier}`;

    const orderChainRequest = JSON.parse(JSON.stringify(orderChainRequestJson));
    orderChainRequest.dialogportenAssociation = {
        dialogId: uuidv4(),
        transmissionId: uuidv4()
    };

    orderChainRequest.sendersReference = mainOrderSendersReference;
    orderChainRequest.recipient.recipientOrganization.resourceId = resourceId;

    orderChainRequest.reminders = orderChainRequest.reminders.map((reminder) => {
        reminder.sendersReference = reminderSendersReference;
        reminder.recipient.recipientOrganization.resourceId = resourceId;
        return reminder;
    });

    return {
        token,
        orderChainRequest
    };
}

/**
 * Prepares an invalid order request by removing the recipientOrganization
 * from both the main recipient and all reminders. This simulates a 400 Bad Request
 * due to missing required fields.
 *
 * @param {Object} baseOrder - A cloned order request object to be modified.
 * @returns {Object} - The modified order request with missing recipientOrganization fields.
 */
function prepareInvalidOrder(baseOrder) {
    delete baseOrder.recipient.recipientOrganization;

    baseOrder.reminders.forEach(reminder => {
        delete reminder.recipient.recipientOrganization;
    });

    return baseOrder;
}

/**
 * Prepares an order request with missing resourceId fields by removing the resourceId
 * from both the main recipient and all reminders. This simulates a 201 Created or
 * 422 Unprocessable Entity response depending on API behavior.
 *
 * Additionally, a new idempotencyId is generated to ensure uniqueness.
 *
 * @param {Object} baseOrder - A cloned order request object to be modified.
 * @returns {Object} - The modified order request with missing resourceId fields.
 */
function prepareMissingResourceOrder(baseOrder) {
    baseOrder.idempotencyId = uuidv4();
    delete baseOrder.recipient.recipientOrganization.resourceId;

    baseOrder.reminders.forEach(reminder => {
        delete reminder.recipient.recipientOrganization.resourceId;
    });

    return baseOrder;
}

/**
 * Creates a new unquie order-chain request based on a template, with updated orgNumber and idempotencyId.
 * @param data - The base data object containing the original orderChainRequest.
 * @param orgNumber - The organization number to apply to the recipient and reminders.
 * @returns A deep-cloned and updated order request object.
 */
function createUnquieOrderChainRequest(data, orgNumber) {
    const clonedOrderChainRequest = JSON.parse(JSON.stringify(data.orderChainRequest));

    clonedOrderChainRequest.idempotencyId = uuidv4();
    clonedOrderChainRequest.recipient.recipientOrganization.orgNumber = orgNumber;

    clonedOrderChainRequest.reminders.forEach(reminder => {
        reminder.recipient.recipientOrganization.orgNumber = orgNumber;
    });

    return clonedOrderChainRequest;
}

/**
 * Posts a notification orderChainRequests chain.
 * @param data - Contains the token and other metadata.
 * @param orderRequest - The order request object to be sent.
 * @param label - Optional label for logging or tracking.
 * @returns The result of the API call.
 */
function postNotificationOrderChain(data, orderRequest, label = post_order_chain) {
    return ordersApi.postNotificationOrderV2(JSON.stringify(orderRequest), data.token, label);
}

/**
 * Generates one or more order chain requests based on the specified order type(s).
 *
 * @param {string[]} orderTypes - List of order types to generate (e.g. ["valid", "invalid"]).
 * @param {Object} data - Setup data including token and base template.
 * @param {string} orgNumber - Organization number to apply to the orderChainRequests.
 * @returns {Object} - Object containing the generated orderChainRequests keyed by type.
 */
function generateOrderChainRequestByOrderType(orderTypes, data, orgNumber) {
    const orderChainRequests = {};

    for (const orderType of orderTypes) {
        switch (orderType) {
            case "valid":
                orderChainRequests.validOrder = createUnquieOrderChainRequest(data, orgNumber);
                break;

            case "duplicate":
                if (orderChainRequests.validOrder) {
                    orderChainRequests.validOrder = createUnquieOrderChainRequest(data, orgNumber);
                }

                orderChainRequests.duplicateOrder = JSON.parse(JSON.stringify(orderChainRequests.validOrder));
                break;

            case "invalid":
                orderChainRequests.invalidOrder = prepareInvalidOrder(createUnquieOrderChainRequest(data, orgNumber));
                break;

            case "missingResource":
                orderChainRequests.missingResourceOrder = prepareMissingResourceOrder(createUnquieOrderChainRequest(data, orgNumber));
                break;

            case "all":
                orderChainRequests.validOrder = createUnquieOrderChainRequest(data, orgNumber);
                orderChainRequests.duplicateOrder = JSON.parse(JSON.stringify(orderChainRequests.validOrder));
                orderChainRequests.invalidOrder = prepareInvalidOrder(createUnquieOrderChainRequest(data, orgNumber));
                orderChainRequests.missingResourceOrder = prepareMissingResourceOrder(createUnquieOrderChainRequest(data, orgNumber));
                break;

            default:
                console.error(`Unknown orderType: ${orderType}`);
                stopIterationOnFail(`Invalid orderType: ${orderType}`);
        }
    }

    return orderChainRequests;
}

/**
 * Main test execution function for sending notification order requests to the API.
 *
 * The function determines which type(s) of order to create and send based on the `orderType`
 * environment variable. Supported types include:
 * - "valid": Sends a valid order (expects 201 Created).
 * - "duplicate": Sends the same order twice to test idempotency (expects 200 OK on second).
 * - "invalid": Sends an order missing required fields (expects 400 Bad Request).
 * - "missingResource": Sends an order missing resource identifiers (expects 201 or 422).
 * - "all": Executes all of the above scenarios in sequence.
 *
 * Each order is created using helper functions and posted to the API using `postNotificationOrderChain`.
 * The responses are validated using `check`, and critical authentication errors (401/403) will stop the iteration.
 *
 * @function
 * @param {Object} data - The setup data returned from the `setup()` function, including token and base order template.
 * @returns {void}
 */
export default function (data) {
    const orgNumber = getOrgNoRecipient();
    const orderChainRequests = generateOrderChainRequestByOrderType(orderTypes, data, orgNumber);

    const responses = [];

    if (orderChainRequests.validOrder) {
        const validOrderResponse = postNotificationOrderChain(data, orderChainRequests.validOrder, post_order_chain);
        responses.push({ name: "valid-order", response: validOrderResponse });
    }

    if (orderChainRequests.invalidOrder) {
        const invalidResponse = postNotificationOrderChain(data, orderChainRequests.invalidOrder, post_invalid_order);
        responses.push({ name: "invalid-order", response: invalidResponse });
    }

    if (orderChainRequests.duplicateOrder) {
        const duplicateResponse = postNotificationOrderChain(data, orderChainRequests.duplicateOrder, post_duplicate_order);
        responses.push({ name: "duplicate-valid-order", response: duplicateResponse });
    }

    if (orderChainRequests.missingResourceOrder) {
        const missingResourceIdentiiferResponse = postNotificationOrderChain(data, orderChainRequests.missingResourceOrder, post_order_chain);
        responses.push({ name: "order-without-resource-identifier", response: missingResourceIdentiiferResponse });
    }

    let criticalFailure = false;
    for (const responsesEntry of responses) {
        let body;
        const requestResponse = responsesEntry.response;

        try {
            body = requestResponse.body ? JSON.parse(requestResponse.body) : {};
        } catch (_) {
            // ignore parse error for paths not returning JSON
        }

        switch (responsesEntry.name) {
            case "valid-order":
                check(requestResponse, {
                    "Valid order returns 201 Created": (result) => result.status === 201,
                    "Valid order has shipmentId": () => body?.notification?.shipmentId !== undefined
                });
                break;

            case "invalid-order":
                check(requestResponse, {
                    "400 validation (invalid order)": (result) => result.status === 400
                });
                break;

            case "duplicate-valid-order":
                check(requestResponse, {
                    "Duplicate order returns 200 OK": (result) => result.status === 200,
                    "Duplicate order has existing shipmentId": () => body?.notification?.shipmentId !== undefined
                });
                break;

            case "order-without-resource-identifier":
                check(requestResponse, {
                    "Missing resource returns 201 or 422": (result) => result.status === 422
                });
                break;
        }

        if (responsesEntry.status === 401 || responsesEntry.status === 403) {
            criticalFailure = true;
        }
    }

    if (criticalFailure) {
        stopIterationOnFail("Critical authentication/authorization error encountered", false);
    }
}
