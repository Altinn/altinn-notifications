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
    const uniqueIdentifier = uuidv4().substring(0, 8);
    const orderChainPayload = JSON.parse(JSON.stringify(orderChainJsonPayload));

    orderChainPayload.requestedSendTime = getFutureDate(7);
    orderChainPayload.sendersReference = `k6-order-${uniqueIdentifier}`;

    if (orderChainPayload.recipient?.recipientSms) {
        orderChainPayload.recipient.recipientSms.phoneNumber = getSmsRecipient();
    }

    if (Array.isArray(orderChainPayload.reminders)) {
        orderChainPayload.reminders = orderChainPayload.reminders.map(reminder => {
            const updatedReminder = {
                ...reminder,
                sendersReference: `k6-reminder-${uniqueIdentifier}`
            };
            if (updatedReminder.recipient?.recipientSms) {
                updatedReminder.recipient.recipientSms.phoneNumber = getSmsRecipient();
            }
            return updatedReminder;
        });
    }

    return { orderChainPayload };
}

/**
 * Creates a unique order chain payload with consistent identifiers for API testing.
 *
 * @param {Object} data - The shared data object
 * @returns {Object} Modified order chain payload
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
 * Generates order chain payload variants by order type.
 *
 * @param {Object} data - Setup data
 * @returns {Array<Object>} Variants
 */
function generateOrderChainPayloadsByOrderType(data) {
    const variants = [];
    for (const orderType of orderTypes) {
        const uniquePayload = createUniqueOrderChainPayload(data);
        switch (orderType) {
            case "valid":
            case "duplicate":
                variants.push({ orderType, orderChainPayload: uniquePayload });
                break;
            case "invalid":
                variants.push({ orderType, orderChainPayload: stripRecipientSmsFromOrderChainPayload(uniquePayload) });
                break;
        }
    }
    return variants;
}

/**
 * Main iteration function.
 *
 * @param {Object} data - From setup
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