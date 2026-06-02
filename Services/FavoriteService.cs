using System;
using System.Collections.Generic;
using System.IO;
using UltraLightApiTester.Models;

namespace UltraLightApiTester.Services
{
    public class FavoriteService
    {
        private string FilePath => Path.Combine(SettingsService.DataPath, "favorites.json");

        public List<SavedRequest> Load()
        {
            if (!File.Exists(FilePath))
            {
                var defaults = CreateDefaults();
                Save(defaults);
                return defaults;
            }
            try { return JsonStore.Deserialize<SavedRequest>(File.ReadAllText(FilePath)); }
            catch { return new List<SavedRequest>(); }
        }

        public void Save(List<SavedRequest> items)
        {
            Directory.CreateDirectory(SettingsService.DataPath);
            File.WriteAllText(FilePath, JsonStore.Serialize(items));
        }

        public void Add(SavedRequest request)
        {
            var items = Load();
            request.Timestamp = DateTime.Now;
            items.Add(request);
            Save(items);
        }

        public void Delete(int index)
        {
            var items = Load();
            if (index >= 0 && index < items.Count) { items.RemoveAt(index); Save(items); }
        }

        public void Rename(int index, string newName)
        {
            var items = Load();
            if (index >= 0 && index < items.Count) { items[index].Name = newName; Save(items); }
        }

        public void MoveToGroup(int index, string groupName)
        {
            var items = Load();
            if (index >= 0 && index < items.Count) { items[index].Group = groupName; Save(items); }
        }

        public List<string> GetGroups()
        {
            var items = Load();
            var groups = new List<string>();
            foreach (var item in items)
                if (!string.IsNullOrEmpty(item.Group) && !groups.Contains(item.Group))
                    groups.Add(item.Group);
            groups.Sort();
            return groups;
        }

        private List<SavedRequest> CreateDefaults()
        {
            return new List<SavedRequest>
            {
                new SavedRequest { Name = "示例 - 获取用户列表", Group = "示例", Method = "GET",
                    Url = "https://jsonplaceholder.typicode.com/users",
                    Headers = "User-Agent: UltraApiTester/1.0", Timestamp = DateTime.Now },
                new SavedRequest { Name = "示例 - 创建用户", Group = "示例", Method = "POST",
                    Url = "https://jsonplaceholder.typicode.com/users",
                    Headers = "User-Agent: UltraApiTester/1.0\r\nContent-Type: application/json",
                    Body = "{\r\n  \"name\": \"test\",\r\n  \"email\": \"test@example.com\"\r\n}",
                    Timestamp = DateTime.Now }
            };
        }
    }
}
