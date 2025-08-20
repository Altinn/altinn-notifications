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

    Metrics & thresholds:
    - success_rate reflects non-4xx/5xx success proportion.
    - Duration trends: valid_order_duration, duplicate_order_duration, invalid_order_duration,
    - Counters: http_201_created, http_200_duplicate, http_400_validation, http_4xx, http_5xx, failed_requests.
*/


import { check } from "k6";
import * as setupToken from "../setup.js";
import { Trend, Counter, Rate } from "k6/metrics";
import * as ordersApi from "../api/notifications/v2.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { getOrgNoRecipient } from "../shared/functions.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { scopes, resourceId, orderTypes } from "../shared/variables.js";
import { textSummary } from "https://jslib.k6.io/k6-summary/0.0.1/index.js";
import { post_valid_order, post_invalid_order, post_duplicate_order, post_order_without_resource_id, setEmptyThresholds } from "./threshold-labels.js";

// Variables to cache and renew the token
let cachedToken = null;
let tokenExpiration = 0;

// Track first successful 201 for idempotency comparisons
let firstSuccessful = { notificationOrderId: null, shipmentId: null };

// Rate to track the proportion of successful requests (non-4xx/5xx responses) to total requests
const successRate = new Rate("success_rate");

// Rate to track the proportion of requests that exceed a defined latency threshold (e.g., 2 seconds)
const highLatencyRate = new Rate("high_latency_rate");

// Rate to track the proportion of requests that result in 4xx client errors
const serverErrorRate = new Rate("server_error_rate");

// Rate to track the proportion of requests that result in 5xx server errors
const orderKindRateValid = new Rate("order_valid_success_rate");

// Rate to track the proportion of requests that result in 5xx server errors
const duplicateMismatchRate = new Rate("duplicate_mismatch_rate");

// Counter to track the number of HTTP responses with 4xx status codes (client errors).
const http4xx = new Counter("http_4xx");

// Counter to track the number of HTTP responses with 5xx status codes (server errors).
const http5xx = new Counter("http_5xx");

// Counter to track the number of failed requests (4xx and 5xx responses).
const failedRequests = new Counter("failed_requests");

// Counter to track the number of HTTP responses with 201 Created status (valid orders).
const http201Created = new Counter("http_201_created");

// Counter to track the number of HTTP responses with 200 Ok status (duplicate orders).
const http200Duplicate = new Counter("http_200_duplicate");

// Counter to track the number of HTTP responses with 400 Bad Request status (validation errors).
const http400Validation = new Counter("http_400_validation");

// Trend to track the response time (duration) for valid orders (expected to return a 201 Created status)
const validOrderDuration = new Trend("valid_order_duration");

// Trend to track the response time (duration) for invalid orders (expected to return a 400 Bad Request status)
const invalidOrderDuration = new Trend("invalid_order_duration");

// Trend to track the response time (duration) for valid but duplicate orders (expected to return a 200 Ok status)
const duplicateOrderDuration = new Trend("duplicate_order_duration");

// Trend to track the response time (duration) for orders missing a resource (expected to return a 201 Created or 200 Ok status)
const missingResourceOrderDuration = new Trend("missing_resource_order_duration");

// Define the order types to be tested based on environment variables or defaults
const labels = [post_valid_order, post_invalid_order, post_duplicate_order, post_order_without_resource_id];

// Test data for order chain requests, loaded from a JSON file.
const orderChainRequestJson = JSON.parse(open("../data/orders/order-with-reminders-for-organizations.json"));

// Define the test scenarios for different performance dimensions
export const options = {
    scenarios: {
        // 1. Quick readiness gate to verify that the system works and meets minimal expectations.
        smoke: {
            rate: 10,
            maxVUs: 10,
            timeUnit: '1s',
            duration: '30s',
            preAllocatedVUs: 5,
            executor: 'constant-arrival-rate'
        },

        // 2. Capacity test with gradually increasing request rate to evaluate system limits.
        capacity_probe: {
            maxVUs: 400,
            startRate: 50,
            timeUnit: '1s',
            startTime: '40s',
            gracefulStop: '30s',
            preAllocatedVUs: 120,
            executor: 'ramping-arrival-rate',
            stages: [
                { target: 100, duration: '2m' },
                { target: 200, duration: '2m' },
                { target: 300, duration: '3m' },
                { target: 0, duration: '1m' }
            ]
        },

        // 3. Load test to validate system performance against realistic SLA/SLO expectations.
        realistic_sla_compliance: {
            maxVUs: 500,
            startRate: 100,
            timeUnit: '1s',
            startTime: '10m',
            gracefulStop: '30s',
            preAllocatedVUs: 250,
            executor: 'ramping-arrival-rate',
            stages: [
                { target: 400, duration: '4m' },
                { target: 700, duration: '6m' },
                { target: 0, duration: '2m' }
            ]
        },

        // 4. Steady-state load to validate system stability under sustained traffic.
        steady_state_load: {
            rate: 300,
            maxVUs: 600,
            timeUnit: '1s',
            duration: '15m',
            startTime: '22m',
            preAllocatedVUs: 300,
            gracefulStop: '45s',
            executor: 'constant-arrival-rate'
        },

        // 5. Sudden spike load to evaluate system resilience under abrupt traffic surges.
        sudden_spike_resilience: {
            maxVUs: 700,
            timeUnit: '1s',
            startTime: '37m',
            gracefulStop: '30s',
            preAllocatedVUs: 350,
            executor: 'ramping-arrival-rate',
            stages: [
                { target: 50, duration: '10s' },
                { target: 1500, duration: '20s' },
                { target: 600, duration: '2m' },
                { target: 0, duration: '40s' }
            ]
        },

        // 6. Soak-long-running stability validation under sustained load.
        soak_long_term_stability: {
            rate: 120,
            maxVUs: 200,
            timeUnit: '1s',
            duration: '30m',
            startTime: '46m',
            gracefulStop: '2m',
            preAllocatedVUs: 120,
            executor: 'constant-arrival-rate'
        }
    },
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)', 'count'],
    thresholds: {
        'checks': ['rate>0.995'],
        'http_5xx': ['count<20'],
        'success_rate': ['rate>0.995'],
        'server_error_rate': ['rate<0.02'],
        'high_latency_rate': ['rate<0.05'],
        'dropped_iterations': ['count==0'],
        'http_req_duration': ['p(95)<1500', 'p(99)<2500'],
        'valid_order_duration': ['p(95)<1200', 'p(99)<1800'],
        'invalid_order_duration': ['p(95)<400', 'p(99)<600'],
        'duplicate_order_duration': ['p(95)<800', 'p(99)<1200'],
        'missing_resource_order_duration': ['p(95)<1300', 'p(99)<1900'],
        'http_req_duration{scenario:steady_state_load}': ['p(95)<1500', 'p(99)<2200'],
        'http_req_duration{scenario:realistic_sla_compliance}': ['p(95)<1500', 'p(99)<2200'],
        'http_req_duration{scenario:soak_long_term_stability}': ['p(95)<1700', 'p(99)<2400']
    }
};

/**
 * Prepares shared data and configurations for the test.
 *
 * This function runs once before the test execution begins and is used to generate
 * an authentication token and prepare the order chain request with unique identifiers.
 *
 * @returns {Object} An object containing:
 * - `orderChainRequest` {Object}: The prepared order chain request.
 */
export function setup() {
    const formattedFutureDate = getFutureDate(7);
    const randomIdentifier = uuidv4().substring(0, 8);

    const orderChainRequest = JSON.parse(JSON.stringify(orderChainRequestJson));
    orderChainRequest.dialogportenAssociation = {
        dialogId: randomIdentifier,
        transmissionId: randomIdentifier
    };

    orderChainRequest.requestedSendTime = formattedFutureDate;
    orderChainRequest.sendersReference = `k6-order-${randomIdentifier}`;
    orderChainRequest.recipient.recipientOrganization.resourceId = resourceId;

    orderChainRequest.reminders = orderChainRequest.reminders.map((reminder) => {
        reminder.recipient.recipientOrganization.resourceId = resourceId;
        reminder.sendersReference = `k6-reminder-order-${randomIdentifier}`;
        return reminder;
    });

    return { orderChainRequest };
}

/**
 * Executes test cases for sending and validating different types of order chain requests
 * against the Notification API. Covers both positive and negative scenarios.
 *
 * Order types:
 * - `validOrder`: A well-formed order (expected => 201 Created).
 * - `invalidOrder`: An order with structural errors (expected => 400 Bad Request).
 * - `duplicateOrder`: A re-submission of a previously valid order (expected => 200 OK with same IDs).
 * - `missingResourceOrder`: An order missing resource identifiers (expected => 201 Created).
 *
 * Behavior:
 * - Each order type is sent to the API, metrics are recorded, and the response
 *   is validated against expected outcomes.
 * - On critical authentication/authorization errors (401/403), iteration stops early.
 *
 * @param {Object} data - The shared context prepared in the `setup` step, containing:
 * @param {Object} data.orderChainRequest - The base order chain request template.
 */
export default function (data) {
    const orgNumber = getOrgNoRecipient();
    const orderChainRequests = generateOrderChainRequestByOrderType(orderTypes, data, orgNumber);

    // Process orders and collect responses directly
    const responses = [
        processOrder("valid", orderChainRequests.validOrder, post_valid_order, validOrderDuration),
        processOrder("invalid", orderChainRequests.invalidOrder, post_invalid_order, invalidOrderDuration),
        processOrder("duplicate", orderChainRequests.duplicateOrder, post_duplicate_order, duplicateOrderDuration),
        processOrder("missingResource", orderChainRequests.missingResourceOrder, post_order_without_resource_id, missingResourceOrderDuration)
    ].filter(Boolean); // Remove any undefined results (skipped orders)

    // Validate collected responses
    validateResponses(responses);
}

function validateResponses(responses) {
    if (!responses || responses.length === 0) return;

    const validators = {
        valid: (response, body, sourceOrder) => {
            // Create named check functions for better error reporting
            const checks = {
                "Valid org order => 201": r => r.status === 201,
                "valid: has shipmentId": () => !!body.notification?.shipmentId,
                "valid: has notificationOrderId": () => !!body.notificationOrderId,
                "valid: reminders array present": () => Array.isArray(body.notification?.reminders),
                "valid: reminder count matches request": () => {
                    const hasReminders = Array.isArray(body.notification?.reminders);
                    const actualCount = hasReminders ? body.notification.reminders.length : 0;
                    const expectedCount = sourceOrder.reminders?.length || 0;
                    return actualCount === expectedCount;
                },
                "valid: each reminder has shipmentId": () => (body.notification?.reminders || []).every(r => !!r.shipmentId)
            };

            // Run checks and collect results
            const results = check(response, checks);

            // If any check failed, provide detailed error message
            if (!results) {
                // Find which specific checks failed
                const failedChecks = Object.entries(checks)
                    .filter(([name, checkFn]) => !checkFn())
                    .map(([name]) => name)
                    .join(", ");

                // Create an informative error message with contextual data
                let errorMsg = `Validation failed for valid order. Failed checks: ${failedChecks}`;

                // Add context about the response
                errorMsg += `\nStatus: ${response.status}`;
                if (response.status !== 201) {
                    errorMsg += `\nExpected status: 201`;
                }

                // Include abbreviated response body for debugging
                try {
                    const bodyPreview = JSON.stringify(body).substring(0, 200) +
                        (JSON.stringify(body).length > 200 ? "..." : "");
                    errorMsg += `\nResponse preview: ${bodyPreview}`;
                } catch (e) {
                    errorMsg += "\nCould not stringify response body";
                }

                stopIterationOnFail(errorMsg, false);
                return;
            }

            if (response.status === 201) {
                http201Created.add(1);
                firstSuccessful.shipmentId ||= body.notification?.shipmentId;
                firstSuccessful.notificationOrderId ||= body.notificationOrderId;
            }
        },

        // Other validators remain the same
        invalid: (response) => {
            check(response, { "Invalid order => 400": r => r.status === 400 });

            if (response.status === 400) {
                http400Validation.add(1);
            }
        },

        duplicate: (response, body) => {
            check(response, {
                "Duplicate => 200": r => r.status === 200,
                "Duplicate: same shipmentId": () => !firstSuccessful.shipmentId || body.notification?.shipmentId === firstSuccessful.shipmentId,
                "Duplicate: same notificationOrderId": () => !firstSuccessful.notificationOrderId || body.notificationOrderId === firstSuccessful.notificationOrderId
            });

            if (response.status === 200) {
                http200Duplicate.add(1);
            }
        },

        missingResource: (response) => {
            check(response, { "Missing resource => 201": r => r.status === 201 });

            if (response.status === 201) {
                http201Created.add(1);
            }
        }
    };

    for (const { kind, response, sourceOrder } of responses) {
        // Add null check to prevent accessing properties of undefined
        if (!response) {
            console.log(`Skipping undefined response for kind: ${kind}`);
            continue;
        }

        if (response.status === 401 || response.status === 403) {
            stopIterationOnFail("Critical authentication/authorization error encountered", false);
            break;
        }

        let body;
        try { body = JSON.parse(response.body); } catch { body = {}; }

        validators[kind]?.(response, body, sourceOrder);
    }
}

/**
 * Generates a custom summary of the test results after the test execution.
 *
 * @param {Object} testResults - The aggregated test results object containing metrics, thresholds, and other performance data collected during the test.
 * @returns {Object} An object containing:
 * - `summary.json`: A JSON string representation of the aggregated test results, saved to a file.
 * - `stdout`: A human-readable summary of the test results formatted for terminal output.
 *
 * The `stdout` output is generated using the `textSummary` function, which formats the results with indentation and optional colorization.
 * The `summary.json` output is useful for further analysis or integration with external tools.
 */
export function handleSummary(testResults) {
    return {
        "summary.json": JSON.stringify(testResults),
        stdout: textSummary(testResults, { indent: "  ", enableColors: true })
    };
}

/**
 * Configures threshold labels for specific test scenarios and ensures they are tracked with empty thresholds.
 *
 * @constant {string[]} labels - An array of labels representing different test scenarios:
 * - `post_valid_order`: Tracks metrics for valid orders.
 * - `post_invalid_order`: Tracks metrics for invalid orders.
 * - `post_duplicate_order`: Tracks metrics for duplicate orders.
 * - `post_order_without_resource_id`: Tracks metrics for orders missing a resource identifier.
 *
 * @function setEmptyThresholds
 * @param {string[]} labels - The array of labels for which thresholds are being configured.
 * @param {object} options - The options object containing the thresholds configuration for the test.
 * 
 * This function ensures that each label in the `labels` array is included in the `options` object with an empty threshold.
 * Empty thresholds allow metrics to be collected for these scenarios without enforcing specific performance criteria.
 * This is particularly useful for exploratory testing or when tracking is required without strict validation.
 */
setEmptyThresholds(labels, options);

/**
 * Updates performance metrics based on an HTTP response.
 *
 * Categorizes the response as successful, client error (4xx), or server error (5xx),
 * then increments the appropriate counters and adjusts success/error rates.
 *
 * @param {Object} httpResponse - The HTTP response object.
 * @param {number} httpResponse.status - The HTTP status code.
 */
function recordResult(httpResponse) {
    const status = httpResponse.status;
    const isServerError = status >= 500;
    const isClientError = status >= 400 && status < 500;

    if (isClientError || isServerError) {
        failedRequests.add(1);

        if (isServerError) {
            http5xx.add(1);
            serverErrorRate.add(true);
        } else {
            http4xx.add(1);
            serverErrorRate.add(false);
        }

        successRate.add(false);
        return;
    }

    successRate.add(true);
    serverErrorRate.add(false);
}

/**
 * Generates a date string for the current date plus the specified number of days.
 *
 * @param {number} daysToAdd - The number of days to add to the current date.
 * @returns {string} The formatted future date as a UTC string.
 */
function getFutureDate(daysToAdd = 0) {
    const currentDate = new Date();
    const futureDate = new Date(currentDate);
    futureDate.setDate(currentDate.getDate() + daysToAdd);
    return futureDate.toISOString();
}

/**
 * Creates a modified copy of an order object without `resourceId` fields.
 * Specifically, it removes the `resourceId` property from:
 * - The main recipient's `recipientOrganization` object.
 * - Each reminder's `recipientOrganization` object.
 *
 * This helper is used in tests to simulate orders that are missing
 * required resource identifiers, e.g. to verify validation logic or
 * behavior when Profile/Authorization API calls are bypassed.
 *
 * @param {Object} baseOrder - The original order object.
 * @param {Object} baseOrder.recipient - The main recipient details.
 * @param {Object} baseOrder.recipient.recipientOrganization - Organization details of the recipient.
 * @param {Array}  baseOrder.reminders - List of reminder objects with recipients.
 * @returns {Object} A cloned order object with all `resourceId` properties removed.
 */
function removeResourceIdFromOrder(baseOrder) {
    const stripResourceId = (recipient) => {
        if (!recipient?.recipientOrganization) return recipient;
        const { resourceId, ...restOrg } = recipient.recipientOrganization;
        return { ...recipient, recipientOrganization: restOrg };
    };

    return {
        ...baseOrder,
        recipient: stripResourceId(baseOrder.recipient),
        reminders: baseOrder.reminders.map(reminder => ({
            ...reminder,
            recipient: stripResourceId(reminder.recipient)
        }))
    };
}

/**
 * Creates a modified copy of an order object with missing required fields.
 * Specifically, it removes the `recipientOrganization` property from:
 * - The main `recipient` object.
 * - Every reminder's `recipient` object.
 *
 * This is intended for generating invalid orders in tests,
 * ensuring the system correctly validates input and returns error responses.
 *
 * @param {Object} baseOrder - The original order object.
 * @param {Object} baseOrder.recipient - The main order recipient.
 * @param {Array} baseOrder.reminders - List of reminder objects.
 * @returns {Object} A cloned order object with `recipientOrganization` removed.
 */
function removeRequiredFieldsFromOrder(baseOrder) {
    return {
        ...baseOrder,
        recipient: {
            ...baseOrder.recipient,
            recipientOrganization: undefined
        },
        reminders: baseOrder.reminders.map(reminder => ({
            ...reminder,
            recipient: {
                ...reminder.recipient,
                recipientOrganization: undefined
            }
        }))
    };
}

/**
 * Creates a unique copy of an order chain request with updated identifiers and organization details.
 *
 * @param {Object} data - The shared data object containing the base `orderChainRequest`.
 * @param {string} orgNumber - The organization number that should be used to identify the recipients.
 * 
 * @returns {Object} A unique order chain request.
 */
function createUniqueOrderChainRequest(data, orgNumber) {
    const { orderChainRequest } = data;

    const base = {
        ...orderChainRequest,
        idempotencyId: uuidv4(),
        recipient: {
            ...orderChainRequest.recipient,
            recipientOrganization: {
                ...orderChainRequest.recipient.recipientOrganization,
                orgNumber
            }
        },
        reminders: orderChainRequest.reminders.map(reminder => ({
            ...reminder,
            recipient: {
                ...reminder.recipient,
                recipientOrganization: {
                    ...reminder.recipient.recipientOrganization,
                    orgNumber
                }
            }
        }))
    };

    return base;
}

/**
 * Processes an order by sending it to the Notification API, recording metrics, and tracking response duration.
 *
 * This function handles the following:
 * - Sends the specified order to the API using the provided label for tagging/metrics.
 * - Records the response status and updates relevant counters (e.g., success, client error, server error).
 * - Tracks the response duration and adds it to the specified duration metric.
 * - Returns the response and associated metadata for further validation or processing.
 *
 * @param {string} kind - The type of order being processed (e.g., "valid", "invalid", "duplicate", "missingResource").
 * @param {Object} order - The order payload to be sent to the API. If null or undefined, the function exits early.
 * @param {string} label - A label used for tagging the request in metrics (e.g., "post_valid_order").
 * @param {Trend} durationMetric - A k6 Trend metric to track the response time for this order type.
 * @returns {Object|undefined} The response data or undefined if order was skipped
 */
function processOrder(kind, order, label, durationMetric) {
    if (!order) return undefined;

    const response = sendNotificationOrderChain(order, label);
    recordResult(response);
    durationMetric.add(response.timings.duration);

    return { kind, response, sourceOrder: order };
}

/**
 * Generates order chain requests based on the specified order types.
 *
 * @param {Array<string>} orderTypes - The types of orders to generate. Supported values:
 * - `"valid"`: Creates an order chain request with valid data.
 * - `"invalid"`: Creates an order chain request with invalid data.
 * - `"duplicate"`: Creates two identical order chain requests with valid data.
 * - `"missingResource"`: Creates an order chain request missing the resource identifiers.
 * - `"all"`: Generates all the above types.
 *
 * @param {Object} data - The shared data containing the base `orderChainRequest`.
 * @param {string} orgNumber - The organization number for the recipient.
 *
 * @returns {Object} An object containing the generated order requests:
 * - `validOrder` {Object}
 * - `invalidOrder` {Object}
 * - `duplicateOrder` {Object}
 * - `missingResourceOrder` {Object}
 *
 * @throws {Error} If an unsupported order type is provided.
 */
function generateOrderChainRequestByOrderType(orderTypes, data, orgNumber) {
    const orderChainRequests = {};

    for (const orderType of orderTypes) {
        switch (orderType) {
            case "valid":
                orderChainRequests.validOrder = createUniqueOrderChainRequest(data, orgNumber);
                break;

            case "invalid":
                orderChainRequests.invalidOrder = removeRequiredFieldsFromOrder(createUniqueOrderChainRequest(data, orgNumber));
                break;

            case "duplicate":
                if (!orderChainRequests.validOrder) {
                    orderChainRequests.validOrder = createUniqueOrderChainRequest(data, orgNumber);
                }
                orderChainRequests.duplicateOrder = JSON.parse(JSON.stringify(orderChainRequests.validOrder));
                break;

            case "missingResource":
                orderChainRequests.missingResourceOrder = removeResourceIdFromOrder(createUniqueOrderChainRequest(data, orgNumber));
                break;

            case "all":
                orderChainRequests.validOrder = createUniqueOrderChainRequest(data, orgNumber);
                orderChainRequests.duplicateOrder = JSON.parse(JSON.stringify(orderChainRequests.validOrder));
                orderChainRequests.invalidOrder = removeRequiredFieldsFromOrder(createUniqueOrderChainRequest(data, orgNumber));
                orderChainRequests.missingResourceOrder = removeResourceIdFromOrder(createUniqueOrderChainRequest(data, orgNumber));
                break;

            default:
                stopIterationOnFail(`Invalid orderType: ${orderType}`, false);
        }
    }
    return orderChainRequests;
}

/**
 * Sends a notification order chain request to the Notification API.
 *
 * Token handling:
 * - Uses a cached Altinn token until its artificial expiration (1680s from acquisition).
 * - The JWT `exp` claim is intentionally ignored to reduce parsing overhead;
 *   instead, a fixed lifetime is assumed.
 *
 * @param {Object} orderRequest - The order chain payload to send.
 * @param {string} [label='post_valid_order'] - Label used for logging/metrics.
 * @returns {Object} The HTTP response from the Notification API.
 */
function sendNotificationOrderChain(orderRequest, label = 'post_valid_order') {
    const requestBody = JSON.stringify(orderRequest);

    const secondsNow = Math.floor(Date.now() / 1000);
    const needsRefresh = !cachedToken || secondsNow >= tokenExpiration;

    if (needsRefresh) {
        const tokenObj = setupToken.getAltinnTokenForOrg(scopes);
        cachedToken = tokenObj;
        tokenExpiration = secondsNow + 1680;
    }

    return ordersApi.postNotificationOrderV2(requestBody, cachedToken, label);
}