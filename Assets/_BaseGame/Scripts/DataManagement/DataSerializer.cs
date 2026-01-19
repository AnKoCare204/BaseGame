using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace CoreData
{
    public static class DataSerializer
    {
        private const int SaltSize = 16;
        private static readonly Dictionary<string, string> DictPath = new();
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
        public static string ToJson(object data)
        {
            return JsonConvert.SerializeObject(data);
        }
        public static T FromJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        public static string Serialize(object data)
        {
            return Encrypt(JsonUtility.ToJson(data));
        }

        public static T Deserialize<T>(string json)
        {
            return JsonUtility.FromJson<T>(Decrypt(json));
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
        public static async UniTask SaveLocal<T>(object o)
        {
            string path = GetPath(typeof(T).Name);
            string strEncode = Serialize(o);
            await File.WriteAllTextAsync(path, strEncode);
        }
        public static UniTask<T> GetDataLocal<T>() where T : class, IDataSave<T>, new()
        {
            string path = GetPath(typeof(T).Name);
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path);
                try
                {
                    T result = Deserialize<T>(text);
                    return UniTask.FromResult(result);
                }
                catch (Exception)
                {
                    T result2 = new T();
                    return UniTask.FromResult(result2.DefaultData());
                }
            }
        
            T result3 = new T();
            return UniTask.FromResult(result3.DefaultData());
        }
        private static string GetPath(string sClassName)
        {
            if (DictPath.TryGetValue(sClassName, out string value))
            {
                return value;
            }
            string text = Convert.ToBase64String(Encoding.UTF8.GetBytes(sClassName));
            value = ZString.Join("", Application.persistentDataPath, "/", text, ".data");
            DictPath.Add(sClassName, value);
            return value;
        }
        public static T LoadDataLocal<T>() where T : class, IDataSave<T>, new()
        {
            string path = GetPath(typeof(T).Name);
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path);
                try
                {
                    return Deserialize<T>(text);
                }
                catch (Exception)
                {
                    T result = new T();
                    return result.DefaultData();
                }
            }
        
            T result2 = new T();
            return result2.DefaultData();
        }
        public static T LoadDataFromPrefs<T>() where T : class, IDataSave<T>, new()
        {
            string key = typeof(T).Name;
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
                    return result.DefaultData();
                }
            }
            T result2 = new T();
            return result2.DefaultData();
        }

        public static void SaveDataPrefs<T>(T obj) where T : class, IDataSave<T>
        {
            string key = typeof(T).Name;
            string stringData = Serialize(obj);
            PlayerPrefs.SetString(key, stringData);
            
        }
    }
}