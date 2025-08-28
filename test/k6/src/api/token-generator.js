import http from "k6/http";
import encoding from "k6/encoding";
import * as config from "../config.js";
import * as apiHelpers from "../apiHelpers.js";
import { stopIterationOnFail } from "../errorhandler.js";

const tokenGeneratorUserPwd = __ENV.tokenGeneratorUserPwd;
const tokenGeneratorUserName = __ENV.tokenGeneratorUserName;

// Storage to hold the cached token and its expiration time
let authenticationStorage = {
    expiresAt: 0,
    cachedToken: null
};

/*
 * Generate enterprise token for test environment.
 * 
 * @param {Object} queryParams - The query parameters to be included in the token generation request.
 * @returns {string} The generated enterprise token.
 */
export function generateEnterpriseToken(queryParams) {
    const endpoint =
        config.tokenGenerator.getEnterpriseToken +
        apiHelpers.buildQueryParametersForEndpoint(queryParams);

    return generateToken(endpoint);
}

/**
 * Generates a token by making an HTTP GET request to the specified Token endpoint.
 * Returns a cached token if one exists and hasn't expired.
 *
 * @param {string} endpoint - The endpoint URL to which the token generation request is sent.
 * @returns {string} The generated or cached token.
 */
function generateToken(endpoint) {
    const currentTime = Math.floor(Date.now() / 1000);

    // Return cached token if it exists and is not expired
    if (authenticationStorage.cachedToken && authenticationStorage.expiresAt > currentTime) {

        return authenticationStorage.cachedToken;
    }

    if (!tokenGeneratorUserName) {
        stopIterationOnFail(`Invalid value for environment variable 'tokenGeneratorUserName': '${tokenGeneratorUserName}'.`, false);
    }

    if (!tokenGeneratorUserPwd) {
        stopIterationOnFail(`Invalid value for environment variable 'tokenGeneratorUserPwd': '${tokenGeneratorUserPwd}'.`, false);
    }

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

    const payloadJson = encoding.b64decode(parts[1], 'url', 's');
    const payload = JSON.parse(payloadJson);
    const exp = Number(payload.exp);
    if (!Number.isFinite(exp)) {
        throw new Error("Token does not contain numeric expiration (exp) claim");
    }

    return exp;
}
