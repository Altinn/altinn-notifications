namespace Altinn.Notifications.Tests.TestData
{
    internal class TestDataConstants
    {
        public static string OrderStatusFeedTestOrderCompleted => @"{
              ""Status"": ""Order_Completed"",
              ""Recipients"": [
                {
                  ""Type"": ""Email"",
                  ""Status"": ""Delivered"",
                  ""Destination"": ""navn.navnesen@example.com""
                },
                {
                  ""Type"": ""SMS"",
                  ""Status"": ""Delivered"",
                  ""Destination"": ""+4799999999""
                }
              ],
              ""ShipmentId"": ""f5d51690-87c8-4df8-a980-15f4554337e8"",
              ""LastUpdated"": ""2025-03-28T16:24:17.8182889+01:00"",
              ""ShipmentType"": ""Notification"",
              ""SendersReference"": ""Random-Senders-Reference-55027""
            }";

        public static string OrderStatusFeedTestOrderCompleted2 => @"{
              ""Status"": ""Order_Completed"",
              ""Recipients"": [
                {
                  ""Type"": ""Email"",
                  ""Status"": ""Delivered"",
                  ""Destination"": ""navn.navnesen@example.com""
                },
                {
                  ""Type"": ""SMS"",
                  ""Status"": ""Delivered"",
                  ""Destination"": ""+4799999999""
                }
              ],
              ""ShipmentId"": ""8c3de834-830f-4c81-b548-0983a59f76df"",
              ""LastUpdated"": ""2025-03-31T16:24:17.8182889+01:00"",
              ""ShipmentType"": ""Notification"",
              ""SendersReference"": ""Random-Senders-Reference-55028""
            }";
    }
}
