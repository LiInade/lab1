using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace ReflectionCsvBenchmark;

public static class CsvReflectionSerializer
{
    static readonly ConcurrentDictionary<Type, MemberInfo[]> MembersCache = new();

    public static string Serialize<T>(T obj, bool includeHeader = true)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var type = typeof(T);
        var members = GetSerializableMembers(type);

        var sb = new StringBuilder();

        if (includeHeader)
        {
            for (int i = 0; i < members.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsv(members[i].Name));
            }
            sb.AppendLine();
        }

        for (int i = 0; i < members.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var value = GetValue(members[i], obj);
            sb.Append(EscapeCsv(ToFlatString(value)));
        }

        return sb.ToString();
    }

    public static T Deserialize<T>(string csv) where T : new()
    {
        if (csv == null) throw new ArgumentNullException(nameof(csv));

        var lines = SplitLines(csv);
        if (lines.Count == 0) throw new ArgumentException("CSV пустой");

        var type = typeof(T);
        var members = GetSerializableMembers(type);

        int dataLineIndex = 0;
        List<string> values;

        if (lines.Count >= 2)
        {
            var header = ParseCsvLine(lines[0]);
            var data = ParseCsvLine(lines[1]);

            if (header.Count == members.Length)
            {
                values = data;
                dataLineIndex = 1;
            }
            else
            {
                values = ParseCsvLine(lines[0]);
            }
        }
        else
        {
            values = ParseCsvLine(lines[0]);
        }

        var obj = new T();

        var count = Math.Min(members.Length, values.Count);
        for (int i = 0; i < count; i++)
        {
            var memberType = GetMemberType(members[i]);
            var parsedValue = FromFlatString(values[i], memberType);
            SetValue(members[i], obj, parsedValue);
        }

        return obj;
    }

    static MemberInfo[] GetSerializableMembers(Type type)
    {
        return MembersCache.GetOrAdd(type, t =>
        {
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => !f.IsStatic)
                .Cast<MemberInfo>();

            var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.CanRead && p.CanWrite)
                .Cast<MemberInfo>();

            return fields.Concat(props).OrderBy(m => m.Name, StringComparer.Ordinal).ToArray();
        });
    }

    static object? GetValue(MemberInfo member, object obj)
    {
        return member switch
        {
            FieldInfo f => f.GetValue(obj),
            PropertyInfo p => p.GetValue(obj),
            _ => null
        };
    }

    static void SetValue(MemberInfo member, object obj, object? value)
    {
        switch (member)
        {
            case FieldInfo f:
                f.SetValue(obj, value);
                break;
            case PropertyInfo p:
                p.SetValue(obj, value);
                break;
        }
    }

    static Type GetMemberType(MemberInfo member)
    {
        return member switch
        {
            FieldInfo f => f.FieldType,
            PropertyInfo p => p.PropertyType,
            _ => typeof(string)
        };
    }

    static string ToFlatString(object? value)
    {
        if (value == null) return string.Empty;

        if (value is string s) return s;

        var type = value.GetType();

        if (type.IsArray)
        {
            var arr = (Array)value;
            var parts = new string[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                var el = arr.GetValue(i);
                parts[i] = Convert.ToString(el, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            return string.Join(';', parts);
        }

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

        return value.ToString() ?? string.Empty;
    }

    static object? FromFlatString(string text, Type targetType)
    {
        if (targetType == typeof(string)) return text;

        if (string.IsNullOrEmpty(text))
        {
            if (targetType.IsValueType) return Activator.CreateInstance(targetType);
            return null;
        }

        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType() ?? typeof(string);
            var parts = text.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var arr = Array.CreateInstance(elementType, parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                var el = FromFlatString(parts[i], elementType);
                arr.SetValue(el, i);
            }
            return arr;
        }

        if (targetType.IsEnum)
            return Enum.Parse(targetType, text, ignoreCase: true);

        if (targetType == typeof(int))
            return int.Parse(text, CultureInfo.InvariantCulture);
        if (targetType == typeof(long))
            return long.Parse(text, CultureInfo.InvariantCulture);
        if (targetType == typeof(double))
            return double.Parse(text, CultureInfo.InvariantCulture);
        if (targetType == typeof(float))
            return float.Parse(text, CultureInfo.InvariantCulture);
        if (targetType == typeof(bool))
            return bool.Parse(text);

        return Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
    }

    static string EscapeCsv(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == ',')
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        result.Add(sb.ToString());
        return result;
    }

    static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        using var sr = new StringReader(text);
        while (true)
        {
            var line = sr.ReadLine();
            if (line == null) break;
            if (line.Length == 0) continue;
            lines.Add(line);
        }
        return lines;
    }
}
