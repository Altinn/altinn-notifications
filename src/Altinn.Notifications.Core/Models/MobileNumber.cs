using PhoneNumbers;

namespace Altinn.Notifications.Core.Models
{
    /// <summary>
    /// A class describing a mobile number and its properties
    /// </summary>
    public sealed class MobileNumber : IEquatable<MobileNumber>, IEquatable<string>
    {
        private string _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="MobileNumber"/> class.
        /// </summary>
        public MobileNumber(string value)
        {
            _value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MobileNumber"/> class.
        /// </summary>
        public MobileNumber()
        {
            _value = string.Empty;
        }

        /// <inheritdoc/>
        public bool Equals(MobileNumber? other)
        {
            if (string.IsNullOrEmpty(_value) && string.IsNullOrEmpty(other?._value))
            {
                return true;
            }
            else if (string.IsNullOrEmpty(_value) || string.IsNullOrEmpty(other?._value))
            {
                return false;
            }

            return _value == other._value;
        }

        /// <summary>
        /// Checks for equality
        /// </summary>
        public static bool operator ==(MobileNumber? obj1, MobileNumber? obj2)
        {
            if (string.IsNullOrEmpty(obj1?._value) && string.IsNullOrEmpty(obj2?._value))
            {
                return true;
            }
            else if (string.IsNullOrEmpty(obj1?._value) || string.IsNullOrEmpty(obj2?._value))
            {
                return false;
            }

            return obj1._value == obj2!._value;

        }

        /// <summary>
        /// Checks for inequality
        /// </summary>
        public static bool operator !=(MobileNumber? obj1, MobileNumber? obj2)
        {
            return !(obj1 == obj2);
        }

        /// <inheritdoc/>
        public bool Equals(string? other)
        {
            return _value.Equals(other);
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

        /// <summary>
        /// Converts to string value
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return _value;
        }
    }
}
