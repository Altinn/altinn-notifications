// Baseurls for platform
export var baseUrls = {
  at21: "at21.altinn.cloud",
  at22: "at22.altinn.cloud",
  at23: "at23.altinn.cloud",
  at24: "at24.altinn.cloud",
  tt02: "tt02.altinn.no"
};

//Get values from environment
const environment = __ENV.env.toLowerCase();
export let baseUrl = baseUrls[environment];

//AltinnTestTools
export var tokenGenerator = {
  getEnterpriseToken:
    "https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken",
  getPersonalToken:
    "https://altinn-testtools-token-generator.azurewebsites.net/api/GetPersonalToken",
};

// Platform Notifications
export var notifications = {
  orders: "https://platform." + baseUrl + "/notifications/api/v1/orders/",
  orders_email: "https://platform." + baseUrl + "/notifications/api/v1/orders/email/"
};
