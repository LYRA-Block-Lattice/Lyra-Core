using Lyra.Core.Blocks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                NullValueHandling = NullValueHandling.Ignore,  // so for new db version, keep the newly added fields default null.
                
                Converters = new List<JsonConverter> { new DecimalJsonConverter(), new BlockDictionaryConverter(), new BlockTagsConverter() },
            };

            Settings = settings;
        }
    }

    // per db ver, exclude newly add fields for lower db version

    public class CustomResolver : DefaultContractResolver
    {
        // Define a list of fields to exclude
        private readonly List<string> _excludedProperties;

        public CustomResolver(List<string> excludedProperties)
        {
            _excludedProperties = excludedProperties;
        }

        protected override IList<Newtonsoft.Json.Serialization.JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            //// type is block. block has a version.
            //// if block's version < current version, there is a exclution fields list.
            //// so for block's ver (exclude) to current ver (include), call block to get the exclution list.
            //if (type.IsSubclassOf(typeof(Block)))
            //{
            //    var currentType = type;
            //    while (currentType != null)
            //    {
            //        // get the static method with the given name from the current type
            //        MethodInfo method = currentType.GetMethod("FieldsAddedForVer", BindingFlags.Static | BindingFlags.Public);
            //        if (method != null)
            //        {
            //            // invoke the static method
            //            var list = method.Invoke(null, null);
            //        }
            //        currentType = currentType.BaseType;
            //    }

            //    MethodInfo methodInfo = type.GetMethod("FieldsAddedForVer", BindingFlags.Public | BindingFlags.Static);

            //    if (methodInfo != null)
            //    {
            //        methodInfo.Invoke(null, null);
            //    }
            //    var exclude = Block.FieldsAddedForVer(type, Block.DatabaseVersion);
            //}

            // Get all the properties
            IList<Newtonsoft.Json.Serialization.JsonProperty> properties = base.CreateProperties(type, memberSerialization);

            // Return only the properties that are not in the exclude list
            return properties.Where(p => !_excludedProperties.Contains(p.PropertyName))
                .OrderBy(p => p.PropertyName, StringComparer.Ordinal)
                .ToList();
        }
    }

    public class BlockDictionaryConverter : JsonConverter<Dictionary<string, long>>
    {
        public override Dictionary<string, long>? ReadJson(JsonReader reader, Type objectType, Dictionary<string, long>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, Dictionary<string, long>? value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            foreach (var key in value.Keys.OrderBy(a => a, StringComparer.Ordinal))
            {
                writer.WritePropertyName(key);
                serializer.Serialize(writer, value[key]);
            }
            writer.WriteEndObject();
        }
    }

    public class BlockTagsConverter : JsonConverter<Dictionary<string, string>>
    {
        public override Dictionary<string, string>? ReadJson(JsonReader reader, Type objectType, Dictionary<string, string>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, Dictionary<string, string>? value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            foreach (var key in value.Keys.OrderBy(a => a, StringComparer.Ordinal))
            {
                writer.WritePropertyName(key);
                serializer.Serialize(writer, value[key]);
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
