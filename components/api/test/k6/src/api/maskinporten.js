import http from "k6/http";
import { check } from "k6";
import encoding from "k6/encoding";
import * as config from "../config.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { buildHeaderWithContentType } from "../apiHelpers.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { getFromSecretSource } from "../secret-reader.js";

let memoizedSigningKeyPromise;

/**
 * Authenticates with Maskinporten and returns an access token.
 * @param {string} scopes - Space-separated list of scopes to request.
 * @returns {Promise<string>} The Maskinporten access token.
 */
export async function generateAccessToken(scopes) {
    const encodedJwk = await getFromSecretSource("encodedJwk");
    const mpKid = await getFromSecretSource("mpKid");
    const mpClientId = await getFromSecretSource("mpClientId");

    const grant = await createJwtGrant(scopes, encodedJwk, mpKid, mpClientId);

    const body = {
        alg: "RS256",
        grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer",
        assertion: grant,
    };

    const res = http.post(
        config.maskinporten.token,
        body,
        buildHeaderWithContentType("application/x-www-form-urlencoded")
    );

    const success = check(res, {
        "// Setup // Authentication towards Maskinporten Success": (r) =>
            r.status === 200,
    });

    stopIterationOnFail(
        "// Setup // Authentication towards Maskinporten Failed",
        success
    );

    const accessToken = JSON.parse(res.body)["access_token"];

    return accessToken;
}

/**
 * Imports the RSA signing key from a base64-encoded JWK.
 * @param {string} encodedJwk Base64-encoded JWK.
 * @returns {Promise<CryptoKey>} The imported signing key.
 */
async function importSigningKey(encodedJwk) {
    const jwk = JSON.parse(encoding.b64decode(encodedJwk, "std", "s"));
    return crypto.subtle.importKey(
        "jwk",
        jwk,
        { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
        false,
        ["sign"]
    );
}

/**
 * Returns the memoized signing key, intentionally importing the jwk only once for the entire test run (on first call). Caching the promise (rather than the resolved value) allows concurrent calls to share the same in-flight import rather than triggering duplicates.
 * @param {string} encodedJwk Base64-encoded JWK.
 * @returns {Promise<CryptoKey>} The signing key.
 */
function getSigningKey(encodedJwk) {
    if (!memoizedSigningKeyPromise) {
        memoizedSigningKeyPromise = importSigningKey(encodedJwk);
    }
    return memoizedSigningKeyPromise;
}

/**
 * Base64url-encodes a JSON-serializable object without padding.
 * @param {object} obj - The object to encode.
 * @returns {string} The base64url-encoded string.
 */
function base64urlEncode(obj) {
    return encoding.b64encode(JSON.stringify(obj), "rawurl");
}

/**
 * Converts an ASCII string to a Uint8Array.
 * @param {string} str - The input string (ASCII only).
 * @returns {Uint8Array} The byte representation.
 */
function stringToBytes(str) {
    const buf = new Uint8Array(str.length);
    for (let i = 0; i < str.length; i++) {
        buf[i] = str.charCodeAt(i);
    }
    return buf;
}

/**
 * Creates a signed JWT grant for the Maskinporten token request.
 * @param {string} scopes - Space-separated list of scopes to include in the grant.
 * @param {string} encodedJwk - Base64-encoded JWK for signing maskinporten token requests.
 * @param {string} mpKid - The key identifier of the JSON web key used to sign the maskinporten token request
 * @param {string} mpClientId - The client-ID of the integration set up in Maskinporten
 * @returns {Promise<string>} The signed JWT string.
 */
async function createJwtGrant(scopes, encodedJwk, mpKid, mpClientId) {
    const header = {
        alg: "RS256",
        typ: "JWT",
        kid: mpKid,
    };

    const now = Math.floor(Date.now() / 1000);

    const payload = {
        aud: config.maskinporten.audience,
        scope: scopes,
        iss: mpClientId,
        iat: now,
        exp: now + 120,
        jti: uuidv4(),
    };

    const cryptoKey = await getSigningKey(encodedJwk);

    const signingInput =
        base64urlEncode(header) + "." + base64urlEncode(payload);
    const data = stringToBytes(signingInput);
    const signature = await crypto.subtle.sign(
        "RSASSA-PKCS1-v1_5",
        cryptoKey,
        data
    );

    return (
        signingInput +
        "." +
        encoding.b64encode(new Uint8Array(signature), "rawurl")
    );
}
