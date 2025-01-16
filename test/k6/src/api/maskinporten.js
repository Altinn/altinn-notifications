import http from "k6/http";
import { check } from "k6";
import encoding from "k6/encoding";
import * as config from "../config.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { buildHeaderWithContentType } from "../apiHelpers.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import KJUR from "https://unpkg.com/jsrsasign@10.8.6/lib/jsrsasign.js";

const mpKid = __ENV.mpKid;
const encodedJwk = __ENV.encodedJwk;
const mpClientId = __ENV.mpClientId;

export function generateAccessToken(scopes) {
  if (!encodedJwk) {
    stopIterationOnFail("Required environment variable Encoded JWK (encodedJWK) was not provided", false);
  }

  if (!mpClientId) {
    stopIterationOnFail("Required environment variable maskinporten client id (mpClientId) was not provided", false);
  }

  if (!mpKid) {
    stopIterationOnFail("Required environment variable maskinporten kid (mpKid) was not provided", false);
  }

  const grant = createJwtGrant(scopes);

  const body = {
    alg: "RS256",
    grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer",
    assertion: grant,
  };

  const res = http.post(config.maskinporten.token, body, buildHeaderWithContentType("application/x-www-form-urlencoded"));

  const success = check(res, {
    "// Setup // Authentication towards Maskinporten Success": (r) => r.status === 200,
  });

  stopIterationOnFail("// Setup // Authentication towards Maskinporten Failed", success);

  const accessToken = JSON.parse(res.body)['access_token'];

  return accessToken;
}

function createJwtGrant(scopes) {
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

  const signedJWT = KJUR.jws.JWS.sign(
    "RS256",
    header,
    payload,
    JSON.parse(encoding.b64decode(encodedJwk, "std", "s"))
  );

  return signedJWT;
}
