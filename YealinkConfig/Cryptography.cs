using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace sharedlib
{
    public interface ICryptography
    {
        string FileMD5(string fileName);
        string TextMD5(string text);
        string EncryptString(string rawText);
        string DecryptString(string base64Encoded);
        string Base64ToText(string base64Encoded);
        bool Base64ToFile(string base64Encoded, string filePath);
        [return: MarshalAs(UnmanagedType.LPStr)] string ConvertString(string text, string from, string to);

    }
    public interface ICryptographyEvents
    {

    }
    public class Cryptography : ICryptography
    {
        public string FileMD5(string fileName)
        {
            var inputBytes = File.ReadAllBytes(fileName);
            return BytesMD5(inputBytes);
        }
        public string TextMD5(string text)
        {
            byte[] inputBytes = Encoding.Default.GetBytes(text);
            return BytesMD5(inputBytes);
        }
        public byte[] Bytes2MD5Bytes(byte[] inputBytes)
        {
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            return md5.ComputeHash(inputBytes);
        }
        private string BytesMD5(byte[] inputBytes)
        {
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }
        public string EncryptString(string rawText)
        {
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.Default.GetBytes(rawText), null, DataProtectionScope.CurrentUser));

        }
        public string DecryptString(string base64Encoded)
        {
            return Encoding.Default.GetString(ProtectedData.Unprotect(Convert.FromBase64String(base64Encoded), null, DataProtectionScope.CurrentUser));
        }
        public string Base64ToText(string base64Encoded)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64Encoded));
        }
        public bool Base64ToFile(string base64Encoded, string filePath)
        {
            File.WriteAllBytes(filePath, Convert.FromBase64String(base64Encoded));
            return true;
        }
        public string ByteArrayToBase64(byte[] ba)
        {
            return Convert.ToBase64String(ba);
        }

        [return: MarshalAs(UnmanagedType.LPStr)]
        public string ConvertString(string text, string from, string to)
        {
            Encoding fromEncoding = Encoding.GetEncoding(from);
            Encoding toEncoding = Encoding.GetEncoding(to);

            return toEncoding.GetString(  Encoding.Convert(fromEncoding, toEncoding, fromEncoding.GetBytes(text)) );
        }
    }
}
