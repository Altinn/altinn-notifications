import { stopIterationOnFail } from "./errorhandler.js";

// Base URLs for the Altinn platform across different environments.
const baseUrls = {
    prod: "altinn.no",
    tt02: "tt02.altinn.no",
    at22: "at22.altinn.cloud",
    at23: "at23.altinn.cloud",
    at24: "at24.altinn.cloud"
};

// Base URLs for Maskinporten authentication service in different environments.
const maskinportenBaseUrls = {
    prod: "https://maskinporten.no/",
    tt02: "https://test.maskinporten.no/"
};

const environment = __ENV.env ? __ENV.env.toLowerCase() : null;
if (!environment) {
    stopIterationOnFail("Environment variable 'env' is not set", false);
}

const baseUrl = baseUrls[environment];
if (!baseUrl) {
    stopIterationOnFail(`Invalid value for environment variable 'env': '${environment}'.`, false);
}

const subscriptionKey = __ENV.subscriptionKey;
const maskinportenBaseUrl = maskinportenBaseUrls[environment];

// Altinn TestTools token generator URL.
export const tokenGenerator = {
    getEnterpriseToken:
        "https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken"
};

// Endpoints for interacting with the Altinn Notifications API in different environments.
export const notifications = {
    orders_sms: `https://platform.${baseUrl}/notifications/api/v1/orders/sms/`,

    orders_email: `https://platform.${baseUrl}/notifications/api/v1/orders/email/`,

    orders_fromId: (orderId) => `https://platform.${baseUrl}/notifications/api/v1/orders/${orderId}`,

    orders_status: (orderId) => `https://platform.${baseUrl}/notifications/api/v1/orders/${orderId}/status`,

    notifications_sms: (orderId) => `https://platform.${baseUrl}/notifications/api/v1/orders/${orderId}/notifications/sms/`,

    notifications_email: (orderId) => `https://platform.${baseUrl}/notifications/api/v1/orders/${orderId}/notifications/email/`,

    orders_fromSendersRef: (sendersReference) => `https://platform.${baseUrl}/notifications/api/v1/orders?sendersReference=${sendersReference}`,

    conditionCheck: (conditionMet) => `https://platform.${baseUrl}/notifications/api/v1/tests/sendcondition?conditionMet=${conditionMet}&subscription-key=${subscriptionKey}`
};

// Provides endpoints for handling authentication work-flows on the Altinn platform.
export const platformAuthentication = {
    exchange: `https://platform.${baseUrl}/authentication/api/v1/exchange/maskinporten`,
};

/**
 * Maskinporten URLs.
 * Contains endpoints and audience values related to the Maskinporten authentication service.
 */
export const maskinporten = {
    audience: maskinportenBaseUrl,
    token: `${maskinportenBaseUrl}token`
};
