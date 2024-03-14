namespace LinkMobility.PSWin.Receiver.Model
{
    public class Position
    {
        public Position(string status, string info)
        {
            Status = status;
            Info = info;
        }

        public Position(string status, string info, float longitude, float latitude, int radius, string council, int councilNumber, string place, string subplace, string zipCode, string city)
            : this(status, info)
        {
            Longitude = longitude;
            Latitude = latitude;
            Radius = radius;
            Council = council;
            CouncilNumber = councilNumber;
            Place = place;
            SubPlace = subplace;
            ZipCode = zipCode;
            City = city;
        }

        public string Status { get; }
        public string Info { get; }
        public float Longitude { get; }
        public float Latitude { get; }
        public int Radius { get; }
        public string Council { get; }
        public int CouncilNumber { get; }
        public string Place { get; }
        public string SubPlace { get; }
        public string ZipCode { get; }
        public string City { get; }
    }
}