import { fail } from "k6";

/**
 * Stops k6 iteration when success is false and prints test name with response code
 * @param {String} failReason The reason for stopping the tests
 * @param {boolean} success The result of a check
 */
export function stopIterationOnFail(failReason, success) {
    if (!success) {
        fail(failReason);
    }
}
