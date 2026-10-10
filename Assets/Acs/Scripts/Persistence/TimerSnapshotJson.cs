using System;
using UnityEngine;
using LumiWorld.Acs.TimeSystem;

namespace LumiWorld.Acs
{
    // Codec only. Call TimerService.Restore to validate before applying; never auto-restore owners.
    public static class TimerSnapshotJson
    {
        public static string Serialize(TimerSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return JsonUtility.ToJson(snapshot, true);
        }
        public static TimerSnapshot Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Missing timer snapshot JSON.", nameof(json));
            var snapshot = JsonUtility.FromJson<TimerSnapshot>(json);
            if (snapshot == null) throw new ArgumentException("Invalid timer snapshot JSON.", nameof(json));
            return snapshot;
        }
    }
}
