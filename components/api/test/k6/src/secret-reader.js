import secrets from "k6/secrets";
import { throwConfigurationError } from "./errorhandler.js";
import { environment, localEnvironment } from "./shared/variables.js";

// Nothing checks the subscription key on the local Aspire stack. The mock token
// issuer's credentials are generated for the checkout: the app host writes them
// to .secrets.aspire in the k6 folder, for --secret-source=file=.secrets.aspire.
const localSecrets = {
    subscriptionKey: "local",
};

/*
 * Get a secret from a secret-source. Throws a configuration error if the secret cannot be accessed
 *
 * @param {string} secretName - The name of the secret to get.
 * @returns {Promise<string>} The secret value.
 */
export async function getFromSecretSource(secretName) {
    if (environment === localEnvironment) {
        return await getLocalSecret(secretName);
    }

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

/*
 * Get a secret for the local environment from the secret source if one is
 * configured, or else the local default.
 *
 * @param {string} secretName - The name of the secret to get.
 * @returns {Promise<string>} The secret value.
 */
async function getLocalSecret(secretName) {
    try {
        const secretValue = await secrets.get(secretName);
        if (secretValue) {
            return secretValue;
        }
    } catch {
        // No secret source, or the secret is not in it.
    }

    const localValue = localSecrets[secretName];
    if (!localValue) {
        throwConfigurationError(
            `Secret ${secretName} is not in a secret source - start the Aspire app host, which writes .secrets.aspire in the k6 folder, and run k6 with --secret-source=file=.secrets.aspire`
        );
    }
    return localValue;
}
