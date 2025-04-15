using Altinn.Notifications.Configuration;
using Altinn.Notifications.Core.Models.Delivery;
using Altinn.Notifications.Core.Services.Interfaces;
using Altinn.Notifications.Core.Shared;
using Altinn.Notifications.Extensions;
using Altinn.Notifications.Mappers;
using Altinn.Notifications.Models.Delivery;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace Altinn.Notifications.Controllers;

/// <summary>
/// API controller for managing notification shipments and their delivery status.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ShipmentController"/> provides endpoints for retrieving information about 
/// notification shipments sent through the Altinn Notifications system, including delivery status
/// for various communication channels (SMS, email).
/// </para>
/// <para>
/// All endpoints require valid authentication and authorization according to the platform's 
/// security policies.
/// </para>
/// </remarks>
[ApiController]
[Route("notifications/api/v1/shipment")]
[SwaggerResponse(401, "Caller is unauthorized")]
[SwaggerResponse(403, "Caller is not authorized to access the requested resource")]
[Authorize(Policy = AuthorizationConstants.POLICY_CREATE_SCOPE_OR_PLATFORM_ACCESS)]
public class ShipmentController : ControllerBase
{
    private readonly IShipmentDeliveryService _shipmentDeliveryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipmentController"/> class.
    /// </summary>
    public ShipmentController(IShipmentDeliveryService shipmentDeliveryService)
    {
        _shipmentDeliveryService = shipmentDeliveryService;
    }

    /// <summary>
    /// Retrieve the delivery mainfest for a specific notification shipment.
    /// </summary>
    /// <param name="id">The unique identifier (GUID) of the shipment.</param>
    /// <returns>
    /// Returns an <see cref="ActionResult{T}"/> containing the <see cref="ShipmentDeliveryManifestExt"/> if found,
    /// or a 404 Not Found response if no shipment with the specified identifier exists.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Use this endpoint to audit delivery outcomes, monitor notification workflows, or reconcile shipment status
    /// with external systems via the sender's reference.
    /// </para>
    /// </remarks>
    [HttpGet]
    [Route("{id}")]
    [Produces("application/json")]
    [SwaggerResponse(404, "No shipment with the provided identifier was found")]
    [SwaggerResponse(200, "The shipment matching the provided identifier was retrieved successfully", typeof(ShipmentDeliveryManifestExt))]
    public async Task<ActionResult<ShipmentDeliveryManifestExt>> GetById([FromRoute] Guid id)
    {
        string? creatorName = HttpContext.GetOrg();

        if (creatorName == null)
        {
            return Forbid();
        }

        Result<IShipmentDeliveryManifest, ServiceError> result = await _shipmentDeliveryService.GetDeliveryManifest(id, creatorName);

        return result.Match<ActionResult<ShipmentDeliveryManifestExt>>(manifest => Ok(manifest.MapToShipmentDeliveryManifestExt()), error => StatusCode(error.ErrorCode, error.ErrorMessage));
    }
}
