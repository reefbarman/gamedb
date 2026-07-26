using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace GameDBLibrary
{
    internal static class JsonSerialization
    {
        private static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            LineInfoHandling = LineInfoHandling.Load
        };

        public static object Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            using (var stringReader = new StringReader(json))
            using (var jsonReader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double
            })
            {
                return ToPlainObject(JToken.ReadFrom(jsonReader, LoadSettings));
            }
        }

        public static string Serialize(object value)
        {
            return JsonConvert.SerializeObject(NormalizeForSerialization(value), Formatting.None);
        }

        private static object ToPlainObject(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var dictionary = new Dictionary<string, object>();
                    foreach (var property in token.Children<JProperty>())
                    {
                        dictionary[property.Name] = ToPlainObject(property.Value);
                    }

                    return dictionary;
                case JTokenType.Array:
                    var list = new List<object>();
                    foreach (var item in token.Children())
                    {
                        list.Add(ToPlainObject(item));
                    }

                    return list;
                case JTokenType.Integer:
                    return token.Value<long>();
                case JTokenType.Float:
                    var value = token.Value<double>();
                    if (!NumericValue.TryNormalizeDouble(value, out var normalized))
                    {
                        throw new FormatException("JSON numbers must be finite.");
                    }
                    return normalized;
                case JTokenType.Boolean:
                    return token.Value<bool>();
                case JTokenType.String:
                    return token.Value<string>();
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;
                default:
                    return ((JValue)token).Value;
            }
        }

        private static object NormalizeForSerialization(object value)
        {
            if (value is IDictionary dictionary)
            {
                var normalized = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    normalized[Convert.ToString(entry.Key)] = NormalizeForSerialization(entry.Value);
                }

                return normalized;
            }

            if (value is IList list)
            {
                var normalized = new List<object>(list.Count);
                foreach (var item in list)
                {
                    normalized.Add(NormalizeForSerialization(item));
                }

                return normalized;
            }

            if (value is double doubleValue)
            {
                if (!NumericValue.TryNormalizeDouble(doubleValue, out var normalized))
                {
                    throw new FormatException("JSON numbers must be finite.");
                }

                return normalized;
            }

            if (value is float floatValue)
            {
                if (!NumericValue.TryNormalizeSingle(floatValue, out var normalized))
                {
                    throw new FormatException("JSON numbers must be finite.");
                }

                return normalized;
            }

            return value;
        }
    }
}
