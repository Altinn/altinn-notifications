using Altinn.Notifications.Tools.DlqManager.Services.Queues;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Notifications.Tools.DlqManager.Services;

/// <summary>
/// Top-level interactive menu that lets the operator choose which ASB queue to work with.
/// Each queue delegates to its own service for the queue-specific sub-menu.
/// </summary>
public class ConsoleMenuService(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _sp = serviceProvider;

    private static readonly IReadOnlyList<string> _queueLabels =
    [
        "altinn.notifications.sms.send",            // 1 — implemented
        "altinn.notifications.sms.deliveryreports",  // 2
        "altinn.notifications.sms.send.result",      // 3
        "altinn.notifications.email.send",            // 4
        "altinn.notifications.email.deliveryreports", // 5
        "altinn.notifications.email.send.result",     // 6
        "altinn.notifications.email.send.ratelimit",  // 7
        "altinn.notifications.email.check.send.status", // 8
        "altinn.notifications.orders.pastdue"         // 9
    ];

    public async Task<int> RunMenuAsync()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== Altinn Notifications — DLQ Manager ===");
            Console.WriteLine();
            Console.WriteLine("Select queue:");

            for (int i = 0; i < _queueLabels.Count; i++)
            {
                string suffix = i == 0 ? string.Empty : "  (not yet implemented)";
                Console.WriteLine($"  {i + 1}. {_queueLabels[i]}{suffix}");
            }

            Console.WriteLine("  0. Exit");
            Console.WriteLine();
            Console.Write($"Enter choice (0-{_queueLabels.Count}): ");

            var input = Console.ReadLine()?.Trim();

            if (input == "0")
            {
                Console.WriteLine("Exiting DLQ Manager.");
                break;
            }

            if (!int.TryParse(input, out int choice) || choice < 1 || choice > _queueLabels.Count)
            {
                Console.WriteLine($"Invalid choice. Please enter 0-{_queueLabels.Count}.");
                continue;
            }

            switch (choice)
            {
                case 1:
                    var smsSendService = _sp.GetRequiredService<ISmsSendQueueService>();
                    await smsSendService.RunMenuAsync();
                    break;

                default:
                    Console.WriteLine();
                    Console.WriteLine($"Queue '{_queueLabels[choice - 1]}' is not yet implemented.");
                    break;
            }
        }

        return 0;
    }
}
