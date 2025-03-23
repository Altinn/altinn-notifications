﻿using Altinn.Notifications.Models;
using Altinn.Notifications.Validators.Email;
using Altinn.Notifications.Validators.Sms;
using FluentValidation;

namespace Altinn.Notifications.Validators
{
    /// <summary>
    /// Represents validation logic for the recipient types associated with request model.
    /// </summary>
    public class RecipientSpecificationValidator : AbstractValidator<RecipientSpecificationExt>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecipientSpecificationValidator"/> class.
        /// </summary>
        public RecipientSpecificationValidator()
        {
            RuleFor(specification => specification)
                .NotNull()
                .Must(HaveOneSetRecipientOnly)
                .WithMessage("Recipient specification cannot be null.");
                
            // todo: do i need to null check before setting a validator?
            RuleFor(specification => specification.RecipientEmail)
               .SetValidator(validator: new RecipientEmailValidator());

            RuleFor(specification => specification.RecipientSms)
                .SetValidator(validator: new RecipientSmsValidator());

            RuleFor(specification => specification.RecipientPerson)
                .SetValidator(validator: new RecipientPersonValidator());

            RuleFor(specification => specification.RecipientOrganization)
                .SetValidator(validator: new RecipientOrganizationValidator());
        }

        /// <summary>
        /// Checks if only one recipient is set.
        /// </summary>
        /// <param name="specification">Object containing four possible recipient types</param>
        /// <returns></returns>
        private static bool HaveOneSetRecipientOnly(RecipientSpecificationExt specification)
        {
            var numberOfSetRecipients = 0;
            numberOfSetRecipients += specification.RecipientEmail != null ? 1 : 0;
            numberOfSetRecipients += specification.RecipientSms != null ? 1 : 0;
            numberOfSetRecipients += specification.RecipientPerson != null ? 1 : 0;
            numberOfSetRecipients += specification.RecipientOrganization != null ? 1 : 0;

            return numberOfSetRecipients == 1;
        }
    }
}
