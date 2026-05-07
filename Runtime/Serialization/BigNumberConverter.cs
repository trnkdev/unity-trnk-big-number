#if NEWTONSOFT_JSON
using System;
using Newtonsoft.Json;

namespace NekoBigNum
{
    /// <summary>
    /// Compact JSON converter for <see cref="BigNumber"/>. Serializes as <c>{"m":1.5,"e":30}</c>.
    /// Reading also accepts a bare number (e.g. <c>1500000</c>) for convenience and migration.
    /// </summary>
    /// <remarks>
    /// To enable, add the <c>NEWTONSOFT_JSON</c> scripting define symbol after installing
    /// <c>com.unity.nuget.newtonsoft-json</c>. NekoBigNum compiles fine without it.
    /// Apply via <c>[JsonConverter(typeof(BigNumberConverter))]</c> on a field, or register globally
    /// in your <c>JsonSerializerSettings.Converters</c>.
    /// </remarks>
    public class BigNumberConverter : JsonConverter<BigNumber>
    {
        public override void WriteJson(JsonWriter writer, BigNumber value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("m");
            writer.WriteValue(value.Mantissa);
            writer.WritePropertyName("e");
            writer.WriteValue(value.Exponent);
            writer.WriteEndObject();
        }

        public override BigNumber ReadJson(
            JsonReader reader,
            Type objectType,
            BigNumber existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                    return BigNumber.Zero;

                case JsonToken.Integer:
                case JsonToken.Float:
                    return new BigNumber(Convert.ToDouble(reader.Value));

                case JsonToken.String:
                    {
                        string s = (string)reader.Value;
                        if (BigNumber.TryParse(s, out BigNumber parsed)) return parsed;
                        throw new JsonSerializationException($"Could not parse '{s}' as BigNumber.");
                    }

                case JsonToken.StartObject:
                    return ReadObject(reader);

                default:
                    throw new JsonSerializationException(
                        $"Unexpected token {reader.TokenType} when parsing BigNumber.");
            }
        }

        private static BigNumber ReadObject(JsonReader reader)
        {
            double mantissa = 0.0;
            long exponent = 0L;

            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName) continue;

                string propertyName = (string)reader.Value;
                if (!reader.Read()) break;

                switch (propertyName)
                {
                    case "m":
                    case "mantissa":
                        mantissa = Convert.ToDouble(reader.Value);
                        break;
                    case "e":
                    case "exponent":
                        exponent = Convert.ToInt64(reader.Value);
                        break;
                }
            }

            return new BigNumber(mantissa, exponent);
        }
    }
}
#endif
