import http from "k6/http";
import { check } from "k6";
import encoding from "k6/encoding";
import * as config from "../config.js";
import * as apiHelpers from "../apiHelpers.js";
import { stopIterationOnFail } from "../errorhandler.js";

const tokenGeneratorUserName = __ENV.tokenGeneratorUserName;
const tokenGeneratorUserPwd = __ENV.tokenGeneratorUserPwd;

/*
Generate enterprise token for test environment
*/
export function generateEnterpriseToken(queryParams) {
  const endpoint =
    config.tokenGenerator.getEnterpriseToken +
    apiHelpers.buildQueryParametersForEndpoint(queryParams);

  return generateToken(endpoint);
}

export function generatePersonalToken(queryParams) {
  const endpoint =
    config.tokenGenerator.getPersonalToken +
    apiHelpers.buildQueryParametersForEndpoint(queryParams);

  return generateToken(endpoint);
}

function generateToken(endpoint) {
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
