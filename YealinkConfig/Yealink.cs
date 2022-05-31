using sharedlib;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;

namespace YealinkConfig
{
    public class Yealink
    {
        private WebClientBase wc;
        private string ip, user, pwd, rsaN, rsaE;
        private byte[] key, iv, dataKey, dataIV;
        private RSAKey rsa;
        private Random rnd;
        public Yealink(string ip, string login, string pwd)
        {
            ServicePointManager.Expect100Continue = false;
            this.ip = ip; 
            this.user = login; 
            this.pwd = pwd;
            rnd = new Random();
            rsa = new RSAKey();
            wc = new WebClientBase();
            wc.Proxy = null; //new WebProxy("127.0.0.1", 8888);
        }
        public string BaseURL
        {
            get => $"https://{ip}";
        }
        public bool Login()
        {
            if( PreLogin() )
            {
                InitEncryption();
                wc.ContentType = ContentType.UrlEncoded;
                var data = $"username={user}&pwd={WebUtility.UrlEncode(Encrypt(pwd))}&rsakey={WebUtility.UrlEncode(ByteArrayToHex(dataKey))}&rsaiv={WebUtility.UrlEncode(ByteArrayToHex(dataIV))}";
                var result = wc.Upload($"{BaseURL}/servlet?m=mod_listener&p=login&q=login&Rajax={new Random().NextDouble()}", data);
                if (Re.IsMatch(result, @"""authstatus"":""done"""))
                    return true;
            }
            return false;
        }
        public bool Upload(string filePath)
        {
            var mp = new MultipartFormBuilder();
            mp.AddFile("UploadName", new FileInfo(filePath));
            var data = mp.GetStream().ToArray();
            var sessionId = Re.GetSubString(wc.CookieParamString, @"JSESSIONID=(\w+)");
            var maxlength = WebUtility.UrlEncode( ByteArrayToHex(rsa.Encrypt($"{sessionId};5MB", rsaN, rsaE)));      
            wc.ContentTypeString = mp.ContentType;
            var response = wc.UploadBytes($"{BaseURL}/servlet?m=mod_res&p=upload&type=localcfg&maxlength={maxlength}", data);
            if (Re.IsMatch(response, @"{""type"":""localcfg"",""result"":\d+}"))
                return true;
            return false;
        }
        public bool PreLogin()
        {
            var content = wc.Download($"{BaseURL}/servlet?m=mod_listener&p=login&q=loginForm&jumpto=status");
            if (String.IsNullOrWhiteSpace(content))
                return false;

            rsaN = GetBodyVar(content, "g_rsa_n");
            rsaE = GetBodyVar(content, "g_rsa_e");

            if (String.IsNullOrWhiteSpace(rsaN) || String.IsNullOrWhiteSpace(rsaE))
                return false;

            return true;
            
        }
        private string RandomDouble()
        {
            return rnd.NextDouble().ToString(CultureInfo.InvariantCulture);
        }
        public void InitEncryption()
        {
            var rsa = new RSAKey();
            var crypto = new Cryptography();
            var r = crypto.TextMD5(RandomDouble()).ToLower();
            var n = crypto.TextMD5(RandomDouble()).ToLower();
            dataKey =  rsa.Encrypt(r, rsaN, rsaE);
            key = StringToByteArray(r);
            dataIV = rsa.Encrypt(n, rsaN, rsaE);
            iv = StringToByteArray(n);
        }
      
        public string Encrypt(string message)
        {
            string result = "";
            using (SymmetricAlgorithm crypt = Aes.Create())
            {
                crypt.Mode = CipherMode.CBC;
                crypt.Padding = PaddingMode.Zeros;
                crypt.Key = key;
                crypt.IV = iv;
                var text = new Random().NextDouble().ToString()+";";
                var sessionId = Re.GetSubString(wc.CookieParamString, @"JSESSIONID=(\w+)");
                if( !String.IsNullOrEmpty(sessionId) )
                {
                    text += sessionId + ";" + message;
                }
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, crypt.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(text);
                        }
                        result = Convert.ToBase64String( msEncrypt.ToArray());
                    }
                }
            }
            return result;
        }
        public string ByteArrayToHex(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "").ToLower();
        }
        public byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        public string GetBodyVar(string content, string name)
        {
            return Re.GetSubString(content, $@"var {name} = ""([A-Za-z0-9]+)""");
        }
    }

}
