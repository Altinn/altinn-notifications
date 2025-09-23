/*
    Test script for registering notification orders and reminders intended for a specific mobile number.

    Scenarios executed per iteration (aborts only on 401/403 auth errors):
    - 201 Created (valid order)
    - 200 OK (idempotent duplicate)
    - 400 Bad Request (validation failure)

    Idempotency:
    - A unique idempotency identifier is generated for each newly constructed base order.

    Abort policy:
    - Iteration stops early only on 401 or 403 responses.

    Command:    
    podman compose run k6 run /src/tests/order-with-reminders-for-mobile-number.js \
    -e tokenGeneratorUserName={the user name to access the token generator} \
    -e tokenGeneratorUserPwd={the password to access the token generator} \
    -e mpClientId={the identifier of an integration defined in maskinporten} \
    -e mpKid={the key identifier of the JSON web key used to sign the maskinporten token request} \
    -e encodedJwk={the encoded JSON web key used to sign the maskinporten token request} \
    -e env={the environment to run this script within: at22, at23, at24, yt01, tt02, prod} \
    -e mobileNumber={Mobile phone number in international format to include as notification recipient} \
    -e orderTypes={types of orders to test, e.g., valid, invalid or duplicate} \

    Command syntax for different shells:
    - Bash: Use the command as written above.   
    - PowerShell: Replace `\` with a backtick (`` ` ``) at the end of each line.
    - Command Prompt (cmd.exe): Replace `\` with `^` at the end of each line.
*/

import { orderTypes } from "../shared/variables.js";
import { getSmsRecipient } from "../shared/functions.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import {
    buildOptions,
    handleSummary,
    runValidators,
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
const orderChainJsonPayload = JSON.parse(open("../data/orders/order-with-reminders-for-mobile-number.json"));

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
    const { orderChainPayload } = prepareBaseOrderChain(orderChainJsonPayload, {
        mutate: (payload) => {
            if (payload.recipient?.recipientSms) {
                payload.recipient.recipientSms.phoneNumber = getSmsRecipient();
            }
            if (Array.isArray(payload.reminders)) {
                payload.reminders = payload.reminders.map(r => {
                    if (r?.recipient?.recipientSms) {
                        r.recipient.recipientSms.phoneNumber = getSmsRecipient();
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
 * @param {Object} baseOrderChainPayload- The shared data object
 * @returns {Object} Modified order chain payload
 */
function createUniqueOrderChainPayload(baseOrderChainPayload) {
    return {
        ...baseOrderChainPayload,
        idempotencyId: uuidv4()
    };
}

/**
 * Creates an intentionally invalid order chain payload.
 *
 * - Removes recipientSms in main and reminder recipients
 *
 * @param {Object} orderChainPayload - Original payload
 * @returns {Object} Invalid payload
 */
function stripRecipientSmsFromOrderChainPayload(orderChainPayload) {
    if (!orderChainPayload) {
        return orderChainPayload;
    }
    return {
        ...orderChainPayload,
        recipient: {
            ...orderChainPayload.recipient,
            recipientSms: undefined
        },
        reminders: orderChainPayload.reminders.map(reminder => ({
            ...reminder,
            recipient: {
                ...reminder.recipient,
                recipientSms: undefined
            }
        }))
    };
}

/**
 * Main iteration function.
 *
 * @param {Object} data - From setup
 */
export default function (data) {
    const variants = generateOrderChainPayloads(orderTypes, data.orderChainPayload, {
        uniqueFactory: createUniqueOrderChainPayload,
        invalidTransform: stripRecipientSmsFromOrderChainPayload
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