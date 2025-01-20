import * as maskinporten from "./api/maskinporten.js";
import { stopIterationOnFail } from "./errorhandler.js";
import * as authentication from "./api/authentication.js";
import * as tokenGenerator from "./api/token-generator.js";

const environment = __ENV.env ? __ENV.env.toLowerCase() : null;

/*
 * Generates an Altinn token for an organization based on the specified environment using AltinnTestTools.
 * If no organization is specified, the default organization (TTD) will be used.
 * @returns An Altinn token with the specified scopes for the organization.
 */
export function getAltinnTokenForOrg(scopes, org = "ttd", orgNo = "991825827") {
    if (!environment) {
        stopIterationOnFail("Environment variable 'env' is not set", false);
    }

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