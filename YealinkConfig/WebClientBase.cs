using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace sharedlib
{
    public enum ContentType
    {
        UrlEncoded,
        UrlEncodedUTF8,
        Multipart,
        JsonUTF8,
        Json,
        PlainUTF8
    }
    public class WebClientBase : WebClient
    {
        private bool isAsync = false;
        private volatile bool isDisposed = false;
        private object downloadLock = new object();
        private CookieContainer m_container = new CookieContainer();
        private const string userAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/56.0.2924.87 Safari/537.36";
        private Dictionary<ContentType, string> contentTypes = new Dictionary<ContentType, string>() {
                 {ContentType.JsonUTF8, "application/json; charset=UTF-8"},
                 {ContentType.UrlEncodedUTF8, "application/x-www-form-urlencoded; charset=UTF-8"},
                 {ContentType.UrlEncoded, "application/x-www-form-urlencoded"},
                 {ContentType.Multipart, "multipart/form-data"},
                 {ContentType.PlainUTF8, "text/plain;charset=UTF-8"},
                 {ContentType.Json, "application/json"},

            };

        protected override void Dispose(bool disposing)
        {
            isDisposed = true;
            base.Dispose(disposing);
        }

        public WebClientBase()
        {

            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                       | SecurityProtocolType.Tls11
                       | SecurityProtocolType.Tls12
                       | SecurityProtocolType.Ssl3;
            }
            catch
            {

            }

            KeepAlive = true;
#if DEBUG
            IsDebug = true;
#else
            IsDebug = false;
#endif
            IsAnonymous = false;
            ErrorHandler = (error) =>
            {
                if (IsDebug)
                    Log.WriteError("Webclient error: {0}, url: {1}", error, Url);
            };
            StartPos = -1;
            EndPos = -1;
            Timeout = 60000;
#if DEBUG
            Proxy = GlobalProxySelection.GetEmptyWebProxy();  //Proxy = Utils.Proxy.DefaultProxy;
#else
            Proxy = GlobalProxySelection.GetEmptyWebProxy();
#endif
            SuccessHandler = () => { };
            UserAgent = userAgent;
        }
        public Action<string> ErrorHandler { get; set; }
        public Action SuccessHandler { get; set; }
        public bool KeepAlive { get; set; }
        public int Timeout { get; set; }
        public long StartPos { get; set; }
        public long EndPos { get; set; }
        public bool IsAnonymous { get; set; }
        public string RedirectUrl { get; set; }
        public WebRequest CurrentRequest { get; set; }
        public string UserAgent { get; set; }
        public string Referer { get; set; }
        public string Url { get; set; }
        public int StatusCode { get; set; }
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest.DefaultWebProxy = null;

            if (IsDebug)
                Log.WriteInfo("Preparing request for {0}", address.OriginalString);
            Url = address.OriginalString;
            RedirectUrl = null;
            WebRequest request = base.GetWebRequest(address);
            CurrentRequest = request;
            HttpWebRequest webRequest = request as HttpWebRequest;
            Sugar.Try(() =>
            {
                if (webRequest != null)
                {
                    if (KeepAlive)
                    {
                        webRequest.ProtocolVersion = HttpVersion.Version11;
                        webRequest.KeepAlive = true;
                        var sp = webRequest.ServicePoint;
                        if( sp != null )
                        {
                            var prop = sp.GetType().GetProperty("HttpBehaviour", BindingFlags.Instance | BindingFlags.NonPublic);
                            if( prop != null )
                                prop.SetValue(sp, (byte)0, null);
                        }
                    }
                    else
                    {
                        webRequest.KeepAlive = false;
                    }

                    if (StartPos != -1 && EndPos != -1)
                    {
                        webRequest.AddRange(StartPos, EndPos);
                    }
                    webRequest.Timeout = Timeout;
                    if (!IsAnonymous)
                        webRequest.CookieContainer = m_container;

                    webRequest.UserAgent = UserAgent;
                    webRequest.Proxy = Proxy;
                    if( Referer != null )
                        webRequest.Referer = Referer;

                    webRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
                    webRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    webRequest.ServicePoint.BindIPEndPointDelegate = BindIPEndPointCallback;
                }
            });
            return request;
        }
        [DebuggerHidden]
        private IPEndPoint BindIPEndPointCallback(ServicePoint servicePoint, IPEndPoint remoteEndPoint, int retryCount)
        {
            if (remoteEndPoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return new IPEndPoint(IPAddress.Any, 0);
            }
            throw new InvalidOperationException("not IPv4 address!");
        }
        public bool IsDebug { get; set; }
        public String Download(String url, bool setEncoding = true)
        {
            lock (downloadLock)
            {
                return (string)TryWeb(url, () =>
                {
                    if (url.StartsWith(@"Content"))
                    {
                        url = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, url);
                    }

                    SuccessHandler();
                    if (setEncoding)
                        Encoding = Encoding.UTF8;

                    return DownloadString(new Uri(url));
                });
            }
        }
        public String DownloadIgnore(String url)
        {
            lock (downloadLock)
            {
                WebResponse response = null;
                try
                {
                    var request = GetWebRequest(new Uri(url));
                    request.Method = "GET";

                    response = GetWebResponse(request);
                    var httpResponse = response as HttpWebResponse;
                    if (httpResponse != null)
                    {
                        StatusCode = (int)httpResponse.StatusCode;
                    }
                    return ResponseToString(response);
                }
                catch (WebException e)
                {
                    response = e.Response;
                    var httpResponse = e.Response as HttpWebResponse;
                    if( httpResponse!= null)
                    {
                        StatusCode = (int)httpResponse.StatusCode;
                        if (IsDebug)                        
                            Log.WriteError("Download ignore {0} code: {1}", url, httpResponse.StatusCode);
                    }

                    if (SuccessHandler != null)
                        SuccessHandler();

                    return ResponseToString(response);

                }
            }
        }
        private string ResponseToString(WebResponse response)
        {
            MemoryStream memoryStream = new MemoryStream();

            if (response == null)
                return null;

            var stream = response.GetResponseStream();

            if (stream.CanRead)
            {
                byte[] buffer = new byte[4096];

                int bytesRead = 0;

                do
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    memoryStream.Write(buffer, 0, bytesRead);
                } while (bytesRead > 0);

                memoryStream.Position = 0;
                response.Close();

                var reader = new StreamReader(memoryStream);
                return reader.ReadToEnd();
            }
            return null;
        }

        public byte[] DownloadToByteArray(String url)
        {
            try
            {
                lock (downloadLock)
                {
                    SuccessHandler();
                    return DownloadData(new Uri(url));
                }
            }
            catch (Exception e)
            {
                if (IsDebug)
                    Log.WriteError("WebClient:{0}", e.Message);
                ErrorHandler(String.Format("Error downloading to byte array from {0}", url));
            }
            return new byte[] { };
        }
        public Task<MemoryStream> DownloadToMemoryStreamAsync(String url, string method = "GET")
        {
            return Task.Factory.StartNew<MemoryStream>(() =>
            {
                return DownloadToMemoryStream(url, method);
            });
        }

        public MemoryStream DownloadToMemoryStream(String url, string method = "GET")
        {
            try
            {
                lock (downloadLock)
                {
                    var request = GetWebRequest(new Uri(url));
                    request.Method = method;

                    var response = GetWebResponse(request);

                    StatusCode = (int)((HttpWebResponse)response).StatusCode;

                    if (SuccessHandler != null)
                        SuccessHandler();

                    MemoryStream memoryStream = new MemoryStream();

                    var stream = response.GetResponseStream();

                    if (stream.CanRead)
                    {
                        byte[] buffer = new byte[4096];

                        int bytesRead = 0;

                        do
                        {
                            bytesRead = stream.Read(buffer, 0, buffer.Length);
                            memoryStream.Write(buffer, 0, bytesRead);
                        } while (bytesRead > 0);

                        memoryStream.Position = 0;
                        response.Close();
                        return memoryStream;
                    }
                }
            }
            catch (WebException e)
            {
                if (e != null && e.Response != null)
                    StatusCode = (int)((HttpWebResponse)e.Response).StatusCode;
                else
                    StatusCode = -1;
                if (IsDebug)
                    Log.WriteError("WebClient:{0}", e.Message);
                ErrorHandler(String.Format("Error downloading {0} to memorystream", url));
            }
            return null;

        }
        public Stream DownloadToStream(String url, bool cache = false)
        {
            try
            {
                lock (downloadLock)
                {
                    var request = GetWebRequest(new Uri(url));
                    if (cache)
                    {
                        request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.CacheIfAvailable);
                    }
                    var response = GetWebResponse(request);

                    if (SuccessHandler != null)
                        SuccessHandler();

                    return response.GetResponseStream();
                }
            }
            catch (Exception e)
            {
                if (IsDebug)
                    Log.WriteError("WebClient:{0}", e.Message);

                ErrorHandler(String.Format("Error downloading {0} to stream", url));
            }
            return null;
        }

        public String Upload(string url, string args)
        {
            string result = null;
            try
            {
                lock (downloadLock)
                {
                    result = CustomMethod(url, "POST", args);
                    SuccessHandler();
                }
            }
            catch (Exception e)
            {
                var responseStream = (e as WebException).With(x => x.Response).With(x => x.GetResponseStream());
                if (responseStream != null)
                {
                    using (StreamReader streamReader = new StreamReader(responseStream))
                    {
                        if (IsDebug)
                            Log.WriteInfo("Error upload to: {0} data: {1} response: {2}", url, args, streamReader.With(x => x.ReadToEnd()));
                    }
                }
                StatusCode = -1;
                if (IsDebug)
                    Log.WriteError("WebClient:{0}", e.Message);
                ErrorHandler(String.Format("Error uploading to {0}", url));
            }
            return result;
        }
        public String UploadBytes(string url, byte[] data)
        {
            lock (downloadLock)
            {
                return (string)TryWeb(url, () =>
                {
                    Uri uri;
                    if (Uri.TryCreate(url, UriKind.Absolute, out uri))
                    {
                        var request = (HttpWebRequest)GetWebRequest(uri);
                        request.Proxy = Proxy;
                        request.Method = "POST";
                        request.KeepAlive = KeepAlive;
                        request.ContentType = contentType;
                        request.Timeout = Timeout;

                        if (data != null)
                        {                            
                            request.ContentLength = data.Length;
                            Stream dataStream = request.GetRequestStream();
                            dataStream.Write(data, 0, data.Length);
                            dataStream.Flush();
                            dataStream.Close();
                        }
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        StatusCode = (int)((HttpWebResponse)response).StatusCode;
                        using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                    return null;
                });
            }

        }
        public String UploadIgnore(string url, string args)
        {
            string result = null;
            try
            {
                lock (downloadLock)
                {
                    result = UploadString(url, args);
                    SuccessHandler();
                }
            }
            catch (WebException e)
            {
                WebResponse response = null;
                response = e.Response;
                var httpResponse = e.Response as HttpWebResponse;
                if (httpResponse != null && IsDebug)
                    Log.WriteError("Download ignore {0} code: {1}", url, httpResponse.StatusCode);

                if (SuccessHandler != null)
                    SuccessHandler();

                return ResponseToString(response);
            }
            return result;
            //lock (downloadLock)
            //{
            //    WebResponse response = null;
            //    try
            //    {
            //        var request = GetWebRequest(new Uri(url));
            //        request.Method = "GET";

            //        response = GetWebResponse(request);

            //        return ResponseToString(response);
            //    }
            //    catch (WebException e)
            //    {
            //        response = e.Response;
            //        var httpResponse = e.Response as HttpWebResponse;
            //        if (httpResponse != null && IsDebug)
            //            Log.WriteError("Download ignore {0} code: {1}", url, httpResponse.StatusCode);

            //        if (SuccessHandler != null)
            //            SuccessHandler();

            //        return ResponseToString(response);

            //    }
            //}
        }
        public string CustomMethod(string url, string method, string data)
        {
            lock (downloadLock)
            {
                return (string)TryWeb(url, () =>
                {
                    Uri uri;
                    if (Uri.TryCreate(url, UriKind.Absolute, out uri))
                    {
                        var request = (HttpWebRequest)GetWebRequest(uri);
                        request.Proxy = Proxy;
                        request.Method = method;
                        request.KeepAlive = KeepAlive;
                        request.ContentType = contentType;
                        request.Timeout = Timeout;

                        if (!String.IsNullOrWhiteSpace(data))
                        {
                            var byteArray = Encoding.UTF8.GetBytes(data);
                            request.ContentLength = byteArray.Length;
                            Stream dataStream = request.GetRequestStream();
                            dataStream.Write(byteArray, 0, byteArray.Length);
                            dataStream.Flush();
                            dataStream.Close();
                        }
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        StatusCode = (int)((HttpWebResponse)response).StatusCode;
                        using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                    return null;
                });
            }
        }
        public string Delete(string url, string data)
        {
            return CustomMethod(url, "DELETE", data);
        }
        public Stream PutStream(string url, Stream stream)
        {
            try
            {
                lock (downloadLock)
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "PUT";
                    if (stream != null)
                    {
                        request.ContentLength = stream.Length;
                        Stream dataStream = request.GetRequestStream();
                        stream.CopyTo(dataStream);
                        stream.Flush();
                        dataStream.Flush();
                        dataStream.Close();
                    }

                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    return response.GetResponseStream();
                }
            }
            catch (Exception e)
            {
                if (IsDebug)
                    Log.WriteInfo("PutStream: {0}", e.Message);
                return null;
            }
        }
        public string Put(string url, string data)
        {
            try
            {
                lock (downloadLock)
                {
                    var request = (HttpWebRequest)GetWebRequest(new Uri(url));
                    request.Method = "PUT";
                    if (!String.IsNullOrWhiteSpace(data))
                    {
                        var byteArray = Encoding.UTF8.GetBytes(data);
                        request.ContentLength = byteArray.Length;
                        Stream dataStream = request.GetRequestStream();
                        dataStream.Write(byteArray, 0, byteArray.Length);
                        dataStream.Flush();
                        dataStream.Close();
                    }

                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                if (IsDebug)
                    Log.WriteInfo("PutStream: {0}", e.Message);
                return null;
            }
        }
        public void RequestPatchOptions(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "OPTIONS";

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response != null)
                    return;
            }
            catch (Exception e)
            {
                if (IsDebug)
                    Log.WriteInfo("RequestPatchOptions {0}, {1}", e.Message, url);
            }
        }

        public Stream PatchStream(string url, Stream stream)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "PATCH";
                request.Headers = Headers;
                request.ContentType = @"application/json;charset=UTF-8";

                if (stream != null)
                {
                    request.ContentLength = stream.Length;
                    Stream dataStream = request.GetRequestStream();
                    stream.CopyTo(dataStream);
                    stream.Flush();
                    dataStream.Flush();
                    dataStream.Close();
                }

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                return response.GetResponseStream();
            }
            catch (Exception e)
            {
                if (IsDebug)
                    Log.WriteInfo("PatchStream {0}, {1}", e.Message, url);
                return null;
            }
        }


        public void SetCookie(string name, string value, string domain, bool issecure = false, long expireSeconds = 0)
        {
            if (CookieValue(name, "http://" + domain) == value)
                return;

            if (name == null || value == null)
                return;



            m_container.Capacity += 1;
            var cookie = new Cookie(name, value, "/", domain);
            if (expireSeconds > 0)
                cookie.Expires = DateTime.Now.AddSeconds(expireSeconds);
            if (issecure)
                cookie.Secure = true;
            m_container.Add(cookie);
        }


        public ContentType ContentType
        {
            set {
                contentType = contentTypes[value];
                this.Headers[HttpRequestHeader.ContentType] = contentTypes[value]; 
            }
        }
        public string ContentTypeString
        {
            set
            {
                contentType = value;
                this.Headers[HttpRequestHeader.ContentType] = value;
            }
        }
        private string contentType { get; set; }

        public string CookieValue(string name, string url)
        {
            Uri uri;

            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
                Uri.TryCreate("http://" + url, UriKind.Absolute, out uri);

            if (uri != null)
            {
                var coll = m_container.GetCookies(uri);
                if (coll == null || coll[name] == null)
                    return String.Empty;
                return coll[name].Value;
            }
            return null;
        }
        public void ResetCookies()
        {
            m_container = null;
            m_container = new CookieContainer();
        }
        public List<Cookie> CookiesTable
        {
            get
            {
                Hashtable table = (Hashtable)Cookies.GetType().InvokeMember("m_domainTable",
                                                             BindingFlags.NonPublic |
                                                             BindingFlags.GetField |
                                                             BindingFlags.Instance,
                                                             null,
                                                             Cookies,
                                                             new object[] { });
                List<Cookie> result = new List<Cookie>();
                foreach (var key in table.Keys)
                {
                    var url = String.Format("http://{0}/", key.ToString().TrimStart('.'));

                    foreach (Cookie cookie in Cookies.GetCookies(new Uri(url)))
                    {
                        if (result.Find(c => c.Name.Equals(cookie.Name)) == default(Cookie))
                            result.Add(cookie);
                    }
                }

                return result;
            }

            set
            {
                try
                {
                    foreach (Cookie cookie in value)
                    {
                        m_container.Add(cookie);
                    }
                }
                catch (Exception e)
                {
                    if (IsDebug)
                        Log.WriteError("WebClient:{0}", e.Message);
                }
            }
        }

        public CookieContainer Cookies
        {
            get { return m_container; }
            set
            {
                if (value == null)
                    return;

                Hashtable table = (Hashtable)value.GetType().InvokeMember("m_domainTable",
                                                             BindingFlags.NonPublic |
                                                             BindingFlags.GetField |
                                                             BindingFlags.Instance,
                                                             null,
                                                             value,
                                                             new object[] { });
                foreach (var key in table.Keys)
                {
                    var url = String.Format("http://{0}/", key.ToString().TrimStart('.'));

                    foreach (Cookie cookie in value.GetCookies(new Uri(url)))
                    {
                        if (CookieValue(cookie.Name, url) == cookie.Value)
                            continue;

                        m_container.Add(cookie);
                    }
                }


            }
        }
        public List<KeyValuePair<string, string>> CookiesStrings
        {
            get
            {
                List<KeyValuePair<string, string>> _cookies = new List<KeyValuePair<string, string>>();
                Hashtable table = (Hashtable)m_container.GetType().InvokeMember("m_domainTable",
                                             BindingFlags.NonPublic |
                                             BindingFlags.GetField |
                                             BindingFlags.Instance,
                                             null,
                                             m_container,
                                             new object[] { });
                foreach (var key in table.Keys)
                {
                    var url = String.Format("http://{0}/", key.ToString().TrimStart('.'));

                    foreach (Cookie cookie in m_container.GetCookies(new Uri(url)))
                    {
                        _cookies.Add(new KeyValuePair<string, string>(cookie.Name, cookie.Value));
                    }
                }

                return _cookies;
            }
        }
        public long GetContentLength(string url)
        {
            lock (downloadLock)
            {
                try
                {
                    WebRequest request = GetWebRequest(new Uri(url));
                    //request.Method = "HEAD";
                    StartPos = -128;
                    EndPos = -128;
                    WebResponse result = request.GetResponse();
                    StartPos = -1;
                    EndPos = -1;
                    if (result != null)
                    {
                        var length = result.ContentLength;
                        result.Close();
                        return length;
                    }
                }
                catch (Exception e)
                {
                    if (IsDebug)
                        Log.WriteInfo("GetContentLength {0}, {1}", e.Message, url);
                    StartPos = -1;
                    EndPos = -1;
                }
                return -1;
            }
        }

        public Stream DownloadPartial(string url, long startPos, long endPos)
        {
            lock (downloadLock)
            {
                try
                {
                    StartPos = startPos;
                    EndPos = endPos;
                    WebRequest request = GetWebRequest(new Uri(url));
                    WebResponse result = request.GetResponse();
                    StartPos = -1;
                    EndPos = -1;
                    return result.GetResponseStream();
                }
                catch (Exception e)
                {
                    if (IsDebug)
                        Log.WriteInfo("DownloadPartial {0}, {1}", e.Message, url);
                    StartPos = -1;
                    EndPos = -1;
                }
                return null;
            }
        }
        public WebHeaderCollection GetResponseHeaders(string url, string method, string data = null)
        {
            try
            {
                lock (downloadLock)
                {
                    return (WebHeaderCollection)TryWeb(url, () =>
                    {
                        Uri uri;

                        if (Uri.TryCreate(url, UriKind.Absolute, out uri))
                        {
                            HttpWebRequest request = (HttpWebRequest)GetWebRequest(uri);
                            request.AllowAutoRedirect = false;
                            request.Method = method;

                            if (method.ToUpper() == "POST" && data != null)
                            {
                                using (var requestStream = request.GetRequestStream())
                                {
                                    var bytes = Encoding.GetBytes(data);
                                    requestStream.Write(bytes, 0, bytes.Length);
                                    requestStream.Close();
                                }
                            }
                            WebResponse response = GetWebResponse(request);
                            response.Close();

                            return response.Headers;
                        }
                        return null;
                    });
                }
            }
            catch (WebException webException)
            {
                if (webException != null && webException.Response != null)
                    StatusCode = (int)((HttpWebResponse)webException.Response).StatusCode;
                else
                    StatusCode = -1;
            }
            catch (Exception e)
            {
                StatusCode = -1;
            }
            return null;

        }
        public bool IsImage(string url)
        {
            var headers = GetResponseHeaders(url, "HEAD");
            if (headers == null)
                return false;
            var contentType = headers["Content-Type"];

            if (!String.IsNullOrWhiteSpace(contentType) &&
                contentType.StartsWith("image/"))
                return true;

            return false;

        }
        public bool IsJpeg(string url)
        {
            var headers = GetResponseHeaders(url, "HEAD");
            if (headers == null)
                return false;

            var contentType = headers["Content-Type"];

            if (!String.IsNullOrWhiteSpace(contentType) && contentType.StartsWith("image/jpeg"))
                return true;

            return false;

        }

        public bool IsGif(string url)
        {
            var headers = GetResponseHeaders(url, "HEAD");
            if (headers == null)
                return false;

            var contentType = headers["Content-Type"];

            if (!String.IsNullOrWhiteSpace(contentType) && contentType.StartsWith("image/gif"))
                return true;

            return false;

        }
        public string GetRedirectUrl(string url)
        {
            lock (downloadLock)
            {
                return (string)TryWeb(url, () =>
                {
                    Uri uri;
                    if (Uri.TryCreate(url, UriKind.Absolute, out uri))
                    {
                        var request = GetWebRequest(uri);
                        var response = GetWebResponse(request);
                        response.Close();
                        RedirectUrl = response.ResponseUri.OriginalString;
                        return RedirectUrl;
                    }
                    return url;
                });
            }
        }

        public object TryWeb(string url, Func<object> action)
        {
            if (isDisposed)
                return null;

            object result = null;
            try
            {
                result = action();
                StatusCode = 200;
                if (!isAsync && !((HttpWebRequest)CurrentRequest).HaveResponse)
                {                                
                    WebResponse response;
                    response = CurrentRequest.GetResponse();

                    if (response != null && response is HttpWebResponse)
                    {
                        StatusCode = (int)((HttpWebResponse)response).StatusCode;

                        if (IsDebug)
                            Log.WriteInfo("HTTP status code: {0}, url: {1}", StatusCode, url);
                    }
                }
            }
            catch (WebException e)
            {
                var response = e.Response;
                var httpResponse = e.Response as HttpWebResponse;
                if (httpResponse != null && IsDebug)
                    Log.WriteError("Request error {0} code: {1}", url, httpResponse.StatusCode);
                StatusCode = (int)httpResponse.StatusCode;
                if (SuccessHandler != null)
                    SuccessHandler();

                return ResponseToString(response);

            }
            catch (Exception e)
            {
                //try
                //{
                //    if (e is WebException)
                //    {
                //        WebException webException = (WebException)e;
                //        if (webException.Response != null && webException.Response is HttpWebResponse)
                //        {
                //            WebResponse response = null;
                //            response = webException.Response;
                //            result = ResponseToString(response);

                //            StatusCode = (int)((HttpWebResponse)webException.Response).StatusCode;
                //            if (IsDebug)
                //                Log.WriteInfo("HTTP status code: {0}, url: {1}", StatusCode, url);
                //        }
                //    }
                //}
                //catch
                //{

                //}

                if (ErrorHandler != null)
                    ErrorHandler(e.Message);
                else if (IsDebug)
                    Log.WriteError("{0}, url: {1}", e.Message, url);

            }

            return result;
        }

        public string PostMultipart(string url, string sData, string boundary)
        {
            byte[] data = Encoding.GetBytes(sData);

            lock (downloadLock)
            {
                return (string)TryWeb(url, () =>
                {
                    Encoding = Encoding.UTF8;

                    HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                    request.Headers = Headers;
                    request.Method = "POST";
                    request.ContentType = "multipart/form-data; boundary=" + boundary;
                    request.UserAgent = UserAgent;
                    request.CookieContainer = m_container;
                    request.ContentLength = data.Length;
                    request.KeepAlive = true;
                    using (var requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(data, 0, data.Length);
                        requestStream.Close();
                    }

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        SuccessHandler();
                        using (Stream resStream = response.GetResponseStream())
                        {
                            StreamReader reader = new StreamReader(resStream, Encoding.UTF8);
                            return reader.ReadToEnd();
                        }
                    }
                });
            }
        }
        public string CookieParamString
        {
            get
            {
                var strings = CookiesStrings.Select(kvp => string.Format("{0}={1}", kvp.Key, kvp.Value));
                return string.Join("; ", strings);
            }
        }
        public string DownloadLowLevel(string url)
        {
            var uri = new Uri(url);
            IPHostEntry hostEntry = Dns.GetHostEntry(uri.Host);
            IPAddress address = hostEntry.AddressList[0];
            IPEndPoint ipEndpoint = new IPEndPoint(address, 80);
            Socket socket = new Socket(ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                socket.Connect(ipEndpoint);
                if (socket.Connected)
                {
                    if (IsDebug)
                        Log.WriteError("WebClient DownloadLowLevel: Connected to {0}", ipEndpoint.ToString());
                }
                else
                {
                    if (IsDebug)
                        Log.WriteError("WebClient DownloadLowLevel: can't connect to {0}", ipEndpoint.ToString());
                }
            }
            catch (SocketException ex)
            {
                if (IsDebug)
                    Log.WriteError("WebClient postFormDataLowLevel: socket exception {0}", ex.Message);
            }
            String requestString = String.Format("GET {0} HTTP/1.1\r\n" +
                "User-Agent: {1}\r\n" +
                "Accept: text/html,application/xhtml+xml,application/xml\r\n,*/*" +
                "Accept-Encoding: deflate\r\n" +
                "Host: {2}\r\n" +
                "Origin: http://{2}\r\n" +
                "Referer: http://{2}\r\n" +
                "Cache-Control: no-store, no-cache\r\n" +
                "Pragma: no-cache\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Accept-Language: en-US\r\n" +
                "Cookie: {3}\r\n\r\n" +
                "Content-Type: application/x-www-form-urlencoded\r\n\r\n",
                uri.AbsolutePath, userAgent, uri.Host, CookieParamString);

            byte[] request = Encoding.UTF8.GetBytes(requestString);

            Byte[] bytesReceived = new Byte[1024];
            socket.Send(request, request.Length, 0);
            string result = String.Empty;
            int bytes = 0;
            try
            {
                do
                {
                    bytes = socket.Receive(bytesReceived, bytesReceived.Length, 0);
                    result = result + Encoding.ASCII.GetString(bytesReceived, 0, bytes);
                }
                while (bytes > 0);
            }
            catch (Exception e)
            {
                if (IsDebug)
                    Log.WriteError("Low level download: {0}", e.Message);
            }
            bool found = true;
            var tmpResult = result;
            while (found)
            {
                var pair = Re.GetSubString(tmpResult, @"Set-Cookie:\s([^;]+)?;");
                if (String.IsNullOrEmpty(pair))
                {
                    found = false;
                    break;
                }
                var name = Re.GetSubString(pair, @"(.*)?=");
                var value = Re.GetSubString(pair, @".*?=(.*)");
                SetCookie(name, value, uri.DnsSafeHost);
                tmpResult = tmpResult.Replace(String.Format(@"Set-Cookie: {0}", name), "");
            }
            return Re.GetSubString(result, @"\r\n\r\n[a-z0-9]+\r\n(.*)");
        }
        public string PostFormDataLowLevel(string formActionUrl, string postData)
        {
            var uri = new Uri(formActionUrl);
            IPHostEntry hostEntry = Dns.GetHostEntry(uri.Host);
            IPAddress address = hostEntry.AddressList[0];
            IPEndPoint ipEndpoint = new IPEndPoint(address, 80);
            Socket socket = new Socket(ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                socket.Connect(ipEndpoint);
                if (socket.Connected)
                {
                    if (IsDebug)
                        Log.WriteError("WebClient postFormDataLowLevel: Connected to {0}", ipEndpoint.ToString());
                }
                else
                {
                    if (IsDebug)
                        Log.WriteError("WebClient postFormDataLowLevel: can't connect to {0}", ipEndpoint.ToString());
                }
            }
            catch (SocketException ex)
            {
                if (IsDebug)
                    Log.WriteError("WebClient postFormDataLowLevel: socket exception {0}", ex.Message);
            }
            var buffer = Encoding.UTF8.GetBytes(postData);

            String requestString = String.Format("POST {0} HTTP/1.1\r\n" +
                "User-Agent: {4}\r\n" +
                "Accept: text/html,application/xhtml+xml,application/xml\r\n" +
                "Accept-Encoding: deflate\r\n" +
                "Host: {1}\r\n" +
                "Origin: http://{1}\r\n" +
                "Referer: http://{1}\r\n" +
                "Cache-Control: no-store, no-cache\r\n" +
                "Pragma: no-cache\r\n" +
                "Content-Length: {2}\r\n" +
                "Connection: Keep-Alive\r\n" +
                "Accept-Language: en-US\r\n" +
                "Cookie: {5}\r\n" +
                "Content-Type: application/x-www-form-urlencoded\r\n\r\n"
                + "{3}", uri.AbsolutePath, uri.Host, Encoding.UTF8.GetBytes(postData).Length, postData, userAgent, CookieParamString);

            byte[] request = Encoding.UTF8.GetBytes(requestString);

            Byte[] bytesReceived = new Byte[1024];
            socket.Send(request, request.Length, 0);
            string result = String.Empty;
            int bytes = 0;
            try
            {
                do
                {
                    bytes = socket.Receive(bytesReceived, bytesReceived.Length, 0);
                    result = result + Encoding.ASCII.GetString(bytesReceived, 0, bytes);
                }
                while (bytes > 0);
            }
            catch (SocketException ex)
            {
                if (IsDebug)
                    Log.WriteError("WebClient postFormDataLowLevel: socket receive exception {0}", ex.Message);
            }
            bool found = true;
            var tmpResult = result;
            while (found)
            {
                var pair = Re.GetSubString(tmpResult, @"Set-Cookie:\s([^;]+)?;");
                if (String.IsNullOrEmpty(pair))
                {
                    found = false;
                    break;
                }
                var name = Re.GetSubString(pair, @"(.*)?=");
                var value = Re.GetSubString(pair, @".*?=(.*)");
                SetCookie(name, value, uri.DnsSafeHost);
                tmpResult = tmpResult.Replace(String.Format(@"Set-Cookie: {0}", name), "");
            }

            return result;
        }
        public event EventHandler<WebEventArgs> OnEvent;
        public event EventHandler<EventArgs> OnEventSourceConnect;
        public event EventHandler<EventArgs> OnEventSourceDisconnect;
        public bool ListenEventSource(string url)
        {
            isAsync = true;
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                var result = TryWeb(url, () =>
                {
                    Headers["Accept"] = "text/event-stream";
                    OpenReadAsync(uri);
                    return true;
                });
                if (result == null)
                    return false;
                else
                    return (bool)result;
            }
            return false;
        }

        protected override void OnOpenReadCompleted(OpenReadCompletedEventArgs e)
        {
            if (OnEvent != null)
            {
                var result = TryWeb(CurrentRequest.RequestUri.OriginalString, () =>
                {
                    using (StreamReader reader = new StreamReader(e.Result))
                    {
                        if (OnEventSourceConnect != null)
                            OnEventSourceConnect(this, new EventArgs());

                        string data = null;
                        string eventName = null;
                        int id = 0;
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if (line.StartsWith("data:"))
                            {
                                data += line.Substring(5).Trim();
                            }
                            else if (line.StartsWith("id:"))
                            {
                                int.TryParse(line.Substring(3).Trim(), out id);
                            }
                            else if (line.StartsWith("event:"))
                            {
                                eventName = line.Substring(6).Trim();
                            }
                            if (!String.IsNullOrWhiteSpace(data))
                            {
                                OnEvent(this, new WebEventArgs(id, data, eventName));
                                data = null;
                                eventName = null;
                                id = 0;
                            }
                        }
                    }
                    return true;
                });

                if (OnEventSourceDisconnect != null)
                    OnEventSourceDisconnect(this, new EventArgs());
            }
        }

    }
    public class WebEventArgs : EventArgs
    {
        public WebEventArgs(int id, string data, string eventName)
        {
            Id = id;
            Data = data;
            EventName = eventName;
        }
        public int Id { get; set; }
        public string Data { get; set; }
        public string EventName { get; set; }
    }

    public enum MultipartPostDataParamType
    {
        Field,
        File
    }
    public class MultipartPostData
    {

        private List<MultipartPostDataParam> m_Params;

        public List<MultipartPostDataParam> Params
        {
            get { return m_Params; }
            set { m_Params = value; }
        }

        public MultipartPostData()
        {
            m_Params = new List<MultipartPostDataParam>();

        }

        public String Boundary
        {
            get;
            set;
        }
        /// <summary>
        /// Returns the parameters array formatted for multi-part/form data
        /// </summary>
        /// <returns></returns>
        public string GetPostData()
        {
            // Get boundary, default is --AaB03x

            Boundary = "----WebKitFormBoundary" + RandomString(16);

            StringBuilder sb = new StringBuilder();
            foreach (MultipartPostDataParam p in m_Params)
            {
                sb.AppendLine("--" + Boundary);

                if (p.Type == MultipartPostDataParamType.File)
                {
                    sb.AppendLine(string.Format("Content-Disposition: file; name=\"{0}\"; filename=\"{1}\"", p.Name, p.FileName));
                    sb.AppendLine("Content-Type: text/plain");
                    sb.AppendLine();
                    sb.AppendLine(p.Value);
                }
                else
                {
                    sb.AppendLine(string.Format("Content-Disposition: form-data; name=\"{0}\"", p.Name));
                    sb.AppendLine();
                    sb.AppendLine(p.Value);
                }
            }

            sb.AppendLine("--" + Boundary + "--");

            return sb.ToString();
        }
        private string RandomString(int Size)
        {
            string input = "ABCDEFGHJIKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            StringBuilder builder = new StringBuilder();
            Random random = new Random();
            char ch;
            for (int i = 0; i < Size; i++)
            {
                ch = input[random.Next(0, input.Length)];
                builder.Append(ch);
            }
            return builder.ToString();
        }

    }
    public class MultipartPostDataParam
    {


        public MultipartPostDataParam(string name, string value, MultipartPostDataParamType type)
        {
            Name = name;
            Value = value;
            Type = type;
        }

        public string Name;
        public string FileName;
        public string Value;
        public MultipartPostDataParamType Type;
    }

    public class MultipartFormBuilder
    {
        static readonly string MultipartContentType = "multipart/form-data; boundary=";
        static readonly string FileHeaderTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: application/octet-stream\r\n\r\n";
        static readonly string FormDataTemplate = "\r\n--{0}\r\nContent-Disposition: form-data; name=\"{1}\";\r\n\r\n{2}";

        public string ContentType { get; private set; }

        string Boundary { get; set; }

        Dictionary<string, FileInfo> FilesToSend { get; set; } = new Dictionary<string, FileInfo>();
        Dictionary<string, string> FieldsToSend { get; set; } = new Dictionary<string, string>();

        public MultipartFormBuilder()
        {
            var id = DateTime.Now.Ticks.ToString("x");
            Boundary = id.PadLeft(38, '-');

            ContentType = MultipartContentType + Boundary;
        }

        public void AddField(string key, string value)
        {

            FieldsToSend.Add(key, value);
        }

        public void AddFile(FileInfo file)
        {
            string key = file.Extension.Substring(1);
            FilesToSend.Add(key, file);
        }

        public void AddFile(string key, FileInfo file)
        {
            FilesToSend.Add(key, file);
        }

        public MemoryStream GetStream()
        {
            var memStream = new MemoryStream();

            WriteFields(memStream);
            WriteStreams(memStream);
            WriteTrailer(memStream);

            memStream.Seek(0, SeekOrigin.Begin);

            return memStream;
        }

        void WriteFields(Stream stream)
        {
            if (FieldsToSend.Count == 0)
                return;

            foreach (var fieldEntry in FieldsToSend)
            {
                string content = string.Format(FormDataTemplate, Boundary, fieldEntry.Key, fieldEntry.Value);

                using (var fieldData = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                {
                    fieldData.CopyTo(stream);
                }
            }
        }

        void WriteStreams(Stream stream)
        {
            if (FilesToSend.Count == 0)
                return;

            foreach (var fileEntry in FilesToSend)
            {
                WriteBoundary(stream);

                string header = string.Format(FileHeaderTemplate, fileEntry.Key, fileEntry.Value.Name);
                byte[] headerbytes = Encoding.UTF8.GetBytes(header);
                stream.Write(headerbytes, 0, headerbytes.Length);

                using (var fileData = File.OpenRead(fileEntry.Value.FullName))
                {
                    fileData.CopyTo(stream);
                }
            }
        }

        void WriteBoundary(Stream stream)
        {
            byte[] boundarybytes = Encoding.UTF8.GetBytes("--" + Boundary + "\r\n");
            stream.Write(boundarybytes, 0, boundarybytes.Length);
        }

        void WriteTrailer(Stream stream)
        {
            byte[] trailer = Encoding.UTF8.GetBytes("\r\n--" + Boundary + "--\r\n");
            stream.Write(trailer, 0, trailer.Length);
        }
    }
}
