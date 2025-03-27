﻿using Altinn.Notifications.Models;
using FluentValidation;
using FluentValidation.Validators;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Class containing validation logic for the <see cref="DialogportenIdentifiersExt"/> model
    /// </summary>
    internal sealed class DialogportenIdentifiersValidator : AbstractValidator<DialogportenIdentifiersExt?>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DialogportenIdentifiersValidator"/> class.
        /// </summary>
        public DialogportenIdentifiersValidator()
        {
        }
    }
}
