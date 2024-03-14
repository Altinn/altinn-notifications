using LinkMobility.PSWin.Receiver.Exceptions;
using LinkMobility.PSWin.Receiver.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace LinkMobility.PSWin.Receiver.Parsers
{
    internal static class MoParser
    {
        internal static MoMessage Parse(XDocument document)
        {
            const string xpath = "MSGLST/MSG";
            var msg = document.XPathSelectElement(xpath);
            if (msg == null)
                throw new MoParserException($"Document does not have {xpath}");
            var text = msg.Element("TEXT")?.Value;
            if (text == null)
                throw new MoParserException($"Document is missing TEXT under {xpath}");
            var sender = msg.Element("SND")?.Value;
            if (sender == null)
                throw new MoParserException($"Document is missing SND under {xpath}");
            var receiver = msg.Element("RCV")?.Value;
            if (receiver == null)
                throw new MoParserException($"Document is missing RCV under {xpath}");
            var address = ParseAddress(msg.Element("ADDRESS"));
            var position = ParsePosition(msg.Element("POSITION"));
            var metadata = ParseMetadata(msg.Element("METADATA"));
            return new MoMessage(text, sender, receiver, address, position, metadata);
        }

        private static Address ParseAddress(XElement element)
        {
            if (element == null)
                return null;

            var csv = element.Value;
            var tokens = csv.Split(';');
            if (tokens.Length < 8)
                throw new MoParserException("ADDRESS is malformed: Not enough fields");
            var firstName = tokens[0];
            var middleName = tokens[1];
            var lastName = tokens[2];
            var street = tokens[3];
            var zipCode = tokens[4];
            var city = tokens[5];
            var regionNumber = tokens[6];
            var countyNumber = tokens[7];
            var additionalFields = tokens.Skip(8).ToArray();
            return new Address(firstName, middleName, lastName, street, zipCode, city, regionNumber, countyNumber, additionalFields);
        }

        private static Position ParsePosition(XElement element)
        {
            if (element == null)
                return null;

            var status = element.Element("STATUS")?.Value;
            var info = element.Element("INFO")?.Value;
            
            if (status == null)
                throw new MoParserException("POSITION/STATUS is missing");
            
            var pos = element.Element("POS");
            if (pos == null)
                return new Position(status, info);

            var longitude = GetOrThrow<float>(pos, "LONGITUDE");
            var latitude = GetOrThrow<float>(pos, "LATITUDE");
            var radius = GetOrThrow<int>(pos, "RADIUS");
            var council = GetOrThrow<string>(pos, "COUNCIL");
            var councilNumber = GetOrThrow<int>(pos, "COUNCILNUMBER");
            var place = GetOrThrow<string>(pos, "PLACE");
            var subplace = GetOrDefault<string>(pos, "SUBPLACE");
            var zipCode = GetOrDefault<string>(pos, "ZIPCODE");
            var city = GetOrDefault<string>(pos, "CITY");
            return new Position(status, info, longitude, latitude, radius, council, councilNumber, place, subplace, zipCode, city);
        }

        private static Metadata ParseMetadata(XElement element)
        {
            if (element == null)
                return null;

            var dictionary = new Dictionary<string, string>();
            var dataElements = element.Elements("DATA");
            foreach (var dataElement in dataElements)
            {
                var key = dataElement.Attribute("KEY")?.Value;
                if (key != null)
                    dictionary.Add(key, dataElement.Attribute("VALUE")?.Value);
            }
            return new Metadata(dictionary);
        }

        private static T GetOrThrow<T>(XElement elem, string tagname)
        {
            if (elem.Element(tagname) == null)
                throw new MoParserException($"{tagname} is missing");
            return GetOrDefault<T>(elem, tagname);
        }

        private static T GetOrDefault<T>(XElement elem, string tagname)
        {
            var child = elem.Element(tagname);
            if (child == null)
                return default;

            try
            {
                return (T)Convert.ChangeType(child.Value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException)
            {
                throw new MoParserException($"{tagname} is not {typeof(T).Name}");
            }
        }
    }
}
