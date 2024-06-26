﻿namespace Altinn.Notifications.Core.Models.SendCondition
{
    /// <summary>
    /// Model describing a condition client error
    /// </summary>
    public class ConditionClientError
    {
        /// <summary>
        /// The result code of the condition check request
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// The error message of the condition check request
        /// </summary>
        public string? Message { get; set; }
    }
}
