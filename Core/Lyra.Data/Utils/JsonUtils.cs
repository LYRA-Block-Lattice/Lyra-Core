using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Lyra.Core.Blocks.Block;

namespace Lyra.Data.Utils
{
    public class JsonUtils
    {
        public static JsonSerializerSettings Settings { get ; set; }

        static JsonUtils()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CustomResolver(new List<string> { "Hash", "Signature" }),
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",
                Converters = new List<JsonConverter> { new DecimalJsonConverter(), new SortedDictionaryConverter<string, long>() },
            };

            Settings = settings;
        }
    }

    public class SortedDictionaryConverter<TKey, TValue> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IDictionary<TKey, TValue>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var result = new Dictionary<TKey, TValue>();

            // Read the JSON object
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonToken.PropertyName)
                {
                    var key = (TKey)Convert.ChangeType(reader.Value, typeof(TKey));
                    reader.Read();
                    var value = (TValue)serializer.Deserialize(reader, typeof(TValue));
                    result.Add(key, value);
                }
            }

            return result;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var dictionary = (IDictionary<TKey, TValue>)value;

            writer.WriteStartObject();
            foreach (var key in dictionary.Keys.OrderBy(k => k.ToString()))
            {
                writer.WritePropertyName(key.ToString());
                serializer.Serialize(writer, dictionary[key]);
            }
            writer.WriteEndObject();
        }
    }

    class DecimalJsonConverter : JsonConverter
    {
        public DecimalJsonConverter()
        {
        }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(decimal) || objectType == typeof(float) || objectType == typeof(double));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (DecimalJsonConverter.IsWholeValue(value))
            {
                writer.WriteRawValue(JsonConvert.ToString(Convert.ToInt64(value)));
            }
            else
            {
                writer.WriteRawValue(JsonConvert.ToString(value));
            }
        }

        private static bool IsWholeValue(object value)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue == Math.Truncate(decimalValue);
            }
            else if (value is float floatValue)
            {
                return floatValue == Math.Truncate(floatValue);
            }
            else if (value is double doubleValue)
            {
                return doubleValue == Math.Truncate(doubleValue);
            }

            return false;
        }
    }
}
