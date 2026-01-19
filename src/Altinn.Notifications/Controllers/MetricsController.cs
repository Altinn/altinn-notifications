using Altinn.Notifications.Authorization;
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
        /// Initializes a new instance of the <see cref="MetricsController"/> class.
        /// </summary>
        public MetricsController(IMetricsService metricsService)
        {
            _metricsService = metricsService;
        }

        /// <summary>
        /// Presents the initial view of the metrics page
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
            return View("Index", metrics);
        }

        /// <summary>
        /// Endpoint for triggering generation of daily SMS metrics
        /// </summary>
        /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation that returns an <see cref="ActionResult"/>.</returns>
        [HttpPost]
        [Route("sms")]
        [Produces("application/octet-stream")]
        [ServiceFilter(typeof(MetricsApiKeyFilter))]
        public async Task<ActionResult> GetSmsDailyMetrics()
        {
            var data = await _metricsService.GetDailySmsMetrics();

            var response = await _metricsService.GetParquetFile(data);

            try
            {
                Response.Headers["X-File-Hash"] = response.FileHash;
                Response.Headers["X-File-Size"] = response.FileSizeBytes.ToString();
                Response.Headers["X-Total-FileTransfer-Count"] = response.TotalFileTransferCount.ToString();
                Response.Headers["X-Generated-At"] = response.GeneratedAt.ToString("O"); // ISO 8601 format
                Response.Headers["X-Environment"] = response.Environment;
            }
            catch
            {
                await response.FileStream.DisposeAsync();
                throw;
            }

            return File(response.FileStream, "application/octet-stream", response.FileName);
        }        
    }
}
