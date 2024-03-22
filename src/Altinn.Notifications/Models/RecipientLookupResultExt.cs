﻿using System.Text.Json.Serialization;

namespace Altinn.Notifications.Models;

/// <summary>
/// Class describing a summary of recipient lookup for a notification order
/// </summary>
public class RecipientLookupResultExt
{
    /// <summary>
    /// The lookup status
    /// </summary>
    [JsonPropertyName("status")]
    public RecipientLookupStatusExt Status { get; set; }

    /// <summary>
    /// The lookup result
    /// </summary>
    [JsonPropertyName("lookupResult")]
    public RecipientLookupExt LookupResult { get; set; } = new();
}

/// <summary>
/// Enum describing the success rate for recipient lookup
/// </summary>
public enum RecipientLookupStatusExt
{
    /// <summary>
    /// The recipient lookup was successful for all recipients
    /// </summary>
    Success,

    /// <summary>
    /// The recipient lookup was successful for some recipients
    /// </summary>
    PartialSuccess,

    /// <summary>
    /// The recipient lookup failed for all recipients
    /// </summary>
    Failed
}

/// <summary>
/// Class describing the lookup result for a set of recipients
/// </summary>
public class RecipientLookupExt
{
    /// <summary>
    /// List of id numbers for the recipients that are reserved 
    /// </summary>
    public List<string>? IsReserved { get; set; }

    /// <summary>
    /// List of id numbers for the recipients where no contact points were identified
    /// </summary>
    public List<string>? MissingContact { get; set; }
}
