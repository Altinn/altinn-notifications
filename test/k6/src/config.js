// Baseurls for platform
const baseUrls = {
  at21: "at21.altinn.cloud",
  at22: "at22.altinn.cloud",
  at23: "at23.altinn.cloud",
  at24: "at24.altinn.cloud",
  tt02: "tt02.altinn.no",
  prod: "altinn.no"
};

const maskinportenBaseUrls = {
  tt02: "https://test.maskinporten.no/",
  prod: "https://maskinporten.no/",
};

// Get values from environment
const environment = __ENV.env.toLowerCase();
const baseUrl = baseUrls[environment];
const maskinportenBaseUrl = maskinportenBaseUrls[environment];

// AltinnTestTools
export var tokenGenerator = {
  getEnterpriseToken:
    "https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken",
};

// Platform Notifications
export var notifications = {
  orders_email:
    "https://platform." + baseUrl + "/notifications/api/v1/orders/email/",
  orders_sms:
    "https://platform." + baseUrl + "/notifications/api/v1/orders/sms/",
  orders_status: function (orderId) {
    return `https://platform.${baseUrl}/notifications/api/v1/orders/${orderId}/status`;
  },
  orders_fromId: function (orderId) {
    return `https://platform.${baseUrl}/notifications/api/v1/orders/${orderId}`;
  },
  orders_fromSendersRef: function (sendersReference) {
    return `https://platform.${baseUrl}/notifications/api/v1/orders?sendersReference=${sendersReference}`;
  },
  notifications_email: function (orderId) {
    return `https://platform.${baseUrl}/notifications/api/v1/orders/${orderId}/notifications/email/`;
  },
  notifications_sms: function (orderId) {
    return `https://platform.${baseUrl}/notifications/api/v1/orders/${orderId}/notifications/sms/`;
  },
  conditionCheck: function(conditionMet) {
    return `http://altinn-notifications.default.svc.cluster.local/notifications/api/v1/tests/sendcondition?conditionMet=${conditionMet}`;
  }
};

// Platform Authentication
export var platformAuthentication = {
  exchange:
    "https://platform." + baseUrl + "/authentication/api/v1/exchange/maskinporten",
};

// Maskinporten
export var maskinporten = {
  audience: maskinportenBaseUrl,
  token: maskinportenBaseUrl + "token",
};
