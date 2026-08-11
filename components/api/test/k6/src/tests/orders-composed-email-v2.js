/*
    Test script for Platform Notifications composed-email API using an application owner token.

    Command:
    podman compose run k6 run /src/tests/orders-composed-email-v2.js \
    --secret-source=file=/.secrets \
    -e altinn_env={environment: at22, at23, at24, tt02, prod} \
    -e emailRecipient={an email address to use as the notification recipient}

    Notes:
    - Setting `subscriptionKey` in .secrets is required - can be retrieved from Azure APIM.

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
    const sendersReference = `k6-composed-email-${uuidv4().substring(0, 8)}`;

    const orderRequest = {
        ...orderRequestJson,
        idempotencyId,
        sendersReference,
        dialogportenAssociation: {
            dialogId: uuidv4(),
            transmissionId: uuidv4(),
        },
        recipient: {
            emailAddress: emailRecipient,
            emailSettings: {
                ...orderRequestJson.recipient.emailSettings,
            },
        },
    };

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
function replayComposedEmailOrder(data) {
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

    const statusOk = check(response, {
        "GET composed-email shipment. Status is 200 OK": (r) =>
            r.status === 200,
    });

    if (!statusOk) {
        return;
    }

    let body;
    try {
        body = JSON.parse(response.body);
    } catch (_) {
        check(null, {
            "GET composed-email shipment. Response body is valid JSON": () =>
                false,
        });
        return;
    }

    check(body, {
        "GET composed-email shipment. ShipmentId matches": (b) =>
            b.shipmentId === shipmentId,
    });
}

/**
 * Posts a composed-email order with a SAS URL missing required query parameters and
 * asserts a 400 Bad Request response.
 * @param {Object} data - Test data containing token.
 */
function postComposedEmailOrderMissingSasQueryParams(data) {
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
 * The main test function.
 * @param {Object} data - Test data returned from setup().
 */
export default function runTests(data) {
    const shipmentId = postComposedEmailOrder(data);

    getComposedEmailShipment(data, shipmentId);

    replayComposedEmailOrder(data);

    postComposedEmailOrderMissingSasQueryParams(data);
}
