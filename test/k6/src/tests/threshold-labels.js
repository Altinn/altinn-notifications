export const post_mail_order = "post_mail_order";
export const get_mail_notifications = "get_mail_notifications";
export const post_sms_order = "post_sms_order";
export const get_sms_notifications = "get_sms_notifications";

export function setEmptyThresholds(labels, options) {
    for (var label of labels) {
        options.thresholds[`http_req_duration{name:${label}}`] = [];
        options.thresholds[`http_reqs{name:${label}}`] = [];
    };
}