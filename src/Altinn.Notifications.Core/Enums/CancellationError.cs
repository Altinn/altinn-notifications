namespace Altinn.Notifications.Core.Enums
{
    /// <summary>
    /// Enum for the different types of errors that can occur when cancelling an order
    /// </summary>
    public enum CancellationError
    {
        /// <summary>
        /// Order was not found
        /// </summary>
        OrderNotFound, 

        /// <summary>
        /// Order was found but processing had already started
        /// </summary>
        CancellationProhibited
    }
}
