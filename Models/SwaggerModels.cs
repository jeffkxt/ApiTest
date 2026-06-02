using System.Collections.Generic;

namespace UltraLightApiTester.Models
{
    public class SwaggerEndpoint
    {
        public string Path { get; set; } = "";
        public string Method { get; set; } = "";
        public string Summary { get; set; } = "";
        public List<SwaggerParameter> Parameters { get; set; } = new List<SwaggerParameter>();
        public string RequestBodyMediaType { get; set; } = "";
        public string RequestBodyExample { get; set; } = "";
    }

    public class SwaggerParameter
    {
        public string Name { get; set; } = "";
        public string In { get; set; } = "";
        public bool Required { get; set; }
        public string Type { get; set; } = "";
    }
}
