// Base URLs for platforms
const baseUrls = {
  at22: "at22.altinn.cloud",
  at23: "at23.altinn.cloud",
  at24: "at24.altinn.cloud",
  tt02: "tt02.altinn.no",
  prod: "altinn.no"
};

const maskinportenBaseUrls = {
  prod: "https://maskinporten.no/",
  tt02: "https://test.maskinporten.no/"
};

// Get values from environment
const environment = __ENV.env.toLowerCase();
const subscriptionKey = __ENV.subscriptionKey;

const baseUrl = baseUrls[environment];
const maskinportenBaseUrl = maskinportenBaseUrls[environment];

// AltinnTestTools
export const tokenGenerator = {
  getEnterpriseToken:
    "https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken",
};

// Platform Notifications
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

// Platform Authentication
export const platformAuthentication = {
  exchange: `https://platform.${baseUrl}/authentication/api/v1/exchange/maskinporten`,
};

// Maskinporten
export const maskinporten = {
  audience: maskinportenBaseUrl,
  token: `${maskinportenBaseUrl}token`,
};
