using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Worker;

var builder = Host.CreateApplicationBuilder(args);
//ПРИНУДИТЕЛЬНО РЕГИСТРИРУЕМ КОНФИГУРАЦИЮ (Решает проблему инициализации фабрики)
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PdfUploadedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {

        string host = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq";
        string user = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        string pass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(host, "/", h =>
        {
            h.Username(user);
            h.Password(pass);
        });

        // Автоматически связывает Consumer с очередью в RabbitMQ
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
