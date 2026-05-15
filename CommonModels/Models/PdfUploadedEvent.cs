using System;
using System.Collections.Generic;
using System.Text;

namespace CommonModels.Models
{
    public class PdfUploadedEvent
    {
        public Guid DocumentId { get; set; }
        public string FilePath { get; set; }
    }
}
