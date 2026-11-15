using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace ProyectoFinal.Localization;

public static class SpanishModelBindingMessages
{
    public static void Configure(DefaultModelBindingMessageProvider messages)
    {
        messages.SetMissingBindRequiredValueAccessor(name => $"El campo {name} es obligatorio.");
        messages.SetMissingKeyOrValueAccessor(() => "Falta una clave o un valor.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "La solicitud debe incluir datos.");
        messages.SetValueMustNotBeNullAccessor(name => $"El campo {name} es obligatorio.");
        messages.SetAttemptedValueIsInvalidAccessor((value, name) => $"El valor ingresado para {name} no es válido.");
        messages.SetUnknownValueIsInvalidAccessor(name => $"El valor ingresado para {name} no es válido.");
        messages.SetValueIsInvalidAccessor(value => "El valor ingresado no es válido.");
        messages.SetValueMustBeANumberAccessor(name => $"Ingresa un número válido para {name}.");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "Ingresa un número válido.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => "El valor ingresado no es válido.");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor ingresado no es válido.");
    }
}
