namespace ProyectoFinal.Services;

public sealed record UbigeoHijo(string codigo, string nombre);

public interface IUbigeoConsultaService
{
    Task<IReadOnlyList<UbigeoHijo>?> HijosAsync(string parent);
}
