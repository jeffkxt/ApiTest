using System.Collections.Generic;

namespace UltraLightApiTester.Models
{
    public class EnvironmentConfig
    {
        public string Name { get; set; } = "";
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
    }
}
