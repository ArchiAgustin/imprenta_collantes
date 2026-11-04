namespace ProyectoFinal.Models;

public static class EstadoPedido
{
    public const string EnCola = "En cola";
    public const string EnProduccion = "En impresión / acabado";
    public const string ListoParaRecojo = "Listo para recojo";
    public const string Entregado = "Entregado";
    public const string Cancelado = "Cancelado";

    public static readonly IReadOnlyList<string> FlujoProduccion =
        new[] { EnCola, EnProduccion, ListoParaRecojo, Entregado };
}
