using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Các prefab ô đất, tra theo khóa lúc nạp save. Khóa là GUID của asset prefab nên đổi tên file không làm hỏng save.
/// Tạo bằng Tools > LumiWorld > Xây danh mục save.
/// </summary>
public class TileCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string key;
        public GameObject prefab;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<string, GameObject> byKey;

    public void SetEntries(List<Entry> list)
    {
        entries = list;
        byKey = null;
    }

    public GameObject Find(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        if (byKey == null) Build();
        return byKey.TryGetValue(key, out GameObject prefab) ? prefab : null;
    }

    /// <summary>Khóa của prefab trong danh mục, rỗng nếu chưa có. Dùng lúc chụp save.</summary>
    public string KeyOf(GameObject prefab)
    {
        foreach (Entry entry in entries)
        {
            if (entry.prefab == prefab) return entry.key;
        }
        return "";
    }

    private void Build()
    {
        byKey = new Dictionary<string, GameObject>();
        foreach (Entry entry in entries)
        {
            if (entry.prefab == null || string.IsNullOrEmpty(entry.key)) continue;
            byKey[entry.key] = entry.prefab;
        }
    }
}
