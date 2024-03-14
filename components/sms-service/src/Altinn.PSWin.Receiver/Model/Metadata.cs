using System;
using System.Collections.Generic;
using System.Globalization;

namespace LinkMobility.PSWin.Receiver.Model
{
    public class Metadata
    {
        public Metadata(Dictionary<string, string> data)
        {
            Data = data;
        }

        public DateTime TimeStamp
        {
            get
            {
                var timestamp = Get("TIMESTAMP");
                if (timestamp == null)
                    return default;
                return DateTime.ParseExact(timestamp, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
        }

        public string Reference => Get("REFERENCE");

        public IReadOnlyDictionary<string, string> Data { get; }

        private string Get(string key, string defaultValue = null)
        {
            if (Data.ContainsKey(key))
                return Data[key];
            return defaultValue;
        }
    }
}