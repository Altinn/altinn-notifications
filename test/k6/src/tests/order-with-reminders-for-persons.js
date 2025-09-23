/*
    Test script for registering notification orders and reminders intended for persons.

    Scenarios executed per iteration (aborts only on 401/403 auth errors):
    - 201 Created (valid order)
    - 200 OK (idempotent duplicate)
    - 400 Bad Request (validation failure)

    Idempotency:
    - A unique idempotency identifier is generated for each newly constructed base order.

    Abort policy:
    - Iteration stops early only on 401 or 403 responses.

    Command:
    podman compose run k6 run /src/tests/order-with-reminders-for-persons.js \
    -e tokenGeneratorUserName={the user name to access the token generator} \
    -e tokenGeneratorUserPwd={the password to access the token generator} \
    -e mpClientId={the identifier of an integration defined in maskinporten} \
    -e mpKid={the key identifier of the JSON web key used to sign the maskinporten token request} \
    -e encodedJwk={the encoded JSON web key used to sign the maskinporten token request} \
    -e env={the environment to run this script within: at22, at23, at24, yt01, tt02, prod} \
    -e ninRecipient={a Norwegian birth number to include as a notification recipient} \
    -e orderTypes={types of orders to test, e.g., valid, invalid or duplicate} \
*/

import { stopIterationOnFail } from "../errorhandler.js";
import { ninRecipient, orderTypes } from "../shared/variables.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import {
    buildOptions,
    getFutureDate,
    processOrderChainPayload,
    runValidators,
    handleSummary,
    validOrderDuration,
    invalidOrderDuration,
    duplicateOrderDuration,
    highLatencyRate,
    orderKindRateValid,
    http201Created,
    http200Duplicate,
    http400Validation,
    validateStandardNotificationShape
} from "./order-with-reminders-functions.js";
import { post_valid_order, post_invalid_order, post_duplicate_order, setEmptyThresholds } from "./threshold-labels.js";

// Define the order types to be tested based on environment variables or defaults
const labels = [post_valid_order, post_invalid_order, post_duplicate_order];

// Test order chain loaded from a JSON file.
const orderChainJsonPayload = JSON.parse(open("../data/orders/order-with-reminders-for-persons.json"));

// Export shared options
export const options = buildOptions();

// Set empty thresholds for label-specific metrics
setEmptyThresholds(labels, options);

/**
 * Prepares test data by creating a notification order chain with unique identifiers.
 * 
 * @returns {Object} Test context containing the order chain payload
 */
export function setup() {
    const uniqueIdentifier = uuidv4().substring(0, 8);
    const orderChainPayload = JSON.parse(JSON.stringify(orderChainJsonPayload));

    orderChainPayload.requestedSendTime = getFutureDate(7);
    orderChainPayload.sendersReference = `k6-order-${uniqueIdentifier}`;

    orderChainPayload.dialogportenAssociation = {
        dialogId: uniqueIdentifier,
        transmissionId: uniqueIdentifier
    };

    if (Array.isArray(orderChainPayload.reminders)) {
        orderChainPayload.reminders = orderChainPayload.reminders.map(reminder => ({
            ...reminder,
            sendersReference: `k6-reminder-order-${uniqueIdentifier}`
        }));
    }

    return { orderChainPayload };
}

/**
 * Update the national identify number used to identify recipients.
 * 
 * @param {Object} recipient - The recipient object to update
 * @param {string} nationalIdentityNumber - The national identify number to set
 * @returns {Object} Updated recipient with new national identify number
 */
function updateRecipientWithBirthNumber(recipient, nationalIdentityNumber) {
    if (!recipient?.recipientPerson) {
        stopIterationOnFail("Recipient is missing required recipientPerson property", false);
        return recipient;
    }

    return {
        ...recipient,
        recipientPerson: {
            ...recipient.recipientPerson,
            nationalIdentityNumber
        }
    };
}

/**
 * Creates a unique order chain payload with consistent identifiers for API testing.
 *
 * @param {Object} data - Shared setup data
 * @returns {Object} Modified payload
 */
function createUniqueOrderChainPayload(data) {
    return {
        ...data.orderChainPayload,
        idempotencyId: uuidv4(),
        recipient: updateRecipientWithBirthNumber(
            data.orderChainPayload.recipient,
            ninRecipient
        ),
        reminders: Array.isArray(data.orderChainPayload.reminders)
            ? data.orderChainPayload.reminders.map(reminder => ({
                ...reminder,
                recipient: updateRecipientWithBirthNumber(
                    reminder.recipient,
                    ninRecipient
                )
            }))
            : []
    };
}

/**
 * Creates an intentionally invalid order chain payload (removes recipientPerson).
 *
 * @param {Object} orderChainPayload - Original payload
 * @returns {Object} Invalid payload
 */
function stripRecipientPersonFromOrderChainPayload(orderChainPayload) {
    if (!orderChainPayload) {
        return orderChainPayload;
    }
    return {
        ...orderChainPayload,
        recipient: {
            ...orderChainPayload.recipient,
            recipientPerson: undefined
        },
        reminders: orderChainPayload.reminders.map(reminder => ({
            ...reminder,
            recipient: {
                ...reminder.recipient,
                recipientPerson: undefined
            }
        }))
    };
}

/**
 * Generates order chain payloads depending on configured order types.
 *
 * @param {Object} data - Setup data
 * @returns {Array<Object>} Variants
 */
function generateOrderChainPayloadsByOrderType(data) {
    const variants = [];
    for (const orderType of orderTypes) {
        const unique = createUniqueOrderChainPayload(data);
        switch (orderType) {
            case "valid":
            case "duplicate":
                variants.push({ orderType, orderChainPayload: unique });
                break;
            case "invalid":
                variants.push({ orderType, orderChainPayload: stripRecipientPersonFromOrderChainPayload(unique) });
                break;
        }
    }
    return variants;
}

/**
 * Main iteration function.
 *
 * @param {Object} data - Setup context
 */
export default function (data) {
    const variants = generateOrderChainPayloadsByOrderType(data);

    const processingResults = variants.map(v => {
        const { orderType, orderChainPayload } = v;
        switch (orderType) {
            case "valid":
                return processOrderChainPayload(orderType, orderChainPayload, post_valid_order, validOrderDuration);
            case "invalid":
                return processOrderChainPayload(orderType, orderChainPayload, post_invalid_order, invalidOrderDuration);
            case "duplicate":
                processOrderChainPayload("valid", orderChainPayload, post_valid_order, validOrderDuration);
                return processOrderChainPayload(orderType, orderChainPayload, post_duplicate_order, duplicateOrderDuration);
            default:
                return undefined;
        }
    }).filter(Boolean);

    const validators = {
        valid: (response, body, payload) => {
            orderKindRateValid.add(response.status === 201);
            highLatencyRate.add(response.timings.duration > 2000);
            validateStandardNotificationShape(response, body, payload, 201);
            if (response.status === 201) {
                http201Created.add(1);
            }
        },
        invalid: (response) => {
            highLatencyRate.add(response.timings.duration > 2000);
            check(response, { "Status is 400 Bad Request": r => r.status === 400 });
            if (response.status === 400) {
                http400Validation.add(1);
            }
        },
        duplicate: (response, body, payload) => {
            highLatencyRate.add(response.timings.duration > 2000);
            validateStandardNotificationShape(response, body, payload, 200);
            if (response.status === 200) {
                http200Duplicate.add(1);
            }
        }
    };

    runValidators(processingResults, validators);
}

export { handleSummary };