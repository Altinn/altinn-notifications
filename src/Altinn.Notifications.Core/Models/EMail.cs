﻿using Altinn.Notifications.Core.Enums;

namespace Altinn.Notifications.Core.Models;

/// <summary>
/// Class representing an email
/// </summary>
public class Email
{
    /// <summary>
    /// Gets the id of the email.
    /// </summary>
    public string Id { get; internal set; }

    /// <summary>
    /// Gets the subject of the email.
    /// </summary>
    public string Subject { get; internal set; }

    /// <summary>
    /// Gets the body of the email.
    /// </summary>
    public string Body { get; internal set; }

    /// <summary>
    /// Gets the to fromAdress of the email.
    /// </summary>
    public string FromAddress { get; internal set; }

    /// <summary>
    /// Gets the to adress of the email.
    /// </summary>
    public string ToAddress { get; internal set; }

    /// <summary>
    /// Gets the content type of the email.
    /// </summary>
    public EmailContentType ContentType { get; internal set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Email"/> class.
    /// </summary>
    public Email(string id, string subject, string body, string fromAddress, string toAddress, EmailContentType contentType)
    {
        Id = id;
        Subject = subject;
        Body = body;
        FromAddress = fromAddress;
        ToAddress = toAddress;
        ContentType = contentType;
    }
}