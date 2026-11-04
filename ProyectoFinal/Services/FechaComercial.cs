namespace ProyectoFinal.Services;

public static class FechaComercial
{
    // Peru does not observe daylight saving time; keep the business date
    // independent from the server machine's local time zone.
    public static DateOnly HoyPeru() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
    public static DateTimeOffset EnPeru(DateTimeOffset fecha) => fecha.ToOffset(TimeSpan.FromHours(-5));
}
