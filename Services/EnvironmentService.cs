using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UltraLightApiTester.Models;

namespace UltraLightApiTester.Services
{
    public class EnvironmentService
    {
        private string FilePath => Path.Combine(SettingsService.DataPath, "environments.json");
        private List<EnvironmentConfig> _environments;
        private int _activeIndex;

        public EnvironmentConfig Active
        {
            get
            {
                if (_environments.Count == 0) return new EnvironmentConfig();
                if (_activeIndex < 0 || _activeIndex >= _environments.Count) _activeIndex = 0;
                return _environments[_activeIndex];
            }
        }

        public List<EnvironmentConfig> All => _environments;

        public int ActiveIndex
        {
            get => _activeIndex;
            set { if (value >= 0 && value < _environments.Count) _activeIndex = value; }
        }

        public string ActiveName => _environments.Count > 0 && _activeIndex < _environments.Count
            ? _environments[_activeIndex].Name : "";

        public EnvironmentService()
        {
            _environments = LoadInternal();
            _activeIndex = _environments.Count > 0 ? 0 : -1;
        }

        private List<EnvironmentConfig> LoadInternal()
        {
            if (!File.Exists(FilePath))
            {
                var defaults = CreateDefaults();
                SaveInternal(defaults);
                return defaults;
            }
            try { return JsonStore.Deserialize<EnvironmentConfig>(File.ReadAllText(FilePath)); }
            catch { return new List<EnvironmentConfig>(); }
        }

        private void SaveInternal(List<EnvironmentConfig> envs)
        {
            Directory.CreateDirectory(SettingsService.DataPath);
            File.WriteAllText(FilePath, JsonStore.Serialize(envs));
        }

        public void Save() => SaveInternal(_environments);

        public void SetActive(string name)
        {
            var idx = _environments.FindIndex(e => e.Name == name);
            if (idx >= 0) _activeIndex = idx;
        }

        public void Add(EnvironmentConfig env) { _environments.Add(env); Save(); }
        public void Update(int index, EnvironmentConfig env)
        {
            if (index >= 0 && index < _environments.Count) { _environments[index] = env; Save(); }
        }
        public void Delete(int index)
        {
            if (index >= 0 && index < _environments.Count)
            {
                _environments.RemoveAt(index);
                if (_activeIndex >= _environments.Count) _activeIndex = _environments.Count - 1;
                Save();
            }
        }

        public string ReplaceVariables(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var env = Active;
            return Regex.Replace(input, @"\{\{(\w+)\}\}", match =>
            {
                var key = match.Groups[1].Value;
                return env.Variables.TryGetValue(key, out var val) ? val : match.Value;
            });
        }

        private List<EnvironmentConfig> CreateDefaults()
        {
            return new List<EnvironmentConfig>
            {
                new EnvironmentConfig { Name = "开发环境", Variables = new Dictionary<string, string> {
                    { "baseUrl", "http://localhost:3000" }, { "token", "dev-token-here" } } },
                new EnvironmentConfig { Name = "生产环境", Variables = new Dictionary<string, string> {
                    { "baseUrl", "https://api.example.com" }, { "token", "prod-token-here" } } }
            };
        }
    }
}
