import * as ordersApi from "./api/notifications/orders.js";

/**
 * Gets a notification order by its ID.
 * @param {Object} data - The data object containing token.
 * @param {string} selfLink - The selfLink of the order.
 * @param {string} orderId - The order identifier.
 */
export function getNotificationOrderById(data, selfLink, orderId) {
    const response = ordersApi.getByUrl(selfLink, data.token);

    check(response, {
        "GET notification order by id. Status is 200 OK": (r) => r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET notification order by id. Id property is a match": (order) => order.id === orderId,
        "GET notification order by id. Creator property is a match": (order) => order.creator === "ttd",
    });
}

/**
 * Gets a notification order by the sender's reference.
 * @param {Object} data - The data object containing sendersReference and token.
 */
export function getNotificationOrderBySendersReference(data) {
    const response = ordersApi.getBySendersReference(data.sendersReference, data.token);

    check(response, {
        "GET notification order by senders reference. Status is 200 OK": (r) => r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET notification order by senders reference. Count is equal to 1": (orderList) => orderList.count === 1,
        "GET notification order by senders reference. Orderlist contains one element": (orderList) => Array.isArray(orderList.orders) && orderList.orders.length == 1,
    });
}

/**
 * Gets a notification order with its status.
 * @param {Object} data - The data object containing token.
 * @param {string} orderId - The ID of the order.
 */
export function getNotificationOrderWithStatus(data, orderId) {
    const response = ordersApi.getWithStatus(orderId, data.token);

    check(response, {
        "GET notification order with status. Status is 200 OK": (r) => r.status === 200,
    });

    check(JSON.parse(response.body), {
        "GET notification order with status. Id property is a match": (order) => order.id === orderId,
        "GET notification order with status. NotificationChannel is email": (order) => order.notificationChannel === "Email",
        "GET notification order with status. ProcessingStatus is defined": (order) => order.processingStatus,
    });
}