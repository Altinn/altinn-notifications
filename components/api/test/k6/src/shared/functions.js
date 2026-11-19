import { randomItem } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { yt01Environment, localEnvironment, environment } from "./variables.js";
import { orgNosYt01 } from "../data/orgnos.js";
import { nationalIdentityNumbers } from "../data/national-identity-numbers.js";

// Defaults for the local Aspire stack, the same as in
// components/api/test/bruno/aspire.env. The platform mocks give any person or
// organisation contact points, so these are just fixed values.
const localTestData = {
    email: "aspire@example.com",
    mobileNumber: "+4799999999",
};

/**
 * Gets the recipient based on environment variables
 */
export function getOrgNoRecipient() {
    if (!__ENV.orgNoRecipient && environment === yt01Environment) {
        return randomItem(orgNosYt01);
    } else {
        return __ENV.orgNoRecipient ? __ENV.orgNoRecipient.toLowerCase() : null;
    }
}

export function getNinRecipient() {
    if (!__ENV.ninRecipient && environment === yt01Environment) {
        return randomItem(nationalIdentityNumbers);
    } else {
        return __ENV.ninRecipient ? __ENV.ninRecipient : null;
    }
}

export function getEmailRecipient() {
    if (__ENV.emailRecipient) {
        return __ENV.emailRecipient.toLowerCase();
    }
    if (environment === yt01Environment) {
        return "noreply@altinn.no";
    }
    if (environment === localEnvironment) {
        return localTestData.email;
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
    if (environment === localEnvironment) {
        return localTestData.mobileNumber;
    }
    return null;
}
