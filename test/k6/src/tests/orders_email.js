/*
    Test script of platform notifications API with application owner token
    Command:
    podman compose run k6 run /src/tests/orders_email.js `
    -e tokenGeneratorUserName={the user name to access the token generator} `
    -e tokenGeneratorUserPwd={the password to access the token generator}`
    -e mpClientId={the id of an integration defined in maskinporten} `
    -e mpKid={the key id of the JSON web key used to sign the maskinporten token request} `
    -e encodedJwk={the encoded JSON web key used to sign the maskinporten token request} `
    -e env={environment: at22, at23, at24, tt02, prod} `
    -e emailRecipient={an email address to add as a notification recipient} `
    -e ninRecipient={a national identity number of a person to include as a notification recipient} `
    -e subscriptionKey={the subscription key with access to the automated tests product} `
    -e runFullTestSet=true

    For use case tests omit environment variable runFullTestSet or set value to false
*/

import { check, sleep } from "k6";
import { notifications } from "../config.js";
import { stopIterationOnFail } from "../errorhandler.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";

import * as setupToken from "../setup.js";
import * as ordersApi from "../api/notifications/orders.js";
import * as notificationsApi from "../api/notifications/notifications.js";

const emailOrderRequestJson = JSON.parse(
    open("../data/orders/01-email-request.json")
);

const scopes = "altinn:serviceowner/notifications.create";
const ninRecipient = __ENV.ninRecipient ? __ENV.ninRecipient.toLowerCase() : null;
const emailRecipient = __ENV.emailRecipient ? __ENV.emailRecipient.toLowerCase() : null;

export const options = {
    thresholds: {
        // Checks rate should be 100%. Raise error if any check has failed.
        checks: ['rate>=1']
    }
};

export function setup() {
    const sendersReference = uuidv4();
    const token = setupToken.getAltinnTokenForOrg(scopes);

    const emailOrderRequest = emailOrderRequestJson;
    emailOrderRequest.sendersReference = sendersReference;
    emailOrderRequest.conditionEndpoint = notifications.conditionCheck(true);
    emailOrderRequest.recipients = [
        {
            emailAddress: emailRecipient
        },
        {
            nationalIdentityNumber: ninRecipient
        }
    ];

    const runFullTestSet = __ENV.runFullTestSet
        ? __ENV.runFullTestSet.toLowerCase().includes("true")
        : false;

    const data = {
        token: token,
        runFullTestSet: runFullTestSet,
        sendersReference: sendersReference,
        emailOrderRequest: emailOrderRequest,
    };

    return data;
}

function TC01_PostEmailNotificationOrderRequest(data) {
    let success;

    const response = ordersApi.postEmailNotificationOrder(
        JSON.stringify(data.emailOrderRequest),
        data.token
    );

    success = check(response, {
        "POST email notification order request. Status is 202 Accepted":
            (r) => r.status === 202
    });

    stopIterationOnFail("POST email notification order request failed", success);

    const selfLink = response.headers["Location"];

    success = check(response, {
        "POST email notification order request. Location header provided":
            (_) => selfLink,
        "POST email notification order request. Recipient lookup was successful":
            (r) => JSON.parse(r.body).recipientLookup.status == 'Success'
    });

    return selfLink;
}

function TC02_GetNotificationOrderById(data, selfLink, orderId) {
    let success;

    const response = ordersApi.getByUrl(selfLink, data.token);

    success = check(response, {
        "GET notification order by id. Status is 200 OK":
            (r) => r.status === 200,
    });

    success = check(JSON.parse(response.body), {
        "GET notification order by id. Id property is a match":
            (order) => order.id === orderId,
        "GET notification order by id. Creator property is a match":
            (order) => order.creator === "ttd",
    });
}

function TC03_GetNotificationOrderBySendersReference(data) {
    let success;

    const response = ordersApi.getBySendersReference(data.sendersReference, data.token);

    success = check(response, {
        "GET notification order by senders reference. Status is 200 OK":
            (r) => r.status === 200,
    });

    success = check(JSON.parse(response.body), {
        "GET notification order by senders reference. Count is equal to 1":
            (orderList) => orderList.count === 1,
        "GET notification order by senders reference. Orderlist contains one element":
            (orderList) => Array.isArray(orderList.orders) && orderList.orders.length == 1,
    });
}

function TC04_GetNotificationOrderWithStatus(data, orderId) {
    let success;

    const response = ordersApi.getWithStatus(orderId, data.token);

    success = check(response, {
        "GET notification order with status. Status is 200 OK":
            (r) => r.status === 200,
    });

    success = check(JSON.parse(response.body), {
        "GET notification order with status. Id property is a match":
            (order) => order.id === orderId,
        "GET notification order with status. NotificationChannel is email":
            (order) => order.notificationChannel === "Email",
        "GET notification order with status. ProcessingStatus is defined":
            (order) => order.processingStatus,
    });
}

function TC05_GetEmailNotificationSummary(data, orderId) {
    let success;

    const response = notificationsApi.getEmailNotifications(orderId, data.token);

    success = check(response, {
        "GET email notifications. Status is 200 OK": (r) => r.status === 200,
    });

    success = check(JSON.parse(response.body), {
        "GET email notifications. OrderId property is a match":
            (notificationSummary) => notificationSummary.orderId === orderId,
    });
}

function TC06_PostEmailNotificationOrderWithNegativeConditionCheck(data) {
    let success, response;

    const emailOrderRequest = data.emailOrderRequest;
    emailOrderRequest.conditionEndpoint = notifications.conditionCheck(false);

    response = ordersApi.postEmailNotificationOrder(
        JSON.stringify(emailOrderRequest),
        data.token
    );

    success = check(response, {
        "POST email notification order request with condition. Status is 202 Accepted":
            (r) => r.status === 202
    });

    stopIterationOnFail("POST email notification order request with condition check failed", success);

    sleep(60); // Waiting 1 minute for the notifications to be generated

    const order = JSON.parse(response.body)

    response = ordersApi.getWithStatus(order.orderId, data.token);
    success = check(response, {
        "GET notification order with condition check status after one minute. Status is 200 OK":
            (r) => r.status === 200,
    });

    success = check(JSON.parse(response.body), {
        "GET notification order with condition check status. Status is condition not met":
            (order) => order.processingStatus.status == "SendConditionNotMet",
    });
}

export default function (data) {
    const selfLink = TC01_PostEmailNotificationOrderRequest(data);
    let id = selfLink.split("/").pop();

    if (data.runFullTestSet) {
        TC02_GetNotificationOrderById(data, selfLink, id);
        TC03_GetNotificationOrderBySendersReference(data);
        TC04_GetNotificationOrderWithStatus(data, id);
        TC05_GetEmailNotificationSummary(data, id);
        TC06_PostEmailNotificationOrderWithNegativeConditionCheck(data);
    } else {
        TC05_GetEmailNotificationSummary(data, id);
    }
}
