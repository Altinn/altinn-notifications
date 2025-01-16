import { check } from "k6";
import http from "k6/http";
import { platformAuthentication } from "../config.js";
import { buildHeaderWithBearer } from "../apiHelpers.js";
import { stopIterationOnFail } from "../errorhandler.js";
export function exchangeToAltinnToken(token, test) {
  const endpoint = `${platformAuthentication.exchange}?test=${test}`;

  const params = buildHeaderWithBearer(token);

  const res = http.get(endpoint, params);

  const success = check(res, {
    "// Setup // Authentication towards Altinn 3 Success": (r) => r.status === 200,
  });
  
  stopIterationOnFail("// Setup // Authentication towards Altinn 3 Failed", success);

  return res.body;
}
