namespace WebApplication1;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.ComponentModel;


public class RecipientOrg: Recipient
{
    string OrgNumber { get; set; }
    
    ChannelScheme ChannelScheme { get; set; }
    
    EmailSettings EmailSettings { get; set; }
    
    SMSSettings SMSSettings { get; set; }
}
