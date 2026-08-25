using System.Collections.Generic;

namespace ISRO_HACKATHON.Models
{
    public class PythonDetectionResponse
    {
        public bool Success { get; set; }

        public string? Error { get; set; }

        public List<DetectionResult>? Detections { get; set; }
    }
}