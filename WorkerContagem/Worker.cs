using System.Text;
using System.Text.Json;
using Microsoft.Azure.ServiceBus;
using WorkerContagem.Data;
using WorkerContagem.Models;

namespace WorkerContagem;

public class Worker : IHostedService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly ContagemRepository _repository;
    private readonly string _queue;
    private readonly QueueClient _client;

    public Worker(ILogger<Worker> logger,
        IConfiguration configuration,
        ContagemRepository repository)
    {
        _logger = logger;
        _configuration = configuration;
        _repository = repository;

        _queue = _configuration["AzureServiceBus:Queue"];
        _client = new (
            _configuration["AzureServiceBus:ConnectionString"],
            _queue, ReceiveMode.ReceiveAndDelete);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Iniciando o processamento de mensagens...");
        _logger.LogInformation($"Queue = {_queue}");

        _client.RegisterMessageHandler(
            async (message, stoppingToken) =>
            {
                await ProcessarResultado(message);
            }
            ,
            new MessageHandlerOptions(
                async (e) =>
                {
                    await ProcessarErro(e);
                }
            )
        );

        _logger.LogInformation("Aguardando mensagens...");

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken _)
    {
        await _client.CloseAsync();
        _logger.LogInformation(
            "Conexao com o Azure Service Bus fechada!");
    }

    private Task ProcessarResultado(Message message)
    {
        var dados = Encoding.UTF8.GetString(message.Body);
        _logger.LogInformation(
            $"[{_queue} | Nova mensagem] " + dados);

        ResultadoContador? resultado;            
        try
        {
            resultado = JsonSerializer.Deserialize<ResultadoContador>(dados,
                new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch
        {
            _logger.LogError("Dados inválidos para o Resultado");
            resultado = null;
        }

        if (resultado is not null)
        {
            try
            {
                _repository.Save(resultado);
                _logger.LogInformation("Resultado registrado com sucesso!");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro durante a gravação: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    private Task ProcessarErro(ExceptionReceivedEventArgs e)
    {
        _logger.LogError("[Falha] " +
            e.Exception.GetType().FullName + " " +
            e.Exception.Message);
        return Task.CompletedTask;
    }
}