/*
    Test script for the composed-email endpoint using an application owner token.
    Covers the POST /notifications/api/v1/future/orders/composed-email endpoint.

    Required scope: altinn:serviceowner/notifications.composedemail.create

    Command:
    podman compose run k6 run /src/tests/orders-composed-email-v2.js \
    --secret-source=file=/.secrets \
    -e altinn_env={environment: at22, at23, at24, tt02, prod} \
    -e emailRecipient={an email address to use as the notification recipient} \
    -e sasUrl={optional: a valid Azure Blob Storage SAS URL to include as a test attachment}

    Notes:
    - When `sasUrl` is omitted the positive flow sends the order without attachments, which is
      valid per the API contract. Set `sasUrl` only when end-to-end attachment delivery must be
      exercised.
    - Setting `subscriptionKey` in .secrets is required — retrieve it from Azure APIM.

    Command syntax for different shells:
    - Bash: Use the command as written above.
    - PowerShell: Replace `\` with a backtick (`` ` ``) at the end of each line.
    - Command Prompt (cmd.exe): Replace `\` with `^` at the end of each line.
*/

import { check } from "k6";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

import * as setupToken from "../setup.js";
import * as futureOrdersApi from "../api/notifications/v2.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { getEmailRecipient } from "../shared/functions.js";
import { composedEmailScope } from "../shared/variables.js";
import {
    post_composed_email_order_v2,
    get_composed_email_shipment,
    setEmptyThresholds,
} from "./threshold-labels.js";

const labels = [post_composed_email_order_v2, get_composed_email_shipment];

const orderRequestJson = JSON.parse(
    open("../data/orders/order-v2-composed-email.json")
);

export const options = {
    summaryTrendStats: [
        "avg",
        "min",
        "med",
        "max",
        "p(95)",
        "p(99)",
        "p(99.5)",
        "p(99.9)",
        "count",
    ],
    thresholds: {
        // Checks rate should be 100%. Raise error if any check has failed.
        checks: ["rate>=1"],
    },
};
setEmptyThresholds(labels, options);

/**
 * Builds a minimal composed-email order request.
 * Includes a single attachment when a sasUrl is provided via the `sasUrl` environment variable.
 * @param {string} emailAddress - The recipient email address.
 * @param {string} idempotencyId - A unique idempotency identifier for the order.
 * @param {string} sendersReference - A caller-defined correlation reference.
 * @returns {Object} The composed-email order request object.
 */
function buildOrderRequest(emailAddress, idempotencyId, sendersReference) {
    const requestedSendTime = new Date(Date.now() + 5 * 60 * 1000).toISOString();

    const request = {
        ...orderRequestJson,
        idempotencyId,
        sendersReference,
        requestedSendTime,
        recipient: {
            emailAddress,
            emailSettings: {
                ...orderRequestJson.recipient.emailSettings,
            },
        },
    };

    const sasUrl = __ENV.sasUrl ? __ENV.sasUrl.trim() : null;
    if (sasUrl) {
        request.recipient.emailSettings.attachments = [
            {
                filename: "test-attachment.pdf",
                mimeType: "application/pdf",
                sasUrl,
            },
        ];
    }

    return request;
}

/**
 * Initialise test data — token and order fixtures.
 * @returns {Object} The data object used across all test iterations.
 */
export async function setup() {
    const emailRecipient = getEmailRecipient();

    if (!emailRecipient) {
        stopIterationOnFail(
            "emailRecipient is required for composed-email orders — set the 'emailRecipient' env var",
            false
        );
    }

    const token = await setupToken.getAltinnTokenForOrg(composedEmailScope);

    const idempotencyId = uuidv4();
    const sendersReference = uuidv4();

    const orderRequest = buildOrderRequest(
        emailRecipient,
        idempotencyId,
        sendersReference
    );

    return {
        token,
        idempotencyId,
        sendersReference,
        orderRequest,
        emailRecipient,
    };
}

/**
 * Posts a composed-email notification order and asserts a 201 Created response.
 * @param {Object} data - Test data containing the order request and token.
 * @returns {string} The shipmentId of the created order.
 */
function postComposedEmailOrder(data) {
    const response = futureOrdersApi.postComposedEmailNotificationOrder(
        JSON.stringify(data.orderRequest),
        data.token,
        post_composed_email_order_v2
    );

    const success = check(response, {
        "POST composed-email order. Status is 201 Created": (r) =>
            r.status === 201,
    });

    stopIterationOnFail("POST composed-email order failed", success);

    const selfLink = response.headers["Location"];

    check(response, {
        "POST composed-email order. Location header provided": (_) => selfLink,
        "POST composed-email order. Response body contains shipmentId": (r) =>
            JSON.parse(r.body).notification.shipmentId !== undefined,
    });

    return JSON.parse(response.body).notification.shipmentId;
}

/**
 * Replays a composed-email order with the same idempotencyId and asserts a 200 OK response.
 * @param {Object} data - Test data containing the order request and token.
 */
function postComposedEmailOrderIdempotentReplay(data) {
    const response = futureOrdersApi.postComposedEmailNotificationOrder(
        JSON.stringify(data.orderRequest),
        data.token,
        post_composed_email_order_v2
    );

    check(response, {
        "POST composed-email order (idempotency replay). Status is 200 OK": (
            r
        ) => r.status === 200,
    });
}

/**
 * Retrieves shipment details and asserts correctness.
 * @param {Object} data - Test data containing token.
 * @param {string} shipmentId - The shipment ID to query.
 */
function getComposedEmailShipment(data, shipmentId) {
    const response = futureOrdersApi.getShipment(
        shipmentId,
        data.token,
        get_composed_email_shipment
    );

    check(response, {
        "GET composed-email shipment. Status is 200 OK": (r) =>
            r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET composed-email shipment. ShipmentId matches": (body) =>
            body.shipmentId === shipmentId,
    });
}

/**
 * Posts a composed-email order with a SAS URL missing required query parameters and
 * asserts a 400 Bad Request response.
 * @param {Object} data - Test data containing token.
 */
function postComposedEmailOrderWithMissingSasParameters(data) {
    const invalidRequest = {
        ...data.orderRequest,
        idempotencyId: uuidv4(),
        recipient: {
            ...data.orderRequest.recipient,
            emailSettings: {
                ...data.orderRequest.recipient.emailSettings,
                attachments: [
                    {
                        filename: "doc.pdf",
                        mimeType: "application/pdf",
                        sasUrl:
                            "https://mystorage.blob.core.windows.net/container/doc.pdf",
                    },
                ],
            },
        },
    };

    const response = futureOrdersApi.postComposedEmailNotificationOrder(
        JSON.stringify(invalidRequest),
        data.token,
        post_composed_email_order_v2
    );

    check(response, {
        "POST composed-email order with missing SAS parameters. Status is 400 Bad Request":
            (r) => r.status === 400,
    });
}

/**
 * Posts a composed-email order with a SAS URL pointing to a non-Azure-Blob-Storage host
 * and asserts a 400 Bad Request response.
 * @param {Object} data - Test data containing token.
 */
function postComposedEmailOrderWithInvalidSasHost(data) {
    const invalidRequest = {
        ...data.orderRequest,
        idempotencyId: uuidv4(),
        recipient: {
            ...data.orderRequest.recipient,
            emailSettings: {
                ...data.orderRequest.recipient.emailSettings,
                attachments: [
                    {
                        filename: "doc.pdf",
                        mimeType: "application/pdf",
                        sasUrl:
                            "https://example.com/container/doc.pdf?se=2099-01-01T00%3A00%3A00Z&sp=r&sr=b&sig=fakesig",
                    },
                ],
            },
        },
    };

    const response = futureOrdersApi.postComposedEmailNotificationOrder(
        JSON.stringify(invalidRequest),
        data.token,
        post_composed_email_order_v2
    );

    check(response, {
        "POST composed-email order with non-Azure SAS host. Status is 400 Bad Request":
            (r) => r.status === 400,
    });
}

/**
 * The main test function.
 * @param {Object} data - Test data returned from setup().
 */
export default function runTests(data) {
    const shipmentId = postComposedEmailOrder(data);

    getComposedEmailShipment(data, shipmentId);

    postComposedEmailOrderIdempotentReplay(data);

    postComposedEmailOrderWithMissingSasParameters(data);

    postComposedEmailOrderWithInvalidSasHost(data);
}
