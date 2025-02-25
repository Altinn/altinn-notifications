import http from "k6/http";
import encoding from "k6/encoding";
import * as config from "../config.js";
import * as apiHelpers from "../apiHelpers.js";
import { stopIterationOnFail } from "../errorhandler.js";

const tokenGeneratorUserPwd = (__ENV.tokenGeneratorUserPwd ?? __ENV.TOKEN_GENERATOR_PASSWORD);
const tokenGeneratorUserName = (__ENV.tokenGeneratorUserName  ?? __ENV.TOKEN_GENERATOR_USERNAME);

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
 *
 * @param {string} endpoint - The endpoint URL to which the token generation request is sent.
 * @returns {string} The generated token.
 */
function generateToken(endpoint) {
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

    const token = response.body;

    return token;
}
