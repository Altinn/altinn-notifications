import { check } from "k6";
import http from "k6/http";

import {
  buildHeaderWithBearer
} from "../apiHelpers.js";
import { platformAuthentication } from "../config.js";
import { stopIterationOnFail } from "../errorhandler.js";


export function exchangeToAltinnToken(token, test) {
  var endpoint = platformAuthentication.exchange + "?test=" + test;
  var params = buildHeaderWithBearer(token);

  var res = http.get(endpoint, params);
  var success = check(res, {
    "// Setup // Authentication towards Altinn 3 Success": (r) =>
      r.status === 200,
  });
  
  stopIterationOnFail("// Setup // Authentication towards Altinn 3  Failed", success);

  return res.body;
}
