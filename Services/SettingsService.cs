using System;
using System.IO;
using UltraLightApiTester.Models;

namespace UltraLightApiTester.Services
{
    public static class SettingsService
    {
        private static SettingsConfig _config;
        private static readonly string SettingsDir;
        private static readonly string SettingsFile;

        static SettingsService()
        {
            SettingsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setting");
            SettingsFile = Path.Combine(SettingsDir, "settings.json");
            Load();
        }

        public static int TimeoutSeconds
        {
            get => _config.TimeoutSeconds;
            set { _config.TimeoutSeconds = value; Save(); }
        }

        public static string DataPath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_config.DataPath))
                    return Path.Combine(SettingsDir, "data");
                return _config.DataPath;
            }
            set { _config.DataPath = value; Save(); }
        }

        public static string DefaultDataPath => Path.Combine(SettingsDir, "data");

        public static string GetSettingsDir() => SettingsDir;

        private static void Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var list = JsonStore.Deserialize<SettingsConfig>(json);
                    _config = list.Count > 0 ? list[0] : new SettingsConfig();
                }
                else
                {
                    _config = new SettingsConfig();
                    Directory.CreateDirectory(SettingsDir);
                    Save();
                }
            }
            catch
            {
                _config = new SettingsConfig();
            }
            // ensure data directory exists
            try { Directory.CreateDirectory(DataPath); } catch { }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var list = new System.Collections.Generic.List<SettingsConfig> { _config };
                File.WriteAllText(SettingsFile, JsonStore.Serialize(list));
            }
            catch { }
        }
    }
}
