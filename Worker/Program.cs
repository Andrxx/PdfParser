//using MassTransit;
//using Worker;

//var builder = Host.CreateApplicationBuilder(args);
//builder.Services.AddHostedService<PdfBackgroundWorker>();

//var host = builder.Build();
//host.Run();


using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMassTransit(x =>
{
    // Регистрируем наш обработчик
    x.AddConsumer<PdfUploadedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        // Автоматически настраивает эндпоинты для всех зарегистрированных Consumer'ов
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
