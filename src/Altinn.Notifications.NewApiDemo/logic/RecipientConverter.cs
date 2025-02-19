namespace Altinn.Notifications.NewApiDemo.logic;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Altinn.Notifications.NewApiDemo.api.Recipient.Notification;

public class RecipientConverter : JsonConverter<Object>
{
    public override Object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object");
        }

        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var rootElement = jsonDoc.RootElement;

        if (!rootElement.TryGetProperty("recipientType", out var typeProperty))
        {
            throw new JsonException("Missing recipientType property");
        }

        var recipientType = typeProperty.GetString();
        var json = rootElement.GetRawText();

        return recipientType switch
        {
            "email" => JsonSerializer.Deserialize<RecipientEmail>(json, options)!,
            "sms" => JsonSerializer.Deserialize<RecipientSms>(json, options)!,
            "ssn" => JsonSerializer.Deserialize<RecipientSSN>(json, options)!,
            "org" => JsonSerializer.Deserialize<RecipientOrg>(json, options)!,
            _ => throw new JsonException($"Invalid recipient type: {recipientType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, Object value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
