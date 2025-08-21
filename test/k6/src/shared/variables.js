export const yt01Environment = "yt01";
export const resourceId = __ENV.resourceId ?? null;
export const scopes = "altinn:serviceowner/notifications.create";
export const environment = __ENV.env ? __ENV.env.toLowerCase() : null;
export const performanceTestScenario = __ENV.performanceTestScenario || 'standard';
export const orderTypes = (__ENV.orderTypes || "valid").split(",").map(e => e.trim());
export const ninRecipient = __ENV.ninRecipient ? __ENV.ninRecipient.toLowerCase() : null;

