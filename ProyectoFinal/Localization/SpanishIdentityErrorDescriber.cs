using Microsoft.AspNetCore.Identity;

namespace ProyectoFinal.Localization;

public sealed class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };

    public override IdentityError DefaultError() => Error(nameof(DefaultError), "No se pudo completar la operación. Inténtalo de nuevo.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Los datos cambiaron mientras realizabas la operación. Vuelve a intentarlo.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "La contraseña ingresada no es correcta.");
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "El enlace ya no es válido. Solicita uno nuevo.");
    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated), "Ese inicio de sesión ya está asociado con otra cuenta.");
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), "El nombre de usuario no es válido.");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), "Ingresa un correo electrónico válido.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), "Ya existe una cuenta con ese nombre de usuario.");
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), "Ya existe una cuenta con ese correo electrónico.");
    public override IdentityError InvalidRoleName(string? role) => Error(nameof(InvalidRoleName), "El nombre del rol no es válido.");
    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName), "Ya existe un rol con ese nombre.");
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword), "La cuenta ya tiene una contraseña.");
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled), "No se puede bloquear esta cuenta.");
    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), "La cuenta ya tiene ese rol.");
    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), "La cuenta no tiene ese rol.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"La contraseña debe tener al menos {length} caracteres.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "La contraseña debe incluir un carácter especial.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "La contraseña debe incluir un número.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "La contraseña debe incluir una letra minúscula.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "La contraseña debe incluir una letra mayúscula.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"La contraseña debe incluir al menos {uniqueChars} caracteres distintos.");
}
