import { randomItem } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { yt01Environment, environment } from "./variables.js";
import { orgNosYt01 } from "../data/orgnos.js";
import { nationalIdentityNumbers } from "../data/national-identity-numbers.js";

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

export function getNinRecipient() {
    if (__ENV.ninRecipient && environment === yt01Environment) {
        return randomItem(nationalIdentityNumbers);
    } else {
        return __ENV.ninRecipient ? __ENV.ninRecipient.toLowerCase() : null;
    }
}

export function getEmailRecipient() {
    if (__ENV.emailRecipient) {
        return __ENV.emailRecipient.toLowerCase();
    }
    if (environment === yt01Environment) {
        return "noreply@altinn.no";
    }
    return null;
}

export function getSmsRecipient() {
    if (__ENV.smsRecipient) {
        return __ENV.smsRecipient.toLowerCase();
    }
    if (environment === yt01Environment) {
        return "+4799999999";
    }
    return null;
}
