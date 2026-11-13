using ProyectoFinal.Models;

namespace ProyectoFinal.Services;

public interface IAnalyticsService
{
    Task<AnalyticsDashboardViewModel> ObtenerAsync(DateOnly desde, DateOnly hasta, int umbralStock);
}
