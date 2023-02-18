using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.Utils
{
    internal class JsonUtils
    {
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

}
