import { environment, localEnvironment } from "./shared/variables.js";

// Base URLs for the Altinn platform across different environments.
const baseUrls = {
    prod: "altinn.no",
    tt02: "tt02.altinn.no",
    yt01: "yt01.altinn.cloud",
    at22: "at22.altinn.cloud",
    at23: "at23.altinn.cloud",
    at24: "at24.altinn.cloud",
};

// Base URLs for Maskinporten authentication service in different environments.
const maskinportenBaseUrls = {
    prod: "https://maskinporten.no/",
    tt02: "https://test.maskinporten.no/",
};

if (!environment) {
    throw new Error("Environment variable 'altinn_env' is not set");
}

const isLocal = environment === localEnvironment;

const baseUrl = baseUrls[environment];
if (!baseUrl && !isLocal) {
    throw new Error(
        `Invalid value for environment variable 'altinn_env': '${environment}'.`
    );
}

// The local Aspire stack (tools/aspire) serves the API on 5090 and the mock
// token issuer on 5094. Override them with 'base_url' and 'token_generator_url',
// e.g. with host.docker.internal when k6 runs in a container.
const platformUrl = isLocal
    ? (__ENV.base_url || "http://localhost:5090").replace(/\/+$/, "")
    : `https://platform.${baseUrl}`;

const tokenGeneratorUrl = isLocal
    ? (__ENV.token_generator_url || "http://localhost:5094").replace(/\/+$/, "")
    : "https://altinn-testtools-token-generator.azurewebsites.net";

// The API calls the condition endpoint itself, so this is the condition mock
// on the API's machine. The API only accepts condition URLs with a top-level
// domain, and local.altinn.cloud resolves to 127.0.0.1.
const localConditionUrl = "http://local.altinn.cloud:5199/condition";

const maskinportenBaseUrl = maskinportenBaseUrls[environment];

// Altinn TestTools token generator URL, or the mock token issuer locally.
export const tokenGenerator = {
    getEnterpriseToken: `${tokenGeneratorUrl}/api/GetEnterpriseToken`,
};

// Endpoints for interacting with the Altinn Notifications API in different environments.
export const notifications = {
    orders_sms: `${platformUrl}/notifications/api/v1/orders/sms/`,

    orders_email: `${platformUrl}/notifications/api/v1/orders/email/`,

    orders_v2: `${platformUrl}/notifications/api/v1/future/orders/`,

    orders_sms_instant_old_v2: `${platformUrl}/notifications/api/v1/future/orders/instant`,
    orders_sms_instant_v2: `${platformUrl}/notifications/api/v1/future/orders/instant/sms`,
    orders_email_instant_v2: `${platformUrl}/notifications/api/v1/future/orders/instant/email`,
    orders_composed_email_v2: `${platformUrl}/notifications/api/v1/future/orders/composed-email`,

    shipment_v2: (orderId) =>
        `${platformUrl}/notifications/api/v1/future/shipment/${orderId}`,

    statusfeed_v2: (sequenceNumber) =>
        `${platformUrl}/notifications/api/v1/future/shipment/feed?seq=${sequenceNumber}`,

    orders_fromId: (orderId) =>
        `${platformUrl}/notifications/api/v1/orders/${orderId}`,

    orders_status: (orderId) =>
        `${platformUrl}/notifications/api/v1/orders/${orderId}/status`,

    notifications_sms: (orderId) =>
        `${platformUrl}/notifications/api/v1/orders/${orderId}/notifications/sms/`,

    notifications_email: (orderId) =>
        `${platformUrl}/notifications/api/v1/orders/${orderId}/notifications/email/`,

    orders_fromSendersRef: (sendersReference) =>
        `${platformUrl}/notifications/api/v1/orders?sendersReference=${sendersReference}`,

    conditionCheck: (conditionMet, subscriptionKey) =>
        isLocal
            ? `${localConditionUrl}/${conditionMet}`
            : `${platformUrl}/notifications/api/v1/tests/sendcondition?conditionMet=${conditionMet}&subscription-key=${subscriptionKey}`,
};

// Provides endpoints for handling authentication work-flows on the Altinn platform.
export const platformAuthentication = {
    exchange: `${platformUrl}/authentication/api/v1/exchange/maskinporten`,
};

/**
 * Maskinporten URLs.
 * Contains endpoints and audience values related to the Maskinporten authentication service.
 */
export const maskinporten = {
    audience: maskinportenBaseUrl,
    token: `${maskinportenBaseUrl}token`,
};
