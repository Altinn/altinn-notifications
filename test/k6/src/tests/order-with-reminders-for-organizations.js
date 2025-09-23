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
    getFutureDate,
    processOrderChainPayload,
    runValidators,
    handleSummary,
    validOrderDuration,
    invalidOrderDuration,
    duplicateOrderDuration,
    missingResourceOrderDuration,
    highLatencyRate,
    orderKindRateValid,
    http201Created,
    http200Duplicate,
    http400Validation,
    validateStandardNotificationShape
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
    const uniqueIdentifier = uuidv4().substring(0, 8);
    const orderChainPayload = JSON.parse(JSON.stringify(orderChainJsonPayload));

    orderChainPayload.requestedSendTime = getFutureDate(7);
    orderChainPayload.sendersReference = `k6-order-${uniqueIdentifier}`;

    orderChainPayload.dialogportenAssociation = {
        dialogId: uniqueIdentifier,
        transmissionId: uniqueIdentifier
    };

    if (orderChainPayload.recipient?.recipientOrganization) {
        orderChainPayload.recipient.recipientOrganization.resourceId = resourceId;
    }

    if (Array.isArray(orderChainPayload.reminders)) {
        orderChainPayload.reminders = orderChainPayload.reminders.map(reminder => ({
            ...reminder,
            sendersReference: `k6-reminder-order-${uniqueIdentifier}`,
            recipient: {
                ...reminder.recipient,
                recipientOrganization: {
                    ...reminder.recipient.recipientOrganization,
                    resourceId
                }
            }
        }));
    }

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
 * @param {Object} data - Shared setup data
 * @returns {Object} Modified payload
 */
function createUniqueOrderChainPayload(data) {
    const orgNumber = getOrgNoRecipient();
    return {
        ...data.orderChainPayload,
        idempotencyId: uuidv4(),
        recipient: updateRecipientWithOrganizationNumber(
            data.orderChainPayload.recipient,
            orgNumber
        ),
        reminders: Array.isArray(data.orderChainPayload.reminders)
            ? data.orderChainPayload.reminders.map(reminder => ({
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
 * Generates order chain payload variants by order type.
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
                variants.push({ orderType, orderChainPayload: stripRecipientOrganizationFromOrderChainPayload(unique) });
                break;
            case "missingResource":
                variants.push({ orderType, orderChainPayload: removeResourceIdFromOrderChainPayload(unique) });
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
            case "missingResource":
                return processOrderChainPayload(orderType, orderChainPayload, post_order_without_resource_id, missingResourceOrderDuration);
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
        },
        missingResource: (response, body, payload) => {
            highLatencyRate.add(response.timings.duration > 2000);
            validateStandardNotificationShape(response, body, payload, 201);
            if (response.status === 201) {
                http201Created.add(1);
            }
        }
    };

    runValidators(processingResults, validators);
}

export { handleSummary };