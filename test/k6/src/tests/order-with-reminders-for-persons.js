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
    runValidators,
    handleSummary,
    processVariants,
    validOrderDuration,
    invalidOrderDuration,
    prepareBaseOrderChain,
    duplicateOrderDuration,
    buildStandardValidators,
    generateOrderChainPayloads,
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
    // Person script needs dialogportenAssociation
    const { orderChainPayload } = prepareBaseOrderChain(orderChainJsonPayload, {
        addDialogAssociation: true
    });
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
 * Main iteration function.
 *
 * @param {Object} data - Setup context
 */
export default function (data) {
    const variants = generateOrderChainPayloads(orderTypes, data.orderChainPayload, {
        uniqueFactory: createUniqueOrderChainPayload,
        invalidTransform: stripRecipientPersonFromOrderChainPayload
    });

    const processingResults = processVariants(variants, {
        labelMap: {
            valid: post_valid_order,
            invalid: post_invalid_order,
            duplicate: post_duplicate_order
        },
        durationMetrics: {
            valid: validOrderDuration,
            invalid: invalidOrderDuration,
            duplicate: duplicateOrderDuration
        }
    });

    const validators = buildStandardValidators();
    runValidators(processingResults, validators);
}

export { handleSummary };