using System;

namespace UltraLightApiTester.Models
{
    public class SavedRequest
    {
        // ── Request ──
        public string Name { get; set; } = "";
        public string Group { get; set; } = "";
        public string Method { get; set; } = "GET";
        public string Url { get; set; } = "";
        public string Headers { get; set; } = "";
        public string Body { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string SourceUrl { get; set; } = "";

        // ── Response (last send result for this interface) ──
        public int ResponseStatusCode { get; set; }
        public string ResponseHeaders { get; set; } = "";
        public string ResponseBody { get; set; } = "";
        public long ResponseTimeMs { get; set; }
    }
}
