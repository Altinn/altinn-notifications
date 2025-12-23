/*
    Shared test for registering notification orders (with reminders) against the Notification API.

    Provided utilities cover:
    - Metrics & trends
    - Base k6 options (scenarios + thresholds)
    - Common setup helpers (future date)
    - HTTP request dispatch + metric collection
    - Generic processing pipeline for order chain payloads
    - Response validation runner (script-specific validators supplied externally)
    - handleSummary for custom output

    Script-specific files now only implement:
    - Unique payload cloning / mutation (idempotency / channel specific fields)
    - Invalid / edge-case transformations
    - Order type enumeration & validators referencing the shared metrics
*/

import { check } from "k6";
import * as setupToken from "../setup.js";
import { Trend, Counter, Rate } from "k6/metrics";
import * as ordersApi from "../api/notifications/v2.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { scopes, performanceTestScenario } from "../shared/variables.js";
import { textSummary } from "https://jslib.k6.io/k6-summary/0.0.1/index.js";

// Rate to track the proportion of successful requests (non-4xx/5xx responses) to total requests
export const successRate = new Rate("success_rate");

// Rate to track the proportion of requests that exceed a defined latency threshold (e.g., 2 seconds)
export const highLatencyRate = new Rate("high_latency_rate");

// Rate to track the proportion of requests that result in 5xx server errors
export const serverErrorRate = new Rate("server_error_rate");

// Rate to track the proportion of requests that result in 201 Created responses
export const orderKindRateValid = new Rate("order_valid_success_rate");

// Counter to track the number of HTTP responses with 4xx status codes (client errors)
export const http4xx = new Counter("http_4xx");

// Counter to track the number of HTTP responses with 5xx status codes (server errors)
export const http5xx = new Counter("http_5xx");

// Counter to track the number of failed requests (4xx and 5xx responses)
export const failedRequests = new Counter("failed_requests");

// Counter to track the number of HTTP responses with 201 Created status (valid orders)
export const http201Created = new Counter("http_201_created");

// Counter to track the number of HTTP responses with 200 Ok status (duplicate orders)
export const http200Duplicate = new Counter("http_200_duplicate");

// Counter to track the number of HTTP responses with 400 Bad Request status (validation errors)
export const http400Validation = new Counter("http_400_validation");

// Trend to track the response time (duration) for valid orders (expected to return a 201 Created status)
export const validOrderDuration = new Trend("valid_order_duration");

// Trend to track the response time(duration) for invalid orders(expected to return a 400 Bad Request status)
export const invalidOrderDuration = new Trend("invalid_order_duration");

// Trend to track the response time (duration) for valid but duplicate orders (expected to return a 200 Ok status)
export const duplicateOrderDuration = new Trend("duplicate_order_duration");

// Trend to track the response time (duration) for orders missing a resource (expected to return a 201 Created status)
export const missingResourceOrderDuration = new Trend("missing_resource_order_duration");

/**
 * Builds the shared k6 options object used by all scripts, with possibility to
 * extend thresholds (e.g., organization script adds missing_resource_order_duration).
 * 
 * @param {Object} [extraThresholds={}] - Additional threshold definitions
 * @returns {Object} k6 options
 */
export function buildOptions(extraThresholds = {}) {
    const base = {
        scenarios: performanceTestScenario === 'userDefined' ?
            {
                userDefined: {
                    executor: 'constant-vus',
                    tags: { scenario: 'custom' },
                    duration: __ENV.duration || '30s',
                    vus: Number.parseInt(__ENV.vus || '10', 10)
                }
            }
            :
            {
                smoke: {
                    rate: 10,
                    maxVUs: 100,
                    timeUnit: '1s',
                    duration: '30s',
                    preAllocatedVUs: 5,
                    gracefulStop: '10s',
                    executor: 'constant-arrival-rate'
                },
                capacity_probe: {
                    maxVUs: 1000,
                    startRate: 25,
                    timeUnit: '1s',
                    startTime: '45s',
                    gracefulStop: '30s',
                    preAllocatedVUs: 120,
                    executor: 'ramping-arrival-rate',
                    stages: [
                        { target: 25, duration: '2m' },
                        { target: 50, duration: '2m' },
                        { target: 75, duration: '3m' },
                        { target: 100, duration: '1m' }
                    ]
                },
                realistic_sla_compliance: {
                    maxVUs: 1000,
                    startRate: 50,
                    timeUnit: '1s',
                    startTime: '9m20s',
                    gracefulStop: '30s',
                    preAllocatedVUs: 250,
                    executor: 'ramping-arrival-rate',
                    stages: [
                        { target: 50, duration: '4m' },
                        { target: 100, duration: '6m' },
                        { target: 75, duration: '2m' }
                    ]
                },
                steady_state_load: {
                    rate: 100,
                    maxVUs: 600,
                    timeUnit: '1s',
                    duration: '15m',
                    startTime: '22m',
                    gracefulStop: '30s',
                    preAllocatedVUs: 250,
                    executor: 'constant-arrival-rate'
                },
                sudden_spike_resilience: {
                    maxVUs: 700,
                    timeUnit: '1s',
                    startTime: '37m5s',
                    gracefulStop: '30s',
                    preAllocatedVUs: 350,
                    executor: 'ramping-arrival-rate',
                    stages: [
                        { target: 50, duration: '10s' },
                        { target: 150, duration: '20s' },
                        { target: 300, duration: '2m' },
                        { target: 0, duration: '40s' }
                    ]
                },
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
            'http_req_duration{scenario:realistic_sla_compliance}': ['p(95)<2200', 'p(99)<3200'],
            ...extraThresholds
        }
    };
    return base;
}

/**
 * Generates a date string for a future date relative to the current date.
 *
 * @param {number} [daysToAdd=0] - The number of days to add to the current date
 * @returns {string} The formatted future date as an ISO UTC string
 */
export function getFutureDate(daysToAdd = 0) {
    if (typeof daysToAdd !== 'number' || Number.isNaN(daysToAdd)) {
        daysToAdd = 0;
    }
    const futureDate = new Date(Date.now() + daysToAdd * 24 * 60 * 60 * 1000);
    return futureDate.toISOString();
}

/**
 * Categorizes HTTP responses by status code and updates performance metrics for test reporting.
 * 
 * @param {Object} httpResponse - The HTTP response object from k6
 * @param {number} httpResponse.status - The HTTP status code
 */
export function collectHttpResponseMetrics(httpResponse) {
    if (!httpResponse) {
        failedRequests.add(1);
        successRate.add(false);
        serverErrorRate.add(false);
        return;
    }

    const status = httpResponse.status;
    const isNetworkFail = status === 0;
    const isServerError = status >= 500;
    const isClientError = status >= 400 && status < 500;

    if (isNetworkFail) {
        failedRequests.add(1);
        successRate.add(false);
        serverErrorRate.add(false);
        return;
    }

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
 * Sends a notification order chain request to the Notification API.
 *
 * @param {Object} orderRequest - The order chain payload to send.
 * @param {string} [label='post_valid_order'] - Label used for logging/metrics.
 * @returns {Object} The HTTP response from the Notification API.
 */
export function sendNotificationOrderChain(orderRequest, label = 'post_valid_order') {
    const requestBody = JSON.stringify(orderRequest);
    const token = setupToken.getAltinnTokenForOrg(scopes);
    return ordersApi.postNotificationOrderV2(requestBody, token, label);
}

/**
 * Processes a notification order chain by sending it to the API and collecting performance metrics.
 *
 * @param {string} orderType - "valid", "invalid", "duplicate", etc.
 * @param {Object} orderChainPayload - The notification order chain payload to send
 * @param {string} label - Metric label
 * @param {Trend} durationMetric - Trend metric to record duration
 * @returns {Object|undefined} { orderType, httpResponse, orderChainPayload }
 */
export function processOrderChainPayload(orderType, orderChainPayload, label, durationMetric) {
    if (!orderType || !orderChainPayload) {
        return undefined;
    }
    const httpResponse = sendNotificationOrderChain(orderChainPayload, label);
    collectHttpResponseMetrics(httpResponse);
    if (durationMetric) {
        durationMetric.add(httpResponse.timings.duration);
    }
    return { orderType, httpResponse, orderChainPayload };
}

/**
 * Runs supplied validator functions against collected processing results.
 *
 * @param {Array<Object>} processingResults - Array of { orderType, httpResponse, orderChainPayload }
 * @param {Object<string,function>} validators - Map of orderType -> validator(response, body, payload)
 */
export function runValidators(processingResults, validators) {
    if (!processingResults || processingResults.length === 0) {
        return;
    }

    for (const { orderType, httpResponse, orderChainPayload } of processingResults) {
        if (!orderType || !httpResponse || !orderChainPayload) {
            continue;
        }

        if (httpResponse.status === 401 || httpResponse.status === 403) {
            stopIterationOnFail("Critical authentication/authorization error encountered", false);
            break;
        }

        let parsed;
        try {
            parsed = (typeof httpResponse.body === 'string' && httpResponse.body.length > 0)
                ? JSON.parse(httpResponse.body)
                : {};
        } catch {
            parsed = {};
        }

        validators[orderType]?.(httpResponse, parsed, orderChainPayload);
    }
}

/**
 * Generates a custom summary of the test results after the test execution.
 *
 * @param {Object} testResults - Aggregated k6 results
 * @returns {Object} Summary output mapping
 */
export function handleSummary(testResults) {
    return {
        "summary.json": JSON.stringify(testResults),
        stdout: textSummary(testResults, { indent: "  ", enableColors: true })
    };
}

/**
 * Common structural checks used by multiple recipient types.
 *
 * @param {Object} response - k6 HTTP response
 * @param {Object} responseBody - Parsed JSON body
 * @param {Object} orderChainPayload - Original request payload
 * @param {number} expectedStatus - Status code expected
 * @returns {void}
 */
export function validateStandardNotificationShape(response, responseBody, orderChainPayload, expectedStatus) {
    if (!response || response.status === 0) {
        check(response, {
            [`Status is ${expectedStatus} (skipped due to network failure)`]: () => false
        });
        return;
    }

    const safeBody = (responseBody && typeof responseBody === 'object') ? responseBody : {};
    const notificationObj = (safeBody.notification && typeof safeBody.notification === 'object') ? safeBody.notification : {};
    const reminderArray = Array.isArray(notificationObj.reminders) ? notificationObj.reminders : [];
    const expectedReminderCount = Array.isArray(orderChainPayload?.reminders) ? orderChainPayload.reminders.length : 0;

    check(response, {
        [`Status is ${expectedStatus}`]: e => e?.status === expectedStatus,
        "Response contains shipment ID": () => typeof notificationObj.shipmentId === 'string' && notificationObj.shipmentId.length > 0,
        "Response contains notification order ID": () => typeof safeBody.notificationOrderId === 'string' && safeBody.notificationOrderId.length > 0,
        "Response includes reminders": () => Array.isArray(notificationObj.reminders),
        "Reminder count matches request": () => reminderArray.length === expectedReminderCount,
        "All reminders have shipment IDs": () => reminderArray.every(e => typeof e.shipmentId === 'string' && e.shipmentId.length > 0)
    });
}

/**
 * Prepares a base order chain payload for setup().
 *
 * @param {Object} orderChainJsonPayload - Raw JSON template (already parsed)
 * @param {Object} [options]
 * @param {number} [options.futureDays=7] - Days in future for requestedSendTime
 * @param {boolean} [options.addDialogAssociation=false] - Whether to add dialogportenAssociation
 * @param {string} [options.orderSenderPrefix='k6-order'] - Prefix for main sendersReference
 * @param {string} [options.reminderSenderPrefix='k6-reminder-order'] - Prefix for reminder sendersReference
 * @param {function(Object,string):void} [options.mutate] - Custom mutation callback (payload, uniqueId)
 * @returns {{orderChainPayload:Object, uniqueIdentifier:string}}
 */
export function prepareBaseOrderChain(orderChainJsonPayload, {
    futureDays = 7,
    addDialogAssociation = false,
    orderSenderPrefix = 'k6-order',
    reminderSenderPrefix = 'k6-reminder-order',
    mutate
} = {}) {
    const uniqueIdentifier = uuidv4().substring(0, 8);
    const orderChainPayload = JSON.parse(JSON.stringify(orderChainJsonPayload));

    orderChainPayload.requestedSendTime = getFutureDate(futureDays);
    orderChainPayload.sendersReference = `${orderSenderPrefix}-${uniqueIdentifier}`;

    if (addDialogAssociation) {
        orderChainPayload.dialogportenAssociation = {
            dialogId: uniqueIdentifier,
            transmissionId: uniqueIdentifier
        };
    }

    if (Array.isArray(orderChainPayload.reminders)) {
        orderChainPayload.reminders = orderChainPayload.reminders.map(reminder => ({
            ...reminder,
            sendersReference: `${reminderSenderPrefix}-${uniqueIdentifier}`
        }));
    }

    if (typeof mutate === 'function') {
        mutate(orderChainPayload, uniqueIdentifier);
    }

    return { orderChainPayload, uniqueIdentifier };
}

/**
 * Generates order chain payload variants based on orderTypes.
 *
 * @param {Array<string>} orderTypes - e.g. ['valid','invalid','duplicate']
 * @param {Object} basePayload - Base payload from setup
 * @param {Object} transforms
 * @param {function(Object):Object} transforms.uniqueFactory - Produces a unique payload (adds idempotencyId + channel specific fields)
 * @param {function(Object):Object} [transforms.invalidTransform] - Produces invalid variant
 * @param {function(Object):Object} [transforms.missingResourceTransform] - Produces missingResource variant (org script)
 * @returns {Array<{orderType:string, orderChainPayload:Object}>}
 */
export function generateOrderChainPayloads(orderTypes, basePayload, {
    uniqueFactory,
    invalidTransform,
    missingResourceTransform
}) {
    const variants = [];
    for (const orderType of orderTypes) {
        const unique = uniqueFactory(basePayload);
        switch (orderType) {
            case "valid":
            case "duplicate":
                variants.push({ orderType, orderChainPayload: unique });
                break;
            case "invalid":
                if (invalidTransform) {
                    variants.push({ orderType, orderChainPayload: invalidTransform(unique) });
                }
                break;
            case "missingResource":
                if (missingResourceTransform) {
                    variants.push({ orderType, orderChainPayload: missingResourceTransform(unique) });
                }
                break;
        }
    }
    return variants;
}

/**
 * Processes variants into HTTP requests & metrics.
 *
 * Handles duplicate pattern by issuing an extra "valid" request first.
 *
 * @param {Array<{orderType:string, orderChainPayload:Object}>} variants
 * @param {Object} config
 * @param {Object<string,string>} config.labelMap - orderType -> label constant
 * @param {Object<string,Trend>} config.durationMetrics - orderType -> Trend
 * @returns {Array<Object>} processingResults
 */
export function processVariants(variants, {
    labelMap,
    durationMetrics
}) {
    return variants.map(v => {
        const { orderType, orderChainPayload } = v;
        switch (orderType) {
            case "valid":
                return processOrderChainPayload(orderType, orderChainPayload, labelMap.valid, durationMetrics.valid);
            case "invalid":
                return processOrderChainPayload(orderType, orderChainPayload, labelMap.invalid, durationMetrics.invalid);
            case "duplicate":
                // Send initial valid for idempotency before expecting 200
                processOrderChainPayload("valid", orderChainPayload, labelMap.valid, durationMetrics.valid);
                return processOrderChainPayload(orderType, orderChainPayload, labelMap.duplicate, durationMetrics.duplicate);
            case "missingResource":
                return processOrderChainPayload(orderType, orderChainPayload, labelMap.missingResource, durationMetrics.missingResource);
            default:
                return undefined;
        }
    }).filter(Boolean);
}

/**
 * Builds a standard validator set (with optional missingResource).
 *
 * @param {Object} [options]
 * @param {boolean} [options.includeMissingResource=false]
 * @returns {Object<string,function>}
 */
export function buildStandardValidators({ includeMissingResource = false } = {}) {
    const base = {
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

    if (includeMissingResource) {
        base.missingResource = (response, body, payload) => {
            highLatencyRate.add(response.timings.duration > 2000);
            validateStandardNotificationShape(response, body, payload, 201);
            if (response.status === 201) {
                http201Created.add(1);
            }
        };
    }

    return base;
}
