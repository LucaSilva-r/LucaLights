using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LTEK_ULed.Validators
{
    public sealed class NameValidationAttribute() : ValidationAttribute(DefaultErrorMessage)
    {
        private static readonly string DefaultErrorMessage = "Please enter a valid name";

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string name)
                return new ValidationResult(ErrorMessage);

            if (string.IsNullOrWhiteSpace(name))
                return new ValidationResult(ErrorMessage);

            if (name.Length > 20)
            {
                return new ValidationResult("Names cannot be longer than 20 characters");
            }
            return ValidationResult.Success;
        }
    }
}
