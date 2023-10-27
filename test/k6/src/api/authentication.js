import { check } from "k6";
import http from "k6/http";

import {
  buildHeaderWithBearer,
  buildHeaderWithContentType,
  buildHeaderWithCookie,
} from "../apiHelpers.js";
import { platformAuthentication, portalAuthentication } from "../config.js";
import { stopIterationOnFail, addErrorCount } from "../errorhandler.js";

const userName = __ENV.userName;
const userPassword = __ENV.userPassword;

export function exchangeToAltinnToken(token, test) {
  var endpoint = platformAuthentication.exchange + "?test=" + test;
  var params = buildHeaderWithBearer(token);

  var res = http.get(endpoint, params);
  var success = check(res, {
    "// Setup // Authentication towards Altinn 3 Success": (r) =>
      r.status === 200,
  });
  addErrorCount(success);
  stopIterationOnFail(
    "// Setup // Authentication towards Altinn 3  Failed",
    success,
    res
  );

  return res.body;
}
