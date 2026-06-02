using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltraLightApiTester.Models;

namespace UltraLightApiTester.Services
{
    public class HistoryService
    {
        private const int MaxCount = 100;

        private string FilePath => Path.Combine(SettingsService.DataPath, "history.json");

        public List<SavedRequest> Load()
        {
            if (!File.Exists(FilePath)) return new List<SavedRequest>();
            try { return JsonStore.Deserialize<SavedRequest>(File.ReadAllText(FilePath)); }
            catch { return new List<SavedRequest>(); }
        }

        public void Save(List<SavedRequest> items)
        {
            if (items.Count > MaxCount)
                items = items.OrderByDescending(i => i.Timestamp).Take(MaxCount).ToList();
            Directory.CreateDirectory(SettingsService.DataPath);
            File.WriteAllText(FilePath, JsonStore.Serialize(items));
        }

        public void Add(SavedRequest request)
        {
            var items = Load();
            if (items.Count > 0)
            {
                var last = items[items.Count - 1];
                if (last.Method == request.Method && last.Url == request.Url &&
                    last.Headers == request.Headers && last.Body == request.Body)
                    return;
            }
            request.Timestamp = DateTime.Now;
            items.Add(request);
            Save(items);
        }

        public void Delete(int index)
        {
            var items = Load();
            if (index >= 0 && index < items.Count) { items.RemoveAt(index); Save(items); }
        }

        public void Clear()
        {
            try { File.Delete(FilePath); } catch { }
        }
    }
}
