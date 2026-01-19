using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

/// <summary>
/// Custom ContractResolver để chỉ serialize trường Value của ReactiveValue thay vì toàn bộ object
/// </summary>
public class ReactiveValueContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);
        
        // Kiểm tra nếu type là ReactiveValue<T>
        if (property.PropertyType != null && property.PropertyType.IsGenericType)
        {
            var genericTypeDefinition = property.PropertyType.GetGenericTypeDefinition();
            if (genericTypeDefinition.Name.StartsWith("ReactiveValue"))
            {
                // Tạo custom converter cho ReactiveValue
                property.Converter = new ReactiveValueConverter();
            }
        }
        
        return property;
    }
}

/// <summary>
/// Custom JsonConverter cho ReactiveValue để chỉ serialize trường Value
/// </summary>
public class ReactiveValueConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType.IsGenericType && objectType.GetGenericTypeDefinition().Name.StartsWith("ReactiveValue");
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        // Sử dụng reflection để lấy property Value
        var valueProperty = value.GetType().GetProperty("Value");
        if (valueProperty != null)
        {
            var actualValue = valueProperty.GetValue(value);
            serializer.Serialize(writer, actualValue);
        }
        else
        {
            writer.WriteNull();
        }
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        // Lấy type của generic parameter
        var genericArgument = objectType.GetGenericArguments()[0];
        var value = serializer.Deserialize(reader, genericArgument);
        
        // Tạo instance mới của ReactiveValue<T>
        return Activator.CreateInstance(objectType, value);
    }
}
