import * as maskinporten from "./api/maskinporten.js";
import { stopIterationOnFail } from "./errorhandler.js";
import * as authentication from "./api/authentication.js";
import * as tokenGenerator from "./api/token-generator.js";
import { environment } from "./shared/variables.js";

/*
 * Generates an Altinn token for an organization based on the specified environment using AltinnTestTools.
 * If no organization is specified, the default organization (TTD) will be used.
 * @returns An Altinn token with the specified scopes for the organization.
 */
export async function getAltinnTokenForOrg(
    scopes,
    org = "ttd",
    orgNo = "991825827"
) {
    if (!environment) {
        throw new Error("Environment variable 'altinn_env' is not set");
    }

    if ((environment === "prod" || environment === "tt02") && org === "ttd") {
        const accessToken = await maskinporten.generateAccessToken(scopes);

        return authentication.exchangeToAltinnToken(accessToken, true);
    }

    const queryParams = {
        env: environment,
        scopes: scopes.replaceAll(/ /gi, ","),
        org: org,
        orgNo: orgNo,
    };

    return await tokenGenerator.generateEnterpriseToken(queryParams);
}
