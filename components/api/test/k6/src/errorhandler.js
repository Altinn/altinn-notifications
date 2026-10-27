import { fail } from "k6";

/**
 * Terminates the k6 iteration when the success condition is false and outputs detailed information about the failure.
 * @param {String} failReason The reason for stopping the tests
 * @param {boolean} success The result of a check
 */
export function stopIterationOnFail(failReason, success) {
    if (!success) {
        fail(failReason);
    }
}

/**
 * Interrupts the test run(s) by flagging an invalid k6 configuration that is fundamental.
 * @param {String} message A description of the faulty configuration parameter
 */
export function throwConfigurationError(message) {
    throw new Error(`Invalid k6 configuration: ${message}`);
}
