using TW.Reactive.CustomComponent;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using DataModule;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

public static class DataSerializer
{
    private const int SaltSize = 16;
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    // Settings tạo 1 lần: Newtonsoft cache contract theo resolver, tạo mới mỗi lần là làm lại reflection cho mọi lần load/save.
    private static readonly JsonSerializerSettings ToJsonSettings = CreateSettings(ObjectCreationHandling.Auto);
    private static readonly JsonSerializerSettings FromJsonSettings = CreateSettings(ObjectCreationHandling.Replace);

    private static JsonSerializerSettings CreateSettings(ObjectCreationHandling creationHandling)
    {
        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            ContractResolver = new CustomResolver(),
            ObjectCreationHandling = creationHandling,
        };
        settings.Converters.Add(new ReactiveConverterFactory());
        settings.Converters.Add(new Vector3Converter());
        return settings;
    }

    /// <summary>log = false khi serialize hàng loạt (tool convert level) để không ngộp Console.</summary>
    public static string ToJson(object data)
    {
        return JsonConvert.SerializeObject(data, ToJsonSettings);
    }

    public static T FromJson<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json, FromJsonSettings);
    }

    /// <summary>log = false khi serialize hàng loạt (tool convert level) để không ngộp Console.</summary>
    public static string Serialize(object data)
    {
        return Encrypt(ToJson(data));
    }

    public static T Deserialize<T>(string json)
    {
        return FromJson<T>(Decrypt(json));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] GetKey() => Encoding.UTF8.GetBytes("1234567890123456");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] GetIv() => Encoding.UTF8.GetBytes("6543210987654321");

    public static string Encrypt(string plainText)
    {
        byte[] plainBytes = new byte[Encoding.UTF8.GetByteCount(plainText)];
        byte[] salt = new byte[SaltSize];

        try
        {
            // Generate salt directly into rented buffer
            Rng.GetBytes(salt, 0, SaltSize);

            // Convert string to bytes directly into rented buffer
            int plainBytesLength = Encoding.UTF8.GetBytes(plainText, 0, plainText.Length, plainBytes, 0);

            // Rent buffer for salted plain text
            int saltedLength = SaltSize + plainBytesLength;
            byte[] saltedPlain = new byte[saltedLength];

            // Copy salt and plain bytes
            Buffer.BlockCopy(salt, 0, saltedPlain, 0, SaltSize);
            Buffer.BlockCopy(plainBytes, 0, saltedPlain, SaltSize, plainBytesLength);

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = GetKey();
            aesAlg.IV = GetIv();

            using ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            using MemoryStream msEncrypt = new MemoryStream();
            using CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);

            csEncrypt.Write(saltedPlain, 0, saltedLength);
            csEncrypt.FlushFinalBlock();

            return Convert.ToBase64String(msEncrypt.GetBuffer(), 0, (int)msEncrypt.Length);
        }
        catch (Exception _)
        {
            // ignored
        }

        return "";
    }

    public static string Decrypt(string cipherText)
    {
        // Calculate exact buffer size needed for base64
        int base64Length = cipherText.Length;
        int bufferSize = (base64Length * 3 + 3) / 4; // Base64 decode size estimation

        byte[] cipherBytes = new byte[bufferSize];

        try
        {
            // Convert from base64 directly into rented buffer
            if (!Convert.TryFromBase64String(cipherText, cipherBytes, out int cipherBytesLength))
            {
                throw new FormatException("Invalid base64 string");
            }

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = GetKey();
            aesAlg.IV = GetIv();

            using ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
            using MemoryStream msDecrypt = new MemoryStream(cipherBytes, 0, cipherBytesLength);
            using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);

            // Rent buffer for decrypted data
            byte[] plainWithSalt = new byte[cipherBytesLength];
            int totalRead = 0;
            int bytesRead;

            // Read all decrypted data
            while ((bytesRead = csDecrypt.Read(plainWithSalt, totalRead, plainWithSalt.Length - totalRead)) > 0)
            {
                totalRead += bytesRead;
            }

            if (totalRead < SaltSize)
                throw new CryptographicException("Decrypted data is shorter than the expected salt size.");

            // Extract plain text without salt
            int plainLength = totalRead - SaltSize;
            byte[] plainBytes = new byte[plainLength];
            Buffer.BlockCopy(plainWithSalt, SaltSize, plainBytes, 0, plainLength);

            return Encoding.UTF8.GetString(plainBytes, 0, plainLength);
        }
        catch (Exception _)
        {
            // ignored
        }

        return "";
    }

    public static T LoadData<T>(IDataModule dataModule) where T : IDataModule, new()
    {
        string key = dataModule.Key;
        string stringData = PlayerPrefs.GetString(key, "");
        if (stringData != "")
        {
            try
            {
                return Deserialize<T>(stringData);
            }
            catch (Exception)
            {
                T result = new T();
                result.SetDefault();
                return result;
            }
        }

        T result2 = new T();
        result2.SetDefault();
        return result2;
    }

    public static void SaveData<T>(T obj) where T : IDataModule
    {
        string key = obj.Key;
        string stringData = Serialize(obj);
        PlayerPrefs.SetString(key, stringData);
    }
}

public class Vector3Converter : JsonConverter<Vector3>
{
    public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(value.x);
        writer.WritePropertyName("y"); writer.WriteValue(value.y);
        writer.WritePropertyName("z"); writer.WriteValue(value.z);
        writer.WriteEndObject();
    }

    public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        JObject obj = JObject.Load(reader);
        float x = obj["x"]?.Value<float>() ?? 0f;
        float y = obj["y"]?.Value<float>() ?? 0f;
        float z = obj["z"]?.Value<float>() ?? 0f;
        return new Vector3(x, y, z);
    }
}

public class CustomResolver : DefaultContractResolver
{
    private const string BACKING_FIELD_PREFIX = "<";
    private const string BACKING_FIELD_SUFFIX = ">k__BackingField";

    protected override List<MemberInfo> GetSerializableMembers(Type objectType)
    {
        List<MemberInfo> result = new List<MemberInfo>();

        const BindingFlags flags = BindingFlags.Instance |
                                   BindingFlags.Public |
                                   BindingFlags.NonPublic;

        foreach (FieldInfo field in objectType.GetFields(flags))
        {
            if (field.IsStatic) continue;

            if (TryGetAutoProperty(field, objectType, out _))
            {
                if (HasSerializeField(field))
                {
                    result.Add(field);
                }

                continue;
            }

            if (field.IsPublic || HasSerializeField(field))
            {
                result.Add(field);
            }
        }

        return result;
    }

    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        IList<JsonProperty> properties = base.CreateProperties(type, memberSerialization);
        List<JsonProperty> result = new List<JsonProperty>(properties.Count);

        foreach (JsonProperty property in properties)
        {
            if (string.IsNullOrEmpty(property.UnderlyingName))
            {
                result.Add(property);
                continue;
            }

            if (!TryGetField(type, property.UnderlyingName, out FieldInfo field))
            {
                result.Add(property);
                continue;
            }

            if (!TryGetAutoProperty(field, type, out PropertyInfo autoProperty))
            {
                result.Add(property);
                continue;
            }

            property.Ignored = false;
            property.Readable = true;
            property.Writable = true;
            property.PropertyName = autoProperty.Name;
            result.Add(property);

            JsonProperty legacyProperty = base.CreateProperty(field, memberSerialization);
            legacyProperty.Readable = false;
            legacyProperty.Writable = true;
            legacyProperty.ShouldSerialize = _ => false;
            result.Add(legacyProperty);
        }

        return result;
    }

    private static bool HasSerializeField(FieldInfo field)
    {
        return field.GetCustomAttribute<SerializeField>() != null;
    }

    private static bool TryGetField(Type objectType, string fieldName, out FieldInfo field)
    {
        const BindingFlags flags = BindingFlags.Instance |
                                   BindingFlags.Public |
                                   BindingFlags.NonPublic;

        field = objectType.GetField(fieldName, flags);
        return field != null;
    }

    private static bool TryGetAutoProperty(FieldInfo field, Type objectType, out PropertyInfo property)
    {
        property = null;
        if (!field.Name.StartsWith(BACKING_FIELD_PREFIX, StringComparison.Ordinal)) return false;
        if (!field.Name.EndsWith(BACKING_FIELD_SUFFIX, StringComparison.Ordinal)) return false;

        string propertyName = field.Name.Substring(1, field.Name.Length - BACKING_FIELD_PREFIX.Length - BACKING_FIELD_SUFFIX.Length);
        const BindingFlags flags = BindingFlags.Instance |
                                   BindingFlags.Public |
                                   BindingFlags.NonPublic;

        property = objectType.GetProperty(propertyName, flags);
        return property != null;
    }
}
