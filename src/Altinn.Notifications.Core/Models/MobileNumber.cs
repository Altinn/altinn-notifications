using PhoneNumbers;

namespace Altinn.Notifications.Core.Models
{
    /// <summary>
    /// A class describing a mobile number and its properties
    /// </summary>
    public sealed class MobileNumber : IEquatable<MobileNumber>
    {
        private string _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="MobileNumber"/> class.
        /// </summary>
        public MobileNumber(string value)
        {
            _value = value;
        }

        /// <inheritdoc/>
        public bool Equals(MobileNumber? other)
        {
            if (other == null)
            {
                return false;
            }

            return _value == other._value;
        }

        /// <summary>
        /// Validated as mobile number based on the Altinn 2 regex. Country code is required.
        /// </summary>
        /// <returns>A boolean indicating that the mobile number is valid or not</returns>
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(_value) || (!_value.StartsWith('+') && !_value.StartsWith("00")))
                {
                    return false;
                }

                if (_value.StartsWith("00"))
                {
                    _value = "+" + _value.Remove(0, 2);
                }

                PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();
                PhoneNumber phoneNumber = phoneNumberUtil.Parse(_value, null);
                return phoneNumberUtil.IsValidNumber(phoneNumber);
            }
        }

        /// <summary>
        /// Checks if number contains country code, if not it adds the country code for Norway if number starts with 4 or 9
        /// </summary>
        /// <remarks>
        /// This method does not validate the number, only ensures that it has a country code.
        /// </remarks>
        public string EnsureCountryCodeIfApplicable()
        {
            if (string.IsNullOrEmpty(_value))
            {
                return string.Empty;
            }
            else if (_value.StartsWith("00"))
            {
                _value = "+" + _value.Remove(0, 2);
            }
            else if (_value.Length == 8 && (_value[0] == '9' || _value[0] == '4'))
            {
                _value = "+47" + _value;
            }

            return _value;
        }
    }
}
