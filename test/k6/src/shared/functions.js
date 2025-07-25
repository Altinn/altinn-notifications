import { randomItem } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { yt01Environment, environment } from "./variables.js";
import { orgNosYt01 } from "../data/orgnos.js";

/**
 * Gets the recipient based on environment variables
 */
export function getOrgNoRecipient() {
    if (!__ENV.orgNoRecipient && environment === yt01Environment) {
        return randomItem(orgNosYt01);
    }
    else {
        return __ENV.orgNoRecipient ? __ENV.orgNoRecipient.toLowerCase() : null;
    }
}
