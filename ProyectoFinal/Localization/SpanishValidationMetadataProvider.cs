using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace ProyectoFinal.Localization;

// Supply defaults for annotations without replacing messages written for a specific workflow.
public sealed class SpanishValidationMetadataProvider : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var attribute in context.Attributes.OfType<ValidationAttribute>())
        {
            if (attribute.ErrorMessageResourceName is not null || !UsesDefaultMessage(attribute))
                continue;

            attribute.ErrorMessage = attribute switch
            {
                RequiredAttribute => "El campo {0} es obligatorio.",
                StringLengthAttribute length when length.MinimumLength > 0 => "El campo {0} debe tener entre {2} y {1} caracteres.",
                StringLengthAttribute => "El campo {0} no puede superar los {1} caracteres.",
                MaxLengthAttribute => "El campo {0} no puede superar los {1} caracteres.",
                MinLengthAttribute => "El campo {0} debe tener al menos {1} caracteres.",
                RangeAttribute => "El campo {0} debe estar entre {1} y {2}.",
                EmailAddressAttribute => "Ingresa un correo electrónico válido.",
                PhoneAttribute => "Ingresa un teléfono válido.",
                CompareAttribute => "Los campos {0} y {1} deben coincidir.",
                RegularExpressionAttribute => "Revisa el formato del campo {0}.",
                UrlAttribute => "Ingresa una dirección web válida.",
                _ => null
            };
        }
    }

    private static bool UsesDefaultMessage(ValidationAttribute attribute) => attribute switch
    {
        RequiredAttribute => attribute.ErrorMessage == new RequiredAttribute().ErrorMessage,
        StringLengthAttribute => attribute.ErrorMessage == new StringLengthAttribute(1).ErrorMessage,
        MaxLengthAttribute => attribute.ErrorMessage == new MaxLengthAttribute(1).ErrorMessage,
        MinLengthAttribute => attribute.ErrorMessage == new MinLengthAttribute(1).ErrorMessage,
        RangeAttribute => attribute.ErrorMessage == new RangeAttribute(1, 2).ErrorMessage,
        EmailAddressAttribute => attribute.ErrorMessage == new EmailAddressAttribute().ErrorMessage,
        PhoneAttribute => attribute.ErrorMessage == new PhoneAttribute().ErrorMessage,
        CompareAttribute => attribute.ErrorMessage == new CompareAttribute("OtroCampo").ErrorMessage,
        RegularExpressionAttribute => attribute.ErrorMessage == new RegularExpressionAttribute(".*").ErrorMessage,
        UrlAttribute => attribute.ErrorMessage == new UrlAttribute().ErrorMessage,
        _ => false
    };
}
