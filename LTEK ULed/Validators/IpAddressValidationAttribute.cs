using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LTEK_ULed.Validators
{
    public sealed class IpAddressValidationAttribute() : ValidationAttribute(DefaultErrorMessage)
    {
        private static readonly string DefaultErrorMessage = "Please enter a valid Ip address";

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string ip)
                return new ValidationResult(ErrorMessage);

            if (string.IsNullOrWhiteSpace(ip))
                return new ValidationResult(ErrorMessage);

            if(!Regex.IsMatch(ip, "^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
            {
                return new ValidationResult(ErrorMessage);
            }
            return ValidationResult.Success;
        }
    }
}
