using System.Text.Json.Serialization;

using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models.Address;

/// <summary>
/// A class represeting an sms address point
/// </summary>
public class SmsAddressPoint : IAddressPoint
{
    /// <inheritdoc/>
    public AddressType AddressType { get; internal set; }

    /// <summary>
    /// Gets the email address
    /// </summary>
    [JsonIgnore]
    public MobileNumber MobileNumber { get; internal set; }

    [JsonPropertyName("MobileNumber")]
    private string MobileNumberValue => MobileNumber.ToString();

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsAddressPoint"/> class.
    /// </summary>
    public SmsAddressPoint(MobileNumber mobileNumber)
    {
        AddressType = AddressType.Sms;
        MobileNumber = mobileNumber;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsAddressPoint"/> class.
    /// </summary>
    [JsonConstructor]
    public SmsAddressPoint(string mobileNumber)
    {
        AddressType = AddressType.Sms;
        MobileNumber = new MobileNumber(mobileNumber);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsAddressPoint"/> class.
    /// </summary>
    internal SmsAddressPoint()
    {
        MobileNumber = new(string.Empty);
    }
}
