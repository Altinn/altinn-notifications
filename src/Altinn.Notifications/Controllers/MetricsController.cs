using System.Text.Json;

using Altinn.Notifications.Core.Models.Metrics;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace Altinn.Notifications.Controllers
{
    /// <summary>
    /// Metrics controller for handling metrics for notifications
    /// </summary>
    [Route("notifications/api/v1/metrics")]
    [ApiController]
    public class MetricsController : Controller
    {
        private readonly IMetricsService _metricsService;

        /// <summary>
        /// Controller for handling metrics for notifications
        /// </summary>
        public MetricsController(IMetricsService metricsService)
        {
            _metricsService = metricsService;
        }

        /// <summary>
        /// Index method
        /// </summary>
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Gets the metrics for the provided month and year
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetMetrics([FromForm] int month, [FromForm] int year)
        {
            MonthlyNotificationMetrics metrics = await _metricsService.GetMonthlyMetrics(month, year);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
            return View("Index", metrics);
        }
    }
}
