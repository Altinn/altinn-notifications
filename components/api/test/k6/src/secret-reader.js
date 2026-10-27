import secrets from "k6/secrets";
import { throwConfigurationError } from "./errorhandler.js";

/*
 * Get a secret from a secret-source. Throws a configuration error if the secret cannot be accessed
 *
 * @param {string} secretName - The name of the secret to get.
 * @returns {Promise<string>} The secret value.
 */
export async function getFromSecretSource(secretName) {
    let secretValue;
    try {
        secretValue = await secrets.get(secretName);
    } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        if (message.includes("no secret sources are configured")) {
            throwConfigurationError(
                "No secret source is configured for the k6 command - specify the file path with the --secret-source flag"
            );
        } else if (message.includes("no value")) {
            throwConfigurationError(
                `Secret ${secretName} does not exist in the secret source`
            );
        }
        throwConfigurationError(
            `Unknown error occurred while reading secret '${secretName}': ${message}`
        );
    }
    if (!secretValue) {
        throwConfigurationError(
            `Secret ${secretName} is not properly assigned in the secret source`
        );
    }
    return secretValue;
}
