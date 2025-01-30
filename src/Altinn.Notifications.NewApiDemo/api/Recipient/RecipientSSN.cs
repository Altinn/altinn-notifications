using System.ComponentModel;

namespace WebApplication1;

public class RecipientSSN: Recipient
{
    
    public string? SSN { get; set; }

    [Description("If set to true, the reservation-flag in KRR will not be respected, and the message is sent even to persons actively objecting to the use of digital channels. Default: false")] 
    public bool DisregardKrrReservationFlag { get; set; } = false;
    
    public ChannelScheme ChannelScheme { get; set; }
    
    public EmailSettings EmailSettings { get; set; }
    
    public SMSSettings SMSSettings { get; set; }
}