using System.Xml;
using System.Xml.Linq;

namespace Altinn.Notifications.Tools.LinkMobilityProxy;

/// <summary>
/// A message in a send request.
/// </summary>
internal sealed record SmsMessage(string Id, string Recipient);

/// <summary>
/// The outcome of one message in a send response: a gateway reference when it was accepted, or the reason it was not.
/// </summary>
internal sealed record SmsResult(string Id, string? Reference, string? Error = null);

/// <summary>
/// Reads and writes the XML of LinkMobility's PSWin gateway: the <c>SESSION</c> a send posts, the answer to it,
/// and the <c>MSGLST</c> of a delivery report.
/// </summary>
internal static class GatewayXml
{
    /// <summary>
    /// Reads the messages of a send, <c>SESSION/MSGLST/MSG</c> with <c>ID</c> and <c>RCV</c>.
    /// </summary>
    public static bool TryReadSend(string? xml, out List<SmsMessage> messages)
    {
        messages = [];
        var session = Parse(xml);
        if (session?.Name != "SESSION")
        {
            return false;
        }

        messages = session.Elements("MSGLST").Elements("MSG")
            .Select(m => new SmsMessage((string?)m.Element("ID") ?? string.Empty, (string?)m.Element("RCV") ?? string.Empty))
            .ToList();

        return messages.Count > 0;
    }

    /// <summary>
    /// The answer to a send: <c>LOGON OK</c>, then <c>STATUS OK</c> with a <c>REF</c> or <c>STATUS FAIL</c> with an <c>INFO</c> per message.
    /// </summary>
    public static string SendResponse(IEnumerable<SmsResult> results)
    {
        var messages = results.Select(r => new XElement(
            "MSG",
            new XElement("ID", r.Id),
            new XElement("STATUS", r.Reference is null ? "FAIL" : "OK"),
            new XElement("INFO", r.Error ?? string.Empty),
            r.Reference is null ? null : new XElement("REF", r.Reference)));

        var session = new XElement("SESSION", new XElement("LOGON", "OK"), new XElement("REASON", string.Empty), new XElement("MSGLST", messages));
        return """<?xml version="1.0" encoding="ISO-8859-1"?>""" + session.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// The messages an answer accepted, those with <c>STATUS OK</c> and a <c>REF</c>, e.g. from an operator in the test harness control panel.
    /// </summary>
    public static List<SmsResult> ReadAccepted(string? xml)
    {
        var session = Parse(xml);
        if (session?.Name != "SESSION" || (string?)session.Element("LOGON") != "OK")
        {
            return [];
        }

        return session.Elements("MSGLST").Elements("MSG")
            .Where(m => (string?)m.Element("STATUS") == "OK" && !string.IsNullOrEmpty((string?)m.Element("REF")))
            .Select(m => new SmsResult((string?)m.Element("ID") ?? string.Empty, (string)m.Element("REF")!))
            .ToList();
    }

    /// <summary>
    /// The delivery report the SMS service's <c>GatewayReceiver</c> parses.
    /// </summary>
    public static string DeliveryReport(string reference, string recipient, string state, DateTimeOffset deliveryTime)
    {
        var message = new XElement(
            "MSG",
            new XElement("ID", "1"),
            new XElement("REF", reference),
            new XElement("RCV", recipient),
            new XElement("STATE", state),
            new XElement("DELIVERYTIME", deliveryTime.ToString("yyyy.MM.dd HH:mm:ss")));

        return """<?xml version="1.0"?>""" + new XElement("MSGLST", message).ToString(SaveOptions.DisableFormatting);
    }

    private static XElement? Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            return XDocument.Parse(xml).Root;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
