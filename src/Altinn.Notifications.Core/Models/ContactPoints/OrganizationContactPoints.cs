﻿namespace Altinn.Notifications.Core.Models.ContactPoints;

/// <summary>
/// Class describing the contact points for an organization
/// </summary>
public class OrganizationContactPoints
{
    /// <summary>
    /// Gets or sets the organization number for the organization
    /// </summary>
    public string OrganizationNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a list of official mobile numbers
    /// </summary>
    public List<string> MobileNumberList { get; set; } = [];

    /// <summary>
    /// Gets or sets a list of official email addresses
    /// </summary>
    public List<string> EmailList { get; set; } = [];
}
