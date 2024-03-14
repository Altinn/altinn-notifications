using System.Collections.Generic;

namespace LinkMobility.PSWin.Receiver.Model
{
    public class Address
    {
        public Address(string firstName, string middleName, string lastName, string street, string zipCode, string city, string regionNumber, string countyNumber, IReadOnlyList<string> additionalFields)
        {
            FirstName = firstName;
            MiddleName = middleName;
            LastName = lastName;
            Street = street;
            ZipCode = zipCode;
            City = city;
            RegionNumber = regionNumber;
            CountyNumber = countyNumber;
            AdditionalFields = additionalFields;
        }

        public string FirstName { get; }
        public string MiddleName { get; }
        public string LastName { get; }
        public string Street { get; }
        public string ZipCode { get; }
        public string City { get; }
        public string RegionNumber { get; }
        public string CountyNumber { get; }
        public IReadOnlyList<string> AdditionalFields { get; }
    }
}
