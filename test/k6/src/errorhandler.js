import { Counter } from "k6/metrics";
import { fail } from "k6";

let ErrorCount = new Counter("errors");

/**
 * Increment the custom error count metric if success is false
 * @param {boolean} success The result of a check
 */
export function addErrorCount(success) {
  if (!success) {
    ErrorCount.add(1);
  }
}

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
