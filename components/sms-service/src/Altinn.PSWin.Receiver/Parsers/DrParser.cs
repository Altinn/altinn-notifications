using LinkMobility.PSWin.Receiver.Exceptions;
using LinkMobility.PSWin.Receiver.Model;

using System;
using System.Xml.Linq;
using System.Xml.XPath;

namespace LinkMobility.PSWin.Receiver.Parsers
{
    internal static class DrParser
    {
        internal static DrMessage Parse(XDocument document)
        {
            const string xpath = "MSGLST/MSG";
            var msg = document.XPathSelectElement(xpath);
            var id = (msg.Element("ID")?.Value) ?? throw new DrParserException($"Missing element ID under {xpath}");

            var receiver = (msg.Element("RCV")?.Value) ?? throw new DrParserException($"Missing element RCV under {xpath}");
            var reference = (msg.Element("REF")?.Value) ?? throw new DrParserException($"Missing element REF under {xpath}");
            var state = (msg.Element("STATE")?.Value) ?? throw new DrParserException($"Missing element STATE under {xpath}");
            var deliverytime = msg.Element("DELIVERYTIME")?.Value;

            return new DrMessage(id, reference, receiver, EnumParseOrDefault(state, DeliveryState.UNKNOWN), deliverytime);
        }

        private static T EnumParseOrDefault<T>(string state, T defaultValue) where T : struct
        {
            if (Enum.TryParse<T>(state, out var value))
                return value;
            return defaultValue;
        }
    }
}
