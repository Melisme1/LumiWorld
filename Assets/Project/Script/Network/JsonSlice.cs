using System;

/// <summary>
/// Lấy nguyên văn một trường object cấp cao nhất của một chuỗi JSON. Dùng khi save từ server có phần world
/// và economy là object tùy ý, JsonUtility không đọc được nên phải tách chuỗi trước khi ghi ra file.
/// </summary>
public static class JsonSlice
{
    /// <summary>Trả về chuỗi object của trường field, hoặc null nếu không có hoặc không phải object.</summary>
    public static string ObjectField(string json, string field)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field)) return null;

        int depth = 0;
        bool inString = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];

            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"')
            {
                // Ở độ sâu 1 là các khóa của object gốc
                if (depth == 1 && IsObjectValueKey(json, i, field, out int valueStart))
                    return ReadObject(json, valueStart);

                inString = true;
                continue;
            }

            if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']') depth--;
        }

        return null;
    }

    // Kiểm tra chuỗi tại i có phải là khóa "field" và giá trị ngay sau là object; trả về vị trí dấu '{'
    private static bool IsObjectValueKey(string json, int i, string field, out int valueStart)
    {
        valueStart = -1;

        int afterKey = i + 1 + field.Length;
        if (afterKey >= json.Length || string.CompareOrdinal(json, i + 1, field, 0, field.Length) != 0) return false;
        if (json[afterKey] != '"') return false;

        int p = SkipWhitespace(json, afterKey + 1);
        if (p >= json.Length || json[p] != ':') return false;

        p = SkipWhitespace(json, p + 1);
        if (p >= json.Length || json[p] != '{') return false;

        valueStart = p;
        return true;
    }

    private static string ReadObject(string json, int start)
    {
        int depth = 0;
        bool inString = false;

        for (int i = start; i < json.Length; i++)
        {
            char c = json[i];

            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"') inString = true;
            else if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']')
            {
                depth--;
                if (depth == 0) return json.Substring(start, i - start + 1);
            }
        }

        return null;
    }

    private static int SkipWhitespace(string json, int p)
    {
        while (p < json.Length && char.IsWhiteSpace(json[p])) p++;
        return p;
    }
}
