using System.Reflection;
using System.Text;

using Altinn.Notifications.Tools.SmsDeliveryReporter.Configuration;

using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
var config = builder.Build();

ReporterSettings settings = new();
config.GetRequiredSection("ReporterSettings").Bind(settings);

if (string.IsNullOrWhiteSpace(settings.EndpointUrl))
{
    Console.Error.WriteLine("ReporterSettings:EndpointUrl is required.");
    return 1;
}

if (string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password))
{
    Console.Error.WriteLine("ReporterSettings:Username and Password are required. Set them via user secrets:");
    Console.Error.WriteLine("  dotnet user-secrets set \"ReporterSettings:Username\" \"<value>\"");
    Console.Error.WriteLine("  dotnet user-secrets set \"ReporterSettings:Password\" \"<value>\"");
    return 1;
}

var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"));
var lines = File.ReadAllLines(settings.InputFile);
var deliveryTime = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

using var http = new HttpClient();
http.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");

var succeeded = 0;
var failed = 0;

for (var i = 0; i < lines.Length; i++)
{
    var line = lines[i].Trim();
    if (string.IsNullOrEmpty(line))
        continue;

    var parts = line.Split("\",\"");
    if (parts.Length != 2)
    {
        Console.Error.WriteLine($"Line {i + 1}: unexpected format, skipping: {line}");
        continue;
    }

    var status = parts[0].TrimStart('"');
    var gatewayRef = parts[1].TrimEnd('"');
    var state = MapState(status);

    var xml = $"<?xml version=\"1.0\"?><!DOCTYPE MSGLST SYSTEM \"pswincom_report_request.dtd\"><MSGLST><MSG><ID>1</ID><REF>{gatewayRef}</REF><RCV>4512345678</RCV><STATE>{state}</STATE><DELIVERYTIME>{deliveryTime}</DELIVERYTIME></MSG></MSGLST>";

    using var content = new StringContent(xml, Encoding.UTF8, "application/xml");
    var response = await http.PostAsync(settings.EndpointUrl, content);

    if (response.IsSuccessStatusCode)
    {
        Console.WriteLine($"[{i + 1}/{lines.Length}] OK  {status,-15} {gatewayRef}");
        succeeded++;
    }
    else
    {
        var body = await response.Content.ReadAsStringAsync();
        Console.Error.WriteLine($"[{i + 1}/{lines.Length}] FAIL {(int)response.StatusCode} {status,-15} {gatewayRef}: {body}");
        failed++;
    }
}

Console.WriteLine($"\nDone. {succeeded} succeeded, {failed} failed.");
return failed > 0 ? 1 : 0;

static string MapState(string status) => status switch
{
    "Delivered" => "DELIVRD",
    "Failed" => "FAILED",
    "Failed_Expired" => "EXPIRED",
    "Failed_Deleted" => "DELETED",
    "Failed_Undelivered" => "UNDELIV",
    "Failed_Rejected" => "REJECTD",
    "Failed_BarredReceiver" => "BARRED",
    _ => status.ToUpperInvariant()
};
