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
    -e orderTypes={types of orders to test, e.g., valid, invalid, duplicate, missingResource} \

    Command syntax for different shells:
    - Bash: Use the command as written above.   
    - PowerShell: Replace `\` with a backtick (`` ` ``) at the end of each line.
    - Command Prompt (cmd.exe): Replace `\` with `^` at the end of each line.
*/

import { check } from "k6";
import * as setupToken from "../setup.js";
import { Trend, Counter, Rate } from "k6/metrics";
import * as ordersApi from "../api/notifications/v2.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { getSmsRecipient } from "../shared/functions.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { textSummary } from "https://jslib.k6.io/k6-summary/0.0.1/index.js";
import { scopes, orderTypes, performanceTestScenario } from "../shared/variables.js";
import { post_valid_order, post_invalid_order, post_duplicate_order, setEmptyThresholds } from "./threshold-labels.js";

// Rate to track the proportion of successful requests (non-4xx/5xx responses) to total requests
const successRate = new Rate("success_rate");

// Rate to track the proportion of requests that exceed a defined latency threshold (e.g., 2 seconds)
const highLatencyRate = new Rate("high_latency_rate");

// Rate to track the proportion of requests that result in 5xx server errors
const serverErrorRate = new Rate("server_error_rate");

// Rate to track the proportion of requests that result in 201 Created responses
const orderKindRateValid = new Rate("order_valid_success_rate");

// Counter to track the number of HTTP responses with 4xx status codes (client errors)
const http4xx = new Counter("http_4xx");

// Counter to track the number of HTTP responses with 5xx status codes (server errors)
const http5xx = new Counter("http_5xx");

// Counter to track the number of failed requests (4xx and 5xx responses)
const failedRequests = new Counter("failed_requests");

// Counter to track the number of HTTP responses with 201 Created status (valid orders)
const http201Created = new Counter("http_201_created");

// Counter to track the number of HTTP responses with 200 Ok status (duplicate orders)
const http200Duplicate = new Counter("http_200_duplicate");

// Counter to track the number of HTTP responses with 400 Bad Request status (validation errors)
const http400Validation = new Counter("http_400_validation");

// Trend to track the response time (duration) for valid orders (expected to return a 201 Created status)
const validOrderDuration = new Trend("valid_order_duration");

// Trend to track the response time (duration) for invalid orders (expected to return a 400 Bad Request status)
const invalidOrderDuration = new Trend("invalid_order_duration");

// Trend to track the response time (duration) for valid but duplicate orders (expected to return a 200 Ok status)
const duplicateOrderDuration = new Trend("duplicate_order_duration");

// Define the order types to be tested based on environment variables or defaults
const labels = [post_valid_order, post_invalid_order, post_duplicate_order];

// Test order chain loaded from a JSON file.
const orderChainJsonPayload = JSON.parse(open("../data/orders/order-with-reminders-for-mobile-number.json"));

// Define the test scenarios for different performance dimensions
export const options = {
    scenarios: performanceTestScenario === 'userDefined' ?
        {
            // Single custom scenario with configurable parameters
            userDefined: {
                //timeUnit: '1s',
                //rate: customRate,
                //gracefulStop: '10s',
                executor: 'constant-vus',
                tags: { scenario: 'custom' },
                duration: __ENV.duration || '30s',
                vus: parseInt(__ENV.vus || '10', 10),
                //preAllocatedVUs: Math.ceil(customVUs * 0.5)
            }
        }
        :
        {
            // 1. Quick readiness gate to verify that the system works and meets minimal expectations.
            smoke: {
                rate: 10,
                maxVUs: 10,
                timeUnit: '1s',
                duration: '30s',
                preAllocatedVUs: 5,
                gracefulStop: '10s',
                executor: 'constant-arrival-rate'
            },

            // 2. Capacity test with gradually increasing request rate to evaluate system limits.
            capacity_probe: {
                maxVUs: 400,
                startRate: 50,
                timeUnit: '1s',
                startTime: '45s',
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
                startTime: '9m20s',
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
                gracefulStop: '30s',
                preAllocatedVUs: 250,
                executor: 'constant-arrival-rate'
            },

            // 5. Sudden spike load to evaluate system resilience under abrupt traffic surges.
            sudden_spike_resilience: {
                maxVUs: 700,
                timeUnit: '1s',
                startTime: '37m5s',
                gracefulStop: '30s',
                preAllocatedVUs: 350,
                executor: 'ramping-arrival-rate',
                stages: [
                    { target: 50, duration: '10s' },
                    { target: 300, duration: '20s' },
                    { target: 1000, duration: '2m' },
                    { target: 0, duration: '40s' }
                ]
            },

            // 6. Soak-long-running stability validation under sustained load.
            soak_long_term_stability: {
                rate: 120,
                maxVUs: 200,
                timeUnit: '1s',
                duration: '30m',
                gracefulStop: '2m',
                startTime: '40m45s',
                preAllocatedVUs: 120,
                executor: 'constant-arrival-rate'
            }
        },
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)', 'count'],
    thresholds: {
        'checks': ['rate>0.99'],
        'success_rate': ['rate>0.70'],
        'server_error_rate': ['rate<0.01'],
        'high_latency_rate': ['rate<0.10'],
        'order_valid_success_rate': ['rate>0.995'],
        'http_req_duration': ['p(95)<2200', 'p(99)<4000'],
        'dropped_iterations{scenario:smoke}': ['count==0'],
        'valid_order_duration': ['p(95)<1800', 'p(99)<2500'],
        'invalid_order_duration': ['p(95)<800', 'p(99)<1200'],
        'duplicate_order_duration': ['p(95)<1000', 'p(99)<1500'],
        'dropped_iterations{scenario:capacity_probe}': ['count<20'],
        'dropped_iterations{scenario:steady_state_load}': ['count==0'],
        'http_req_duration{scenario:smoke}': ['p(95)<1200', 'p(99)<1800'],
        'dropped_iterations{scenario:sudden_spike_resilience}': ['count<50'],
        'dropped_iterations{scenario:realistic_sla_compliance}': ['count<5'],
        'dropped_iterations{scenario:soak_long_term_stability}': ['count<10'],
        'http_req_duration{scenario:capacity_probe}': ['p(95)<2500', 'p(99)<4000'],
        'http_req_duration{scenario:steady_state_load}': ['p(95)<2000', 'p(99)<3000'],
        'http_req_duration{scenario:sudden_spike_resilience}': ['p(95)<3500', 'p(99)<5000'],
        'http_req_duration{scenario:soak_long_term_stability}': ['p(95)<2400', 'p(99)<3500'],
        'http_req_duration{scenario:realistic_sla_compliance}': ['p(95)<2200', 'p(99)<3200']
    }
};

/**
 * Prepares test data by creating a notification order chain with unique identifiers.
 * 
 * @returns {Object} Test context containing the order chain payload that will be
 *                   used by the default function for each virtual user iteration
 */
export function setup() {
    // Create unique identifier for this test run
    const uniqueIdentifier = uuidv4().substring(0, 8);

    // Deep clone the base request template
    const orderChainPayload = JSON.parse(JSON.stringify(orderChainJsonPayload));

    // Configure core properties
    orderChainPayload.requestedSendTime = getFutureDate(7);
    orderChainPayload.sendersReference = `k6-order-${uniqueIdentifier}`;

    // Set phone number for main recipient
    if (orderChainPayload.recipient?.recipientSms) {
        orderChainPayload.recipient.recipientSms.phoneNumber = getSmsRecipient();
    }

    // Configure all reminders with consistent references and phone numbers
    if (Array.isArray(orderChainPayload.reminders)) {
        orderChainPayload.reminders = orderChainPayload.reminders.map(reminder => {
            // Update reminder with reference and phone number
            const updatedReminder = {
                ...reminder,
                sendersReference: `k6-reminder-${uniqueIdentifier}`
            };

            // Set phone number for reminder recipient
            if (updatedReminder.recipient?.recipientSms) {
                updatedReminder.recipient.recipientSms.phoneNumber = getSmsRecipient();
            }

            return updatedReminder;
        });
    }

    return { orderChainPayload };
}

/**
 * Main test function executed for each virtual user iteration.
 * 
 * This is the core test execution function that runs
 * once per VU iteration. It tests multiple API scenarios in sequence: 
 * 1. Generates various order requests based on configured order types (valid, invalid, duplicate)
 * 2. Processes each order by sending API requests and capturing responses
 * 3. For duplicate testing, makes an initial valid request followed by an identical request to verify idempotency behavior
 * 4. Collects all responses and validates them against expected outcomes
 * 
 * @param {Object} data - Test context from setup phase containing the prepared orderChainPayload template
 */
export default function (data) {
    const orderChainPayloads = generateOrderChainPayloadsByOrderType(data);

    const processingResults = orderChainPayloads.map(e => {
        const { orderType, orderChainPayload } = e;

        switch (orderType) {
            case "valid":
                return processOrderChainPayload(orderType, orderChainPayload, post_valid_order, validOrderDuration);

            case "invalid":
                return processOrderChainPayload(orderType, orderChainPayload, post_invalid_order, invalidOrderDuration);

            case "duplicate": {
                processOrderChainPayload("valid", orderChainPayload, post_valid_order, validOrderDuration);
                return processOrderChainPayload(orderType, orderChainPayload, post_duplicate_order, duplicateOrderDuration);
            }

            default:
                return undefined;
        }
    }).filter(Boolean); // Remove any undefined results

    validateProcessingResults(processingResults);
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
 * Generates a date string for a future date relative to the current date.
 *
 * @param {number} [daysToAdd=0] - The number of days to add to the current date
 * @returns {string} The formatted future date as an ISO UTC string
 */
function getFutureDate(daysToAdd = 0) {
    if (typeof daysToAdd !== 'number' || isNaN(daysToAdd)) {
        daysToAdd = 0;
    }

    const futureDate = new Date(Date.now() + daysToAdd * 24 * 60 * 60 * 1000);
    return futureDate.toISOString();
}

/**
 * Creates a unique order chain payload with consistent identifiers for API testing.
 * 
 * This function:
 * 1. Generates a UUID-based idempotency identifier that enables testing identical 
 *    request handling (proper 200 OK responses for duplicates)
 * @param {Object} data - The shared data object containing the base order chain payload template
 * @returns {Object} A cloned order chain payload with:
 *                   - Unique idempotency identifier
 */
function createUniqueOrderChainPayload(data) {
    return {
        ...data.orderChainPayload,
        idempotencyId: uuidv4()
    };
}

/**
 * Categorizes HTTP responses by status code and updates performance metrics for test reporting.
 * 
 * This function is critical for tracking key metrics that drive test thresholds:
 * - Status 200-399: Increments success rates and metrics
 * - Status 400-499: Records client errors (4xx), updates failure counters
 * - Status 500-599: Records server errors (5xx), updates failure counters
 * 
 * The metrics updated include:
 * - successRate: Proportion of non-error responses (affects 'success_rate' threshold)
 * - serverErrorRate: Proportion of server (5xx) errors (affects 'server_error_rate' threshold)
 * - http4xx/http5xx: Count of client/server errors (affects 'http_5xx' threshold)
 * - failedRequests: Combined count of all error responses
 *
 * @param {Object} httpResponse - The HTTP response object from k6
 * @param {number} httpResponse.status - The HTTP status code (e.g., 200, 400, 500)
 */
function collectHttpResponseMetrics(httpResponse) {
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
 * Generates different types of order chain payloads for API testing scenarios.
 * 
 * This function creates specialized test payloads based on the configured order types in the 
 * global `orderTypes` array. Each order type results in a different modification: 
 * - "valid": Standard well-formed order chain with all required fields
 * - "invalid": Order chain with required recipient sms fields removed
 * - "duplicate": Creates an order chain that will later be sent twice to test idempotency
 * 
 * @param {Object} data - The shared data containing the base order chain payload from setup
 * @returns {Array<Object>} Array of objects with format { orderType: string, orderChainPayload: Object }
 *                          where each orderChainPayload is crafted for its specific test scenario
 */
function generateOrderChainPayloadsByOrderType(data) {
    const orderChainPayloads = [];

    for (const orderType of orderTypes) {
        let uniqueOrderChainPayload = createUniqueOrderChainPayload(data);

        switch (orderType) {
            case "valid":
            case "duplicate":
                orderChainPayloads.push({ orderType, orderChainPayload: uniqueOrderChainPayload });
                break;

            case "invalid":
                orderChainPayloads.push({ orderType, orderChainPayload: stripRecipientSmsFromOrderChainPayload(uniqueOrderChainPayload) });
                break;
        }
    }

    return orderChainPayloads;
}

/**
 * Validates and records metrics for API responses by order type.
 *
 * Consumes the processing results produced during execution and applies type-specific
 * assertions, while updating domain metrics (e.g., latency, idempotency, status counters).
 *
 * Validation rules by order type:
 * - valid: Expects 201 Created, verifies notificationOrderId, shipmentId, reminders array,
 *          reminder count parity with request, and shipmentId presence on each reminder.
 * - invalid: Expects 400 Bad Request.
 * - duplicate: Expects 200 Ok, verifies notificationOrderId, shipmentId, reminders array,
 *              reminder count parity with request, and shipmentId presence on each reminder.
 * - missingResource: Expects 201 Created.
 *
 * @returns {void}
 */
function validateProcessingResults(processingResults) {
    if (!processingResults || processingResults.length === 0) {
        return;
    }

    const validators = {
        valid: (response, responseBody, orderChainPayload) => {

            const notificationObj = responseBody.notification || {};
            const reminderArray = Array.isArray(notificationObj.reminders) ? notificationObj.reminders : [];
            const expectedReminderCount = Array.isArray(orderChainPayload?.reminders) ? orderChainPayload.reminders.length : 0;

            // Track success rate specifically for valid orders
            orderKindRateValid.add(response.status === 201);

            // Track high latency for valid orders
            highLatencyRate.add(response.timings.duration > 2000);

            check(response, {
                "Status is 201 Created": e => e.status === 201,
                "Response contains shipment ID": () => typeof notificationObj.shipmentId === 'string' && notificationObj.shipmentId.length > 0,
                "Response contains notification order ID": () => typeof responseBody.notificationOrderId === 'string' && responseBody.notificationOrderId.length > 0,
                "Response includes reminders": () => Array.isArray(notificationObj.reminders),
                "Reminder count matches request": () => reminderArray.length === expectedReminderCount,
                "All reminders have shipment IDs": () => reminderArray.length === 0 || reminderArray.every(e => typeof e.shipmentId === 'string' && e.shipmentId.length > 0)
            });

            if (response.status === 201) {
                http201Created.add(1);
            }
        },

        invalid: (response) => {
            // Track high latency for invalid orders
            highLatencyRate.add(response.timings.duration > 2000);

            check(response, { "Status is 400 Bad Request": e => e.status === 400 });

            if (response.status === 400) {
                http400Validation.add(1);
            }
        },

        duplicate: (response, responseBody, orderChainPayload) => {
            const notificationObj = responseBody.notification || {};
            const reminderArray = Array.isArray(notificationObj.reminders) ? notificationObj.reminders : [];
            const expectedReminderCount = Array.isArray(orderChainPayload?.reminders) ? orderChainPayload.reminders.length : 0;

            // Track high latency for duplicate orders
            highLatencyRate.add(response.timings.duration > 2000);

            check(response, {
                "Status is 200 OK": e => e.status === 200,
                "Response contains shipment ID": () => typeof notificationObj.shipmentId === 'string' && notificationObj.shipmentId.length > 0,
                "Response contains notification order ID": () => typeof responseBody.notificationOrderId === 'string' && responseBody.notificationOrderId.length > 0,
                "Response includes reminders": () => Array.isArray(notificationObj.reminders),
                "Reminder count matches request": () => reminderArray.length === expectedReminderCount,
                "All reminders have shipment IDs": () => reminderArray.length === 0 || reminderArray.every(e => typeof e.shipmentId === 'string' && e.shipmentId.length > 0)
            });

            if (response.status === 200) {
                http200Duplicate.add(1);
            }
        },

        missingResource: (response) => {
            // Track high latency for missing resource orders
            highLatencyRate.add(response.timings.duration > 2000);

            check(response, { "Status is 201 Created": e => e.status === 201 });

            if (response.status === 201) {
                http201Created.add(1);
            }
        }
    };

    for (const { orderType, httpResponse, orderChainPayload } of processingResults) {
        if (!orderType || !httpResponse || !orderChainPayload) {
            continue;
        }

        if (httpResponse.status === 401 || httpResponse.status === 403) {
            stopIterationOnFail("Critical authentication/authorization error encountered", false);
            break;
        }

        let responseBody;
        try {
            responseBody = JSON.parse(httpResponse.body);
        } catch { responseBody = {}; }

        validators[orderType]?.(httpResponse, responseBody, orderChainPayload);
    }
}

/**
 * Creates an intentionally invalid order chain payload.
 *
 * The function preserves the original structure while:
 * - Setting recipientSms to undefined in the main recipient
 * - Setting recipientSms to undefined in all reminder objects
 *
 * @param {Object} orderChainPayload - The original valid order chain payload
 * @returns {Object} An invalid order chain payload that should be rejected by the API
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

    const token = setupToken.getAltinnTokenForOrg(scopes);

    return ordersApi.postNotificationOrderV2(requestBody, token, label);
}

/**
 * Processes a notification order chain by sending it to the API and collecting performance metrics.
 * 
 * This function is a key part of the k6 testing pipeline that:
 * 1. Performs input validation and skips processing for null/undefined payloads
 * 2. Sends the notification order chain to the API with appropriate authentication
 * 3. Records performance metrics based on the response status code:
 *    - Success rates for 2xx responses
 *    - Error counts for 4xx and 5xx responses
 *    - Response duration metrics for performance analysis
 * 4. Returns a composite object that will be used by validateResponses() for assertions
 * 
 * Each request is tagged with a label to enable filtering metrics by order type
 * in the test results dashboard.
 *
 * @param {string} orderType - The test scenario identifier ("valid", "invalid", "duplicate", "missingResource")
 * @param {Object} orderChainPayload - The notification order chain payload to send
 * @param {string} label - Metric label for tracking this specific request type in results
 * @param {Trend} durationMetric - k6 Trend object for recording response time statistics
 * @returns {Object|undefined} Object containing { orderType, httpResponse, orderChainPayload } for validation, or undefined if payload validation failed
 */
function processOrderChainPayload(orderType, orderChainPayload, label, durationMetric) {
    if (!orderType || !orderChainPayload) {
        return undefined;
    }

    const httpResponse = sendNotificationOrderChain(orderChainPayload, label);

    collectHttpResponseMetrics(httpResponse);

    durationMetric.add(httpResponse.timings.duration);

    return { orderType, httpResponse, orderChainPayload };
}