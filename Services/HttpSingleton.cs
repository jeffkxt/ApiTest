using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace UltraLightApiTester.Services
{
    public static class HttpSingleton
    {
        public static readonly HttpClient Instance;

        static HttpSingleton()
        {
            var handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 50,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            handler.ServerCertificateCustomValidationCallback = CertificateValidationCallback;
            Instance = new HttpClient(handler, disposeHandler: false);
            Instance.Timeout = TimeSpan.FromSeconds(30);
        }

        private static bool CertificateValidationCallback(
            HttpRequestMessage request,
            X509Certificate2 certificate,
            X509Chain chain,
            SslPolicyErrors errors)
        {
            return true; // allow self-signed certificates
        }
    }
}
