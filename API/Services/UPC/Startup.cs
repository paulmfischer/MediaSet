namespace Services.UPC.Upcitemdb;

public static class Startup
{
    public static void SetupUPCServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IUPCService, UpcitemdbService>(client =>
        {
            client.BaseAddress = new Uri(configuration["UpcitemdbUrl"]);
        });
    }
}