using System.Text.Json;
using AITriage.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.Configure<WorkerOptions>(options =>
{
    options.Serializer = new Azure.Core.Serialization.JsonObjectSerializer(
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ITopDeskService, TopDeskService>();
builder.Services.AddSingleton<IAITriageService, AITriageService>();

builder.Build().Run();
