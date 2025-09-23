/*
    Test script for registering notification orders and reminders intended for a specific email address.

    Scenarios executed per iteration (aborts only on 401/403 auth errors):
    - 201 Created (valid order)
    - 200 OK (idempotent duplicate)
    - 400 Bad Request (validation failure)

    Idempotency:
    - A unique idempotency identifier is generated for each newly constructed base order.

    Abort policy:
    - Iteration stops early only on 401 or 403 responses.

    Command:    
    podman compose run k6 run /src/tests/order-with-reminders-for-email-address.js \
    -e tokenGeneratorUserName={the user name to access the token generator} \
    -e tokenGeneratorUserPwd={the password to access the token generator} \
    -e mpClientId={the identifier of an integration defined in maskinporten} \
    -e mpKid={the key identifier of the JSON web key used to sign the maskinporten token request} \
    -e encodedJwk={the encoded JSON web key used to sign the maskinporten token request} \
    -e env={the environment to run this script within: at22, at23, at24, yt01, tt02, prod} \
    -e emailRecipient={Email address to include as notification recipient} \
    -e orderTypes={types of orders to test, e.g., valid, invalid or duplicate} \

    Command syntax for different shells:
    - Bash: Use the command as written above.   
    - PowerShell: Replace `\` with a backtick (`` ` ``) at the end of each line.
    - Command Prompt (cmd.exe): Replace `\` with `^` at the end of each line.
*/

import { orderTypes } from "../shared/variables.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { getEmailRecipient } from "../shared/functions.js";
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
    generateOrderChainPayloads
} from "./order-with-reminders-functions.js";

import { post_valid_order, post_invalid_order, post_duplicate_order, setEmptyThresholds } from "./threshold-labels.js";

// Define the order types to be tested based on environment variables or defaults
const labels = [post_valid_order, post_invalid_order, post_duplicate_order];

// Test order chain loaded from a JSON file.
const orderChainJsonPayload = JSON.parse(open("../data/orders/order-with-reminders-for-email-address.json"));

// Export shared options
export const options = buildOptions();

// Set empty thresholds for label-specific metrics
setEmptyThresholds(labels, options);

/**
 * Prepares test data by creating a notification order chain with unique identifiers.
 * 
 * @returns {Object} Test context containing the order chain payload that will be
 *                   used by the default function for each virtual user iteration
 */
export function setup() {
    const emailRecipient = getEmailRecipient();
    if (!emailRecipient) {
        stopIterationOnFail("Missing emailRecipient for this environment", false);
    }

    const { orderChainPayload } = prepareBaseOrderChain(orderChainJsonPayload, {
        mutate: (payload) => {
            if (payload.recipient?.recipientEmail) {
                payload.recipient.recipientEmail.emailAddress = emailRecipient;
            }
            if (Array.isArray(payload.reminders)) {
                payload.reminders = payload.reminders.map(r => {
                    if (r?.recipient?.recipientEmail) {
                        r.recipient.recipientEmail.emailAddress = emailRecipient;
                    }
                    return r;
                });
            }
        },
        reminderSenderPrefix: 'k6-reminder'
    });

    return { orderChainPayload };
}

/**
 * Creates a unique order chain payload with consistent identifiers for API testing.
 * 
 * @param {Object} data - The shared data object containing the base order chain payload template
 * @returns {Object} A cloned order chain payload with unique idempotency identifier
 */
function createUniqueOrderChainPayload(data) {
    return {
        ...data.orderChainPayload,
        idempotencyId: uuidv4()
    };
}

/**
 * Creates an intentionally invalid order chain payload.
 *
 * @param {Object} orderChainPayload - The original valid order chain payload
 * @returns {Object} An invalid order chain payload that should be rejected by the API
 */
function stripRecipientEmailFromOrderChainPayload(orderChainPayload) {
    if (!orderChainPayload) {
        return orderChainPayload;
    }

    return {
        ...orderChainPayload,
        recipient: {
            ...orderChainPayload.recipient,
            recipientEmail: undefined
        },
        reminders: Array.isArray(orderChainPayload.reminders)
            ? orderChainPayload.reminders.map(reminder => ({
                ...reminder,
                recipient: {
                    ...reminder.recipient,
                    recipientEmail: undefined
                }
            }))
            : orderChainPayload.reminders
    };
}

/**
 * Main test function executed for each virtual user iteration.
 *
 * @param {Object} data - Test context from setup phase
 */
export default function (data) {
    const variants = generateOrderChainPayloads(orderTypes, data.orderChainPayload, {
        uniqueFactory: createUniqueOrderChainPayload,
        invalidTransform: stripRecipientEmailFromOrderChainPayload
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

// Re-export shared summary handler
export { handleSummary };