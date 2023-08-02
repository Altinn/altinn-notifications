import * as tokenGenerator from "./api/token-generator.js";
import http from 'k6/http';
const environment = __ENV.env.toLowerCase();

/*
 * generate an altinn token for org based on the environment using AltinnTestTools
 * If org is not provided TTD will be used.
 * @returns altinn token with the provided scopes for an org
 */
export function getAltinnTokenForOrg(scopes, org = "ttd", orgNo = "991825827") {
  //TODO: Handle login for prod
  var queryParams = {
    env: environment,
    scopes: scopes,
    org: org,
    orgNo: orgNo,
  };

  return tokenGenerator.generateEnterpriseToken(queryParams);
}

export function getAltinnTokenForUser(userId, partyId, pid) {
  //TODO: Handle login for prod
  var queryParams = {
    env: environment,
    userId: userId,
    partyId: partyId,
    pid: pid,
  };

  return tokenGenerator.generatePersonalToken(queryParams);
}
