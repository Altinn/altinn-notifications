import secrets from "k6/secrets";
import encoding from "k6/encoding";
import http from "k6/http";
import * as config from "../config.js";
import * as apiHelpers from "../apiHelpers.js";
import { stopIterationOnFail, throwConfigurationError } from "../errorhandler.js";

// Storage to hold the cached token and its expiration time
let authenticationStorage = {
    expiresAt: 0,
    cachedToken: null
};

/*
 * Generate enterprise token for test environment.
 * 
 * @param {Object} queryParams - The query parameters to be included in the token generation request.
 * @returns {Promise<string>} The generated enterprise token.
 */
export async function generateEnterpriseToken(queryParams) {
    const endpoint =
        config.tokenGenerator.getEnterpriseToken +
        apiHelpers.buildQueryParametersForEndpoint(queryParams);

    return await generateToken(endpoint);
}

/**
 * Generates a token by making an HTTP GET request to the specified Token endpoint.
 * Returns a cached token if one exists and hasn't expired.
 *
 * @param {string} endpoint - The endpoint URL to which the token generation request is sent.
 * @returns {Promise<string>} The generated or cached token.
 */
async function generateToken(endpoint) {
    const currentTime = Math.floor(Date.now() / 1000);
    const skewSeconds = 30;

    // Return cached token if it exists and is not expired
    if (authenticationStorage.cachedToken && (authenticationStorage.expiresAt - skewSeconds) > currentTime) {
        return authenticationStorage.cachedToken;
    }

    const tokenGeneratorUserName = await getFromSecretSource('tokenGeneratorUserName', throwConfigurationError);
    const tokenGeneratorUserPwd = await getFromSecretSource('tokenGeneratorUserPwd', throwConfigurationError);
    const credentials = `${tokenGeneratorUserName}:${tokenGeneratorUserPwd}`;
    const encodedCredentials = encoding.b64encode(credentials);

    const params = apiHelpers.buildHeaderWithBasic(encodedCredentials);

    const response = http.get(endpoint, params);

    if (response.status != 200) {
        stopIterationOnFail("Token generation failed", false);
    }

    authenticationStorage.cachedToken = response.body;
    authenticationStorage.expiresAt = getTokenExpiration(authenticationStorage.cachedToken);

    return authenticationStorage.cachedToken;
}

/**
 * Decodes a JWT token payload and extracts the expiration time (exp).
 * Uses base64url -> text decoding; supports optional "Bearer " prefix.
 * 
 * @param {string} token - The JWT token to decode
 * @returns {number} The expiration timestamp in seconds since epoch
 */
function getTokenExpiration(token) {
    // Remove 'Bearer ' prefix if present
    const tokenValue = token.trim().replace(/^Bearer\s+/i, '');

    const parts = tokenValue.split('.');
    if (parts.length !== 3) {
        throw new Error("Invalid JWT token format");
    }

    const payloadJson = encoding.b64decode(parts[1], 'rawurl', 's');
    const payload = JSON.parse(payloadJson);
    const exp = Number(payload.exp);
    if (!Number.isFinite(exp)) {
        throw new TypeError("Token does not contain numeric expiration (exp) claim");
    }

    return exp;
}

async function getFromSecretSource(secretName, raiseError) {
    let secretValue;
    try {
        secretValue = await secrets.get(secretName);
    }
    catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        if (message.includes("no secret sources are configured")) {
            raiseError("No secret source is configured for the k6 command - specify the file path with the --secret-source flag");
        }
        else if (message.includes("no value")) {
            raiseError(`Secret ${secretName} does not exist in the secret source`);
        }
        raiseError(`Unknown error occurred while reading secret '${secretName}': ${message}`);
    }
    if (!secretValue) {
        raiseError(`Secret ${secretName} is not properly assigned in the secret source`);
    }
    return secretValue;
}
