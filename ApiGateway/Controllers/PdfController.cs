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
            _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "UploadedFiles");

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
    }
}
