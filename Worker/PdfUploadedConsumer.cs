using MassTransit;
using Npgsql; // Для прямой быстрой работы с БД без таскания всего EF-контекста во Worker
using CommonModels.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using UglyToad.PdfPig; 


namespace Worker
{
    public class PdfUploadedConsumer : IConsumer<PdfUploadedEvent>
    {
        private readonly string _connectionString;

        public PdfUploadedConsumer(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task Consume(ConsumeContext<PdfUploadedEvent> context)
        {
            var message = context.Message;

            // 1. Обновляем статус в БД на "Processing"
            await UpdateDocumentStatus(message.DocumentId, "Processing", null);

            try
            {
                // 2. Проверяем, существует ли файл по указанному пути
                if (!File.Exists(message.FilePath))
                {
                    throw new FileNotFoundException($"Файл не найден по пути: {message.FilePath}");
                }

                // 3. Извлекаем текст с помощью PdfPig
                string extractedText = "";
                using (var pdf = PdfDocument.Open(message.FilePath))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        extractedText += page.Text + Environment.NewLine;
                    }
                }

                // 4. Сохраняем результат в БД и ставим статус "Completed"
                await UpdateDocumentStatus(message.DocumentId, "Completed", extractedText);
            }
            catch (Exception ex)
            {
                // 5. В случае ошибки — фиксируем сбой
                await UpdateDocumentStatus(message.DocumentId, "Failed", $"Ошибка обработки: {ex.Message}");
                throw; // Пробрасываем ошибку, чтобы MassTransit зафиксировал сбой сообщения
            }
        }

        // Вспомогательный метод для прямого обновления статуса в PostgreSQL via ADO.NET (быстро и независимо)
        private async Task UpdateDocumentStatus(Guid id, string status, string? text)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = text != null
                ? "UPDATE \"Documents\" SET \"Status\" = @status, \"ExtractedText\" = @text, \"ProcessedAt\" = @now WHERE \"Id\" = @id"
                : "UPDATE \"Documents\" SET \"Status\" = @status WHERE \"Id\" = @id";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@id", id);
            if (text != null)
            {
                command.Parameters.AddWithValue("@text", text);
                command.Parameters.AddWithValue("@now", DateTime.UtcNow);
            }

            await command.ExecuteNonQueryAsync();
        }
    }
}
