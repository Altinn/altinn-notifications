/*
    Performance & correctness test for creating notification order chains (main order + reminders) for organizations.

    Core per-iteration functional scenarios (executed when included in orderTypes):
    - 201 Created (valid order) -> must return shipmentId.
    - 200 OK (idempotent duplicate) -> reuse same idempotencyId; must return existing shipmentId.
    - 400 Bad Request (invalid structure: required recipientOrganization removed).
    - 201 Created or 422 (order missing resourceId) -> exercises path with reduced external lookups.

    Order variants supported (configured via imported `orderTypes` array):
    - "valid" | "invalid" | "duplicate" | "missingResource" | "all".
    Each variant is produced from an independently cloned base request to avoid cross-mutation.
    Duplicate uses an exact clone of the valid request (same idempotencyId) to assert idempotency.

    Dynamic data & identifiers:
    - A single random 8-char token seeds: dialogId, transmissionId, main sendersReference, reminder sendersReference.
    - Per order variant we assign a fresh idempotencyId (except duplicate which reuses the valid one).
    - Organization number: __ENV.orgNoRecipient (if present) else generated (helper getOrgNoRecipient()).
    - resourceId injected from shared variables for both main order and reminders unless intentionally removed.

    Scenario suite (k6 `scenarios`):
    1. smoke_test (constant-arrival-rate) – fast readiness gate.
    2. capacity_probe (ramping-arrival-rate) – multi-stage throughput escalation (discover knee / saturation).
    3. load_test_valid_orders (ramping-arrival-rate) – SLO/SLA validation tiers (low/mid/high).
    4. steady_state_load_test (constant-arrival-rate) – forecast production baseline (warm system before spike).
    5. spike_test (ramping-arrival-rate) – rapid burst & decay resilience check.
    6. soak_test (constant-arrival-rate) – extended stability, error accumulation & leaks.

    Environment variable tunables:
    - precheckTestRequestsPerSecond
    - CAP_START
    - requestsPerSecond, requestsPerSecondLowestStage, requestsPerSecondMiddleStage, requestsPerSecondHighestStage
    - FORECAST_RATE, FORECAST_DURATION
    - SOAK_RATE, SOAK_DURATION
    - orgNoRecipient
    (All parsed with Number(...) or used as string durations; safe defaults applied when unset.)

    Metrics (custom):
    - success_rate (Rate) -> non-4xx/5xx proportion (also refined per-scenario via tag scoping).
    - http_4xx (Counter)
    - http_5xx (Counter)
    - failed_requests (Counter) -> any 4xx or 5xx.
    - valid_order_duration (Trend)
    - invalid_order_duration (Trend)
    - duplicate_order_duration (Trend)
    - missing_resource_order_duration (Trend)

    Labels (for per-request grouping / empty thresholds injected):
    - post_valid_order
    - post_invalid_order
    - post_duplicate_order
    - post_order_without_resource_id
    (setEmptyThresholds ensures they appear without enforcing extra performance gates.)

    Thresholds (selected excerpts):
    - http_5xx: count < 50 (global)
    - failed_requests: count < 100 (global)
    - success_rate: rate > 0.99 (global), stricter for load_test_valid_orders ( > 0.999 )
    - checks: rate > 0.995
    - Scenario-specific http_req_duration p95 / p99 bounds (capacity, load, steady-state, soak, spike).
    - valid_order_duration: p95 < 1500, p99 < 2500

    Validation (k6 checks per request type):
    - Valid: 201 + shipmentId.
    - Duplicate: 200 + shipmentId (idempotency confirmation).
    - Invalid: 400 (structure validation).
    - Missing resource: 201 or 422 (accepts alternate backend behavior).

    Error handling:
    - Any 4xx/5xx increments counters; classification split between http_4xx/http_5xx.
    - 401 / 403 triggers stopIterationOnFail (fail-fast for authZ/authN issues).

    Test data source:
    - Base JSON: ../data/orders/order-with-reminders-for-organizations.json (deep cloned per variant).

    Summary output:
    - summary.json (raw k6 result object)
    - Colored human-readable stdout (textSummary).

    Goals:
    1. Gate readiness quickly before heavy stages (smoke_test).
    2. Empirically discover capacity limits & latency inflection (capacity_probe).
    3. Validate SLO/SLA conformance across realistic demand tiers (load & steady-state).
    4. Assess burst resilience (spike_test) and long-haul stability / leak absence (soak_test).
    5. Verify correctness paths: creation, idempotency, validation failures, reduced-external-call variant.
    6. Produce actionable metrics for regression tracking and release fitness decisions.

    Notes:
    - All cloning uses JSON deep copies to avoid shared mutations.
    - Idempotency enforced solely by reused idempotencyId in duplicate order path.
    - Missing resource variant deliberately strips resourceId fields in main + reminder recipients.
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

// Rate to track the proportion of successful requests (non-4xx/5xx responses) to total requests
const successRate = new Rate("success_rate");

// Counter to track the number of HTTP responses with 4xx status codes (client errors)
const http4xx = new Counter("http_4xx");

// Counter to track the number of HTTP responses with 5xx status codes (server errors)
const http5xx = new Counter("http_5xx");

// Counter to track the the total number of failed requests (any request with a 4xx or 5xx status code)
const failedRequests = new Counter("failed_requests");

// Trend to track the response time (duration) for valid orders (expected to return a 201 Created status)
const validOrderDuration = new Trend("valid_order_duration");

// Trend to track the response time (duration) for invalid orders (expected to return a 400 Bad Request status)
const invalidOrderDuration = new Trend("invalid_order_duration");

// Trend to track the response time (duration) for valid but duplicate orders (expected to return a 200 Ok status)
const duplicateOrderDuration = new Trend("duplicate_order_duration");

// Trend to track the response time (duration) for orders missing a resource (expected to return a 201 Created or 200 Ok status)
const missingResourceOrderDuration = new Trend("missing_resource_order_duration");

// Test data for order chain requests, loaded from a JSON file.
const orderChainRequestJson = JSON.parse(open("../data/orders/order-with-reminders-for-organizations.json"));

// Define the order types that will be used in the test scenarios
export const options = {
    scenarios: {
    // 1. Quick readiness gate (runs first, alone)
        smoke_test: {
            maxVUs: 20,
            timeUnit: '1s',
            duration: '30s',
            preAllocatedVUs: 10,
            gracefulStop: '30s',
            executor: 'constant-arrival-rate',
            rate: Number(__ENV.precheckTestRequestsPerSecond || 5)
        },

        // 2. Capacity probe (runs alone – all other heavy scenarios start after it finishes)
        // Total duration of stages = 27m. Starts after smoke_test (startTime 45s).
        capacity_probe: {
            maxVUs: 500,
            timeUnit: '1s',
            startTime: '45s',
            gracefulStop: '1m',
            preAllocatedVUs: 10,
            executor: 'ramping-arrival-rate',
            startRate: Number(__ENV.CAP_START || 50),
            stages: [
                { target: 100, duration: '2m' },
                { target: 200, duration: '3m' },
                { target: 300, duration: '4m' },
                { target: 400, duration: '5m' },
                { target: 500, duration: '6m' },
                { target: 500, duration: '5m' },
                { target: 0, duration: '2m' }
            ]
        },

        // 3. Load test for SLO/SLA validation (starts after capacity probe -> 27m + 45s around 28m)
        load_test_valid_orders: {
            maxVUs: 60,
            timeUnit: '1s',
            startTime: '28m',
            preAllocatedVUs: 20,
            gracefulStop: '30s',
            executor: 'ramping-arrival-rate',
            startRate: Number(__ENV.requestsPerSecond || 10),
            stages: [
                { target: Number(__ENV.requestsPerSecondLowestStage || 100), duration: '4m' },
                { target: Number(__ENV.requestsPerSecondMiddleStage || 150), duration: '5m' },
                { target: Number(__ENV.requestsPerSecondHighestStage || 200), duration: '6m' },
                { target: 0, duration: '1m' }
            ]
        },

        // 4. Steady-state
        // Starts after load test completion (around 28m + 16m = 44m -> add 1m buffer)
        steady_state_load_test: {
            maxVUs: 500,
            timeUnit: '1s',
            startTime: '45m',
            gracefulStop: '1m',
            preAllocatedVUs: 250,
            executor: 'constant-arrival-rate',
            rate: Number(__ENV.FORECAST_RATE || 600),
            duration: __ENV.FORECAST_DURATION || '10m'
        },

        // 5. Spike test – follows warm steady-state baseline
        spike_test: {
            maxVUs: 500,
            startRate: 0,
            startTime: '56m',
            timeUnit: '1s',
            gracefulStop: '15s',
            preAllocatedVUs: 150,
            executor: 'ramping-arrival-rate',
            stages: [
                { target: 50, duration: '15s' },
                { target: 400, duration: '15s' },
                { target: 10, duration: '30s' },
                { target: 0, duration: '30s' }
            ]
        },

        // 6. Soak test – long-running stability (starts after spike)
        soak_test: {
            maxVUs: 200,
            timeUnit: '1s',
            startTime: '58m',
            gracefulStop: '2m',
            preAllocatedVUs: 120,
            executor: 'constant-arrival-rate',
            rate: Number(__ENV.SOAK_RATE || 100),
            duration: __ENV.SOAK_DURATION || '30m'
        }
    },

    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)', 'count'],

    thresholds: {
        'http_5xx': ['count<50'],
        'success_rate': ['rate>0.99'],
        'failed_requests': ['count<100'],

        'success_rate{scenario:load_test_valid_orders}': ['rate>0.999'],
        'http_req_duration{scenario:load_test_valid_orders}': [
            'p(95)<1200',
            'p(99)<1800'
        ],
        'http_req_duration{scenario:steady_state_load_test}': [
            'p(95)<1500',
            'p(99)<2200'
        ],
        'http_req_duration{scenario:soak_test}': [
            'p(95)<1600',
            'p(99)<2300'
        ],

        'valid_order_duration': ['p(95)<1500', 'p(99)<2500'],

        'http_req_duration{scenario:spike_test}': ['p(99)<5000'],

        'http_req_duration{scenario:capacity_probe}': ['p(95)<3000'],

        checks: ['rate>0.995']
    }
};

/**
 * Defines threshold labels for specific test scenarios and applies empty thresholds to them.
 *
 * @constant {string[]} labels - An array of labels representing different test scenarios:
 * - `post_valid_order`: Label for testing valid orders.
 * - `post_invalid_order`: Label for testing invalid orders.
 * - `post_duplicate_order`: Label for testing duplicate orders.
 * - `post_order_without_resource_id`: Label for testing valid orders without a resource identifier.
 *
 * @function setEmptyThresholds
 * @param {string[]} labels - The array of labels for which thresholds are being set.
 * @param {object} options - The options object containing the thresholds configuration.
 * 
 * This code ensures that each label in the `labels` array has an empty threshold defined in the `options` object.
 * Empty thresholds are useful for scenarios where no specific performance criteria are required but the label must still be tracked.
 */
const labels = [post_valid_order, post_invalid_order, post_duplicate_order, post_order_without_resource_id];
setEmptyThresholds(labels, options);

/**
 * Prepares shared data and configurations for the test.
 *
 * This function runs once before the test execution begins and is used to generate
 * an authentication token and prepare the order chain request with unique identifiers.
 *
 * @returns {Object} An object containing:
 * - `token` {string}: The authentication token used for API requests.
 * - `orderChainRequest` {Object}: The prepared order chain request, including:
 *   - `dialogportenAssociation`: An object with unique `dialogId` and `transmissionId`.
 *   - `sendersReference`: A unique reference for the main order.
 *   - `recipient.recipientOrganization.resourceId`: The resource identifier for the recipient organization.
 *   - `reminders`: An array of reminders, each with unique references and resource identifiers.
 *
 * The returned data is shared across all virtual users (VUs) during the test.
 */
export function setup() {
    const token = setupToken.getAltinnTokenForOrg(scopes);

    const randomIdentifier = uuidv4().substring(0, 8);
    const mainOrderSendersReference = `k6-order-${randomIdentifier}`;
    const reminderOrderSendersReference = `k6-reminder-order-${randomIdentifier}`;

    const orderChainRequest = JSON.parse(JSON.stringify(orderChainRequestJson));
    orderChainRequest.dialogportenAssociation = {
        dialogId: randomIdentifier,
        transmissionId: randomIdentifier
    };

    orderChainRequest.sendersReference = mainOrderSendersReference;
    orderChainRequest.recipient.recipientOrganization.resourceId = resourceId;

    orderChainRequest.reminders = orderChainRequest.reminders.map((reminder) => {
        reminder.sendersReference = reminderOrderSendersReference;
        reminder.recipient.recipientOrganization.resourceId = resourceId;
        return reminder;
    });

    return {
        token,
        orderChainRequest
    };
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
 * Executes the main test logic for sending and validating different types of order chain requests.
 *
 * @param {Object} data - The shared data prepared in the `setup` function, including:
 * - `token` {string}: The authentication token for API requests.
 * - `orderChainRequest` {Object}: The base order chain request structure.
 *
 * The function performs the following steps:
 * 1. Retrieves the recipients based on the provided or a random organization number.
 * 
 * 
 * 
 * 2. Generates order chain requests for different scenarios (valid, invalid, duplicate, missing resource).
 * 3. Sends each order chain request and records the response time and result.
 * 4. Validates the responses based on the expected behavior for each scenario:
 *    - Valid Order: Expects `201 Created` with a `shipmentId`.
 *    - Invalid Order: Expects `400 Bad Request` with validation errors.
 *    - Duplicate Order: Expects `200 OK` with an existing `shipmentId`.
 *    - Order Without Resource Identifier: Expects `201 Created` with a `shipmentId`.
 * 5. Stops the test iteration if a critical failure (e.g., `401 Unauthorized` or `403 Forbidden`) is encountered.
 *
 * @throws {Error} If a critical authentication or authorization error is detected.
 */
export default function (data) {
    const orgNumber = getOrgNoRecipient();
    const orderChainRequests = generateOrderChainRequestByOrderType(orderTypes, data, orgNumber);

    const responses = [];

    if (orderChainRequests.validOrder) {
        const response = postNotificationOrderChain(data, orderChainRequests.validOrder, post_valid_order);

        recordResult(response);
        validOrderDuration.add(response.timings.duration);
        responses.push({ name: "valid-order", response: response });
    }

    if (orderChainRequests.invalidOrder) {
        const response = postNotificationOrderChain(data, orderChainRequests.invalidOrder, post_invalid_order);

        recordResult(response);
        invalidOrderDuration.add(response.timings.duration);
        responses.push({ name: "invalid-order", response: response });
    }

    if (orderChainRequests.duplicateOrder) {
        const response = postNotificationOrderChain(data, orderChainRequests.duplicateOrder, post_duplicate_order);

        recordResult(response);
        duplicateOrderDuration.add(response.timings.duration);
        responses.push({ name: "duplicate-order", response: response });
    }

    if (orderChainRequests.missingResourceOrder) {
        const response = postNotificationOrderChain(data, orderChainRequests.missingResourceOrder, post_order_without_resource_id);

        recordResult(response);
        missingResourceOrderDuration.add(response.timings.duration);
        responses.push({ name: "missing-resource-id", response: response });
    }

    for (const entry of responses) {
        if (entry.response.status === 401 || entry.response.status === 403) {
            stopIterationOnFail("Critical authentication/authorization error encountered", false);
            break;
        }

        switch (entry.name) {
            case "valid-order":
                check(entry.response, {
                    "Valid order returns 201 Created": (r) => r.status === 201,
                    "Valid order has shipmentId": () => entry.response.body?.notification?.shipmentId !== undefined
                });
                break;

            case "invalid-order":
                check(entry.response, {
                    "400 validation (invalid order)": (r) => r.status === 400
                });
                break;

            case "duplicate-order":
                check(entry.response, {
                    "Duplicate order returns 200 OK": (r) => r.status === 200,
                    "Duplicate order has existing shipmentId": () => entry.response.body?.notification?.shipmentId !== undefined
                });
                break;

            case "missing-resource-id":
                check(entry.response, {
                    "Missing resource returns 201 or 422": (r) => r.status === 201 || r.status === 422
                });
                break;
        }
    }
}

/**
 * Records the result of an HTTP response by categorizing
 * it as successful or failed and updating relevant performance metrics.
 *
 * @param {Object} httpResponse - The HTTP response object to be evaluated. It includes:
 * - `status` {number}: The HTTP status code of the response.
 */
function recordResult(httpResponse) {
    const isFailedRequest = httpResponse.status >= 400;

    if (isFailedRequest) {
        failedRequests.add(1);
        httpResponse.status >= 500 ? http5xx.add(1) : http4xx.add(1);
    }

    successRate.add(!isFailedRequest);
}

/**
 * Removes the `resourceId` property from the recipient's organization in both the main order
 * and its reminders. This is used to simulate scenarios where the order lacks a required
 * resource identifier, such as when bypassing Profile and Authorization API calls.
 *
 * @param {Object} baseOrder - The base order object to be modified. It includes:
 * - `recipient` {Object}: The recipient details of the main order, including `recipientOrganization`.
 * - `reminders` {Array}: An array of reminder objects, each containing recipient details.
 *
 * @returns {Object} The modified order object with the following changes:
 * - The `resourceId` property is removed from the `recipientOrganization` object in the main order.
 * - The `resourceId` property is removed from the `recipientOrganization` object in each reminder.
 *
 * This function is used to create orders without resource identifiers for testing purposes.
 */
function removeResourceIdFromOrder(baseOrder) {
    delete baseOrder.recipient.recipientOrganization.resourceId;

    baseOrder.reminders.forEach(reminder => {
        delete reminder.recipient.recipientOrganization.resourceId;
    });
    return baseOrder;
}

/**
 * Removes the `recipientOrganization` property 
 * from the recipient in both the main order and its reminders. 
 *
 * @param {Object} baseOrder - The base order object to be modified. It includes:
 * - `recipient` {Object}: The recipient details of the main order.
 * - `reminders` {Array}: An array of reminder objects, each containing recipient details.
 *
 * @returns {Object} The modified order object with the following changes:
 * - The `recipientOrganization` property is removed from the `recipient` object of the main order.
 * - The `recipientOrganization` property is removed from the `recipient` object of each reminder.
 *
 * This function is used to create invalid orders for testing purposes, ensuring that the system
 * correctly handles invalid input by returning appropriate error responses.
 */
function removeRequiredFieldsFromOrder(baseOrder) {
    delete baseOrder.recipient.recipientOrganization;

    baseOrder.reminders.forEach(reminder => {
        delete reminder.recipient.recipientOrganization;
    });

    return baseOrder;
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
    const clonedOrderChainRequest = JSON.parse(JSON.stringify(data.orderChainRequest));

    clonedOrderChainRequest.idempotencyId = uuidv4();
    clonedOrderChainRequest.recipient.recipientOrganization.orgNumber = orgNumber;

    clonedOrderChainRequest.reminders.forEach(reminder => {
        reminder.recipient.recipientOrganization.orgNumber = orgNumber;
    });

    return clonedOrderChainRequest;
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
                console.error(`Unknown orderType: ${orderType}`);
                stopIterationOnFail(`Invalid orderType: ${orderType}`);
        }
    }
    return orderChainRequests;
}

/**
 * Sends a notification order chain request to the Notification API.
 *
 * @param {Object} data - Shared data containing the authentication token.
 * @param {Object} orderRequest - The order chain request object to be sent.
 * @param {string} [label=post_valid_order] - A label used for tracking the request in metrics.
 *
 * @returns {Object} The HTTP response object returned by the API.
 */
function postNotificationOrderChain(data, orderRequest, label = post_valid_order) {
    const requestBody = JSON.stringify(orderRequest);
    return ordersApi.postNotificationOrderV2(requestBody, data.token, label);
}