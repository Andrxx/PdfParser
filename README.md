# Система обработки PDF-документов

Микросервисная система (ASP.NET Core Web API + Worker Service) для асинхронного извлечения текста из PDF-файлов с использованием PostgreSQL и RabbitMQ (MassTransit).

## Быстрый запуск всей системы
Выполните в корневом каталоге проекта команду:
```bash
docker compose up --build -d
```

## Тестирование эндпоинтов
- **Интерфейс API (Scalar UI):** `http://localhost:5081/scalar/v1`
- **Панель RabbitMQ:** `http://localhost:15672` (логин/пароль: `guest`/`guest`)
