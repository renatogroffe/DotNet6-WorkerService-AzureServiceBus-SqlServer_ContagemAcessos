using WorkerContagem;
using WorkerContagem.Data;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ContagemRepository>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();