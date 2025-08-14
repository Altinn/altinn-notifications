export const yt01Environment = "yt01";
export const resourceId = __ENV.resourceId;
export const scopes = "altinn:serviceowner/notifications.create";
export const environment = __ENV.env ? __ENV.env.toLowerCase() : null;
export const orderTypes = (__ENV.orderTypes || "valid").split(",").map(e => e.trim());

