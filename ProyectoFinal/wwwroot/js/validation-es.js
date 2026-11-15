// Fallbacks for rules without a server-supplied data-val message.
$.extend($.validator.messages, {
    required: "Este campo es obligatorio.",
    remote: "Revisa este valor.",
    email: "Ingresa un correo electrónico válido.",
    url: "Ingresa una dirección web válida.",
    date: "Ingresa una fecha válida.",
    dateISO: "Ingresa una fecha válida.",
    number: "Ingresa un número válido.",
    digits: "Ingresa solo dígitos.",
    equalTo: "Los valores deben coincidir.",
    maxlength: $.validator.format("No ingreses más de {0} caracteres."),
    minlength: $.validator.format("Ingresa al menos {0} caracteres."),
    rangelength: $.validator.format("Ingresa entre {0} y {1} caracteres."),
    range: $.validator.format("Ingresa un valor entre {0} y {1}."),
    max: $.validator.format("Ingresa un valor menor o igual a {0}."),
    min: $.validator.format("Ingresa un valor mayor o igual a {0}.")
});
