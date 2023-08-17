// Baseurls for platform
export var baseUrls = {
  at21: "at21.altinn.cloud",
  at22: "at22.altinn.cloud",
  at23: "at23.altinn.cloud",
  at24: "at24.altinn.cloud",
  tt02: "tt02.altinn.no",
};

//Get values from environment
const environment = __ENV.env.toLowerCase();
export let baseUrl = baseUrls[environment];

//AltinnTestTools
export var tokenGenerator = {
  getEnterpriseToken:
    "https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken",
};

// Platform Notifications
export var notifications = {
  orders_email:
    "https://platform." + baseUrl + "/notifications/api/v1/orders/email/",
  orders_status: function (orderId) {
    return `https://platform.${baseUrl}/notifications/api/v1/orders/${orderId}/status`;
  },
  orders_fromId: function (orderId) {
    return `https://platform.${baseUrl}/notifications/api/v1/orders/${orderId}`;
  },
  orders_fromSendersRef: function (sendersReference) {
    return `https://platform.${baseUrl}/notifications/api/v1/orders?sendersReference=${sendersReference}`;
  },
};
