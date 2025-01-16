import * as maskinporten from "./api/maskinporten.js";
import * as authentication from "./api/authentication.js";
import * as tokenGenerator from "./api/token-generator.js";

const environment = __ENV.env.toLowerCase();

/*
 * Generate an Altinn token for an organization based on the environment using AltinnTestTools.
 * If organization is not provided, TTD will be used.
 * @returns Altinn token with the provided scopes for an organization.
 */
export function getAltinnTokenForOrg(scopes, org = "ttd", orgNo = "991825827") {
  if ((environment === "prod" || environment === "tt02") && org === "ttd") {
    const accessToken = maskinporten.generateAccessToken(scopes);

    return authentication.exchangeToAltinnToken(accessToken, true);
  }

  const queryParams = {
    env: environment,
    scopes: scopes.replace(/ /gi, ","),
    org: org,
    orgNo: orgNo,
  };

  return tokenGenerator.generateEnterpriseToken(queryParams);
}