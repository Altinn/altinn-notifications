export const post_sms_order = "post_sms_order";
export const get_sms_notifications = "get_sms_notifications";

export const post_mail_order = "post_mail_order";
export const get_mail_notifications = "get_mail_notifications";

export const post_sms_order_v2 = "post_sms_order_v2";
export const post_email_order_v2 = "post_email_order_v2";

export const get_sms_shipment = "get_sms_shipment";
export const get_email_shipment = "get_email_shipment";

export const get_sms_instant_shipment = "get_sms_instant_shipment";
export const post_sms_instant_order_v2 = "post_sms_instant_order_v2";

export const get_status_feed = "get_status_feed";

export const post_valid_order = "post_valid_order";
export const post_invalid_order = "post_invalid_order";
export const post_duplicate_order = "post_duplicate_order";
export const post_order_with_resource_id = "post_order_with_resource_id";
export const post_order_without_resource_id = "post_order_without_resource_id";

/**
 * Sets empty thresholds for the specified labels
 * @param {string[]} labels - Array of label names to set thresholds for
 * @param {object} options - Options object containing thresholds configuration
 */
export function setEmptyThresholds(labels, options) {
    for (const label of labels) {
        options.thresholds[`http_reqs{name:${label}}`] = [];
        options.thresholds[`http_req_duration{name:${label}}`] = [];
    }
}
