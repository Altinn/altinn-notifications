/*
    Test script for registering notification orders and reminders intended for organizations.

    Scenarios executed per iteration (aborts only on 401/403 auth errors):
    - 201 Created (valid order)
    - 200 OK (idempotent duplicate)
    - 400 Bad Request (validation failure)

    Idempotency:
    - A unique idempotency identifier is generated for each newly constructed base order.

    Abort policy:
    - Iteration stops early only on 401 or 403 responses.

    Command:
    podman compose run k6 run /src/tests/order-with-reminders-for-organizations.js \
    -e tokenGeneratorUserName={the user name to access the token generator} \
    -e tokenGeneratorUserPwd={the password to access the token generator} \
    -e mpClientId={the identifier of an integration defined in maskinporten} \
    -e mpKid={the key identifier of the JSON web key used to sign the maskinporten token request} \
    -e encodedJwk={the encoded JSON web key used to sign the maskinporten token request} \
    -e env={the environment to run this script within: at22, at23, at24, yt01, tt02, prod} \
    -e orgNoRecipient={an organization number to include as a notification recipient} \
    -e orderTypes={types of orders to test, e.g., valid, invalid, duplicate, missingResource} \
*/

import { stopIterationOnFail } from "../errorhandler.js";
import { getOrgNoRecipient } from "../shared/functions.js";
import { resourceId, orderTypes } from "../shared/variables.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import {
    buildOptions,
    runValidators,
    processVariants,
    validOrderDuration,
    invalidOrderDuration,
    prepareBaseOrderChain,
    duplicateOrderDuration,
    buildStandardValidators,
    generateOrderChainPayloads,
    missingResourceOrderDuration
} from "./order-with-reminders-functions.js";
import { post_valid_order, post_invalid_order, post_duplicate_order, post_order_without_resource_id, setEmptyThresholds } from "./threshold-labels.js";

// Define the order types to be tested based on environment variables or defaults
const labels = [post_valid_order, post_invalid_order, post_duplicate_order, post_order_without_resource_id];

// Test order chain loaded from a JSON file.
const orderChainJsonPayload = JSON.parse(open("../data/orders/order-with-reminders-for-organizations.json"));

// Export shared options (extend thresholds with missing_resource_order_duration)
export const options = buildOptions({
    'missing_resource_order_duration': ['p(95)<2000', 'p(99)<3000']
});

// Set empty thresholds for label-specific metrics
setEmptyThresholds(labels, options);

/**
 * Prepares test data by creating a notification order chain with unique identifiers.
 * 
 * @returns {Object} Test context
 */
export function setup() {
    const { orderChainPayload } = prepareBaseOrderChain(orderChainJsonPayload, {
        addDialogAssociation: true,
        mutate: (payload) => {
            if (payload.recipient?.recipientOrganization) {
                payload.recipient.recipientOrganization.resourceId = resourceId;
            }
            if (Array.isArray(payload.reminders)) {
                payload.reminders = payload.reminders.map(r => ({
                    ...r,
                    recipient: {
                        ...r.recipient,
                        recipientOrganization: {
                            ...r.recipient.recipientOrganization,
                            resourceId
                        }
                    }
                }));
            }
        }
    });

    return { orderChainPayload };
}

/**
 * Updates the organization number in a recipient object.
 * 
 * @param {Object} recipient - The recipient object to update
 * @param {string} orgNumber - The organization number to set
 * @returns {Object} New recipient object with updated organization number
 */
function updateRecipientWithOrganizationNumber(recipient, orgNumber) {
    if (!recipient?.recipientOrganization) {
        stopIterationOnFail("Recipient is missing required recipientOrganization property", false);
        return recipient;
    }

    return {
        ...recipient,
        recipientOrganization: {
            ...recipient.recipientOrganization,
            orgNumber
        }
    };
}

/**
 * Removes the resourceId from a recipient object.
 * 
 * @param {Object|null|undefined} recipient - The recipient object to process
 * @returns {Object} A new recipient object with resourceId removed
 */
function stripResourceIdentifierFromRecipient(recipient) {
    if (!recipient?.recipientOrganization) {
        return recipient;
    }

    const { resourceId, ...remainingOrgProperties } = recipient.recipientOrganization;
    return {
        ...recipient,
        recipientOrganization: remainingOrgProperties
    };
}

/**
 * Creates a modified copy of an order chain request without resource identifiers.
 * 
 * @param {Object} baseOrder - The original order chain payload
 * @returns {Object} A new order chain object with all resourceId properties removed
 */
function removeResourceIdFromOrderChainPayload(baseOrder) {
    if (!baseOrder) {
        return baseOrder;
    }
    return {
        ...baseOrder,
        recipient: stripResourceIdentifierFromRecipient(baseOrder.recipient),
        reminders: Array.isArray(baseOrder.reminders)
            ? baseOrder.reminders.map(reminder => ({
                ...reminder,
                recipient: stripResourceIdentifierFromRecipient(reminder.recipient)
            }))
            : baseOrder.reminders
    };
}

/**
 * Creates a unique order chain payload with consistent identifiers for API testing.
 *
 * @param {Object} baseOrderChainPayload - The base order chain payload
 * @returns {Object} Modified payload
 */
function createUniqueOrderChainPayload(baseOrderChainPayload) {
    const orgNumber = getOrgNoRecipient();
    return {
        ...baseOrderChainPayload,
        idempotencyId: uuidv4(),
        recipient: updateRecipientWithOrganizationNumber(
            baseOrderChainPayload.recipient,
            orgNumber
        ),
        reminders: Array.isArray(baseOrderChainPayload.reminders)
            ? baseOrderChainPayload.reminders.map(reminder => ({
                ...reminder,
                recipient: updateRecipientWithOrganizationNumber(
                    reminder.recipient,
                    orgNumber
                )
            }))
            : []
    };
}

/**
 * Creates an intentionally invalid order chain payload.
 *
 * - Removes recipientOrganization for main and reminder recipients
 *
 * @param {Object} orderChainPayload - Original payload
 * @returns {Object} Invalid payload
 */
function stripRecipientOrganizationFromOrderChainPayload(orderChainPayload) {
    if (!orderChainPayload) {
        return orderChainPayload;
    }
    const invalidRecipient = orderChainPayload.recipient ? {
        ...orderChainPayload.recipient,
        recipientOrganization: undefined
    } : undefined;

    const invalidReminders = Array.isArray(orderChainPayload.reminders)
        ? orderChainPayload.reminders.map(reminder => {
            if (!reminder) return reminder;
            return {
                ...reminder,
                recipient: reminder.recipient ? {
                    ...reminder.recipient,
                    recipientOrganization: undefined
                } : undefined
            };
        })
        : orderChainPayload.reminders;

    return {
        ...orderChainPayload,
        recipient: invalidRecipient,
        reminders: invalidReminders
    };
}

/**
 * Main iteration function.
 *
 * @param {Object} data - Setup context
 */
export default function runTests(data) {
    const variants = generateOrderChainPayloads(orderTypes, data.orderChainPayload, {
        uniqueFactory: createUniqueOrderChainPayload,
        invalidTransform: stripRecipientOrganizationFromOrderChainPayload,
        missingResourceTransform: removeResourceIdFromOrderChainPayload
    });

    const processingResults = processVariants(variants, {
        labelMap: {
            valid: post_valid_order,
            invalid: post_invalid_order,
            duplicate: post_duplicate_order,
            missingResource: post_order_without_resource_id
        },
        durationMetrics: {
            valid: validOrderDuration,
            invalid: invalidOrderDuration,
            duplicate: duplicateOrderDuration,
            missingResource: missingResourceOrderDuration
        }
    });

    const validators = buildStandardValidators({ includeMissingResource: true });
    runValidators(processingResults, validators);
}

export { handleSummary } from "./order-with-reminders-functions.js";