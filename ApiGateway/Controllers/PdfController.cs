using CommonModels.Models; 
using MassTransit;
using MassTransit.Transports;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("api/pdf")]
    public class PdfController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly string _storagePath;
        private readonly IPublishEndpoint _publishEndpoint; //  шина данных
        public PdfController(AppDbContext context, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            // Папка на сервере, где будут физически храниться загруженные PDF
            _storagePath = Path.Combine(AppContext.BaseDirectory, "UploadedFiles");

            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }
        }

        // ЗАГРУЗКА PDF (POST: api/pdf/upload)
        [HttpPost("upload")]
        public async Task<IActionResult> UploadPdf(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Файл не выбран или пуст.");

            if (Path.GetExtension(file.FileName).ToLower() != ".pdf")
                return BadRequest("Допускаются только файлы формата PDF.");

            // Генерируем уникальный GUID для документа заранее (преимущество Guid над int!)
            var documentId = Guid.NewGuid();
            var uniqueFileName = $"{documentId}.pdf";
            var fullPath = Path.Combine(_storagePath, uniqueFileName);

            // Сохраняем файл на диск
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Создаем запись для базы данных со статусом Pending
            var pdfDocument = new Document
            {
                Id = documentId,
                FileName = file.FileName,
                FilePath = fullPath,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Documents.Add(pdfDocument);
            await _context.SaveChangesAsync();

            await _publishEndpoint.Publish(new PdfUploadedEvent
            {
                DocumentId = documentId,
                FilePath = fullPath
            });

            return Ok(new { message = "Файл успешно загружен и добавлен в очередь на обработку", documentId });
        }

        //ПОЛУЧЕНИЕ СПИСКА PDF (GET: api/pdf)
        [HttpGet]
        public async Task<IActionResult> GetDocuments()
        {
            var documents = await _context.Documents
                .Select(d => new
                {
                    d.Id,
                    d.FileName,
                    d.Status,
                    d.CreatedAt,
                    d.ProcessedAt
                })
                .ToListAsync();

            return Ok(documents);
        }

        // ПОЛУЧЕНИЕ ТЕКСТОВОГО СОДЕРЖИМОГО PDF (GET: api/pdf/{id}/text)
        [HttpGet("{id}/text")]
        public async Task<IActionResult> GetDocumentText(Guid id)
        {
            // Запрашиваем из базы только нужные поля для оптимизации трафика
            var document = await _context.Documents
                .Select(d => new
                {
                    d.Id,
                    d.Status,
                    d.ExtractedText
                })
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return NotFound(new { message = "Документ с указанным идентификатором не найден." });
            }

            if (document.Status == "Pending" || document.Status == "Processing")
            {
                return Ok(new
                {
                    status = document.Status,
                    message = "Файл находится в очереди на обработку. Пожалуйста, повторите запрос позже."
                });
            }

            // Если воркер завершил обработку со сбоем (например, PDF поврежден)
            if (document.Status == "Failed")
            {
                return BadRequest(new
                {
                    status = document.Status,
                    message = "Не удалось извлечь текст из данного файла.",
                    error = document.ExtractedText // В случае ошибки здесь лежит текст исключения
                });
            }

            return Ok(new
            {
                id = document.Id,
                status = document.Status,
                text = document.ExtractedText
            });
        }

    }
}
