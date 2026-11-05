using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using SekaiToolsBase;
using Microsoft.Extensions.Logging;

namespace SekaiToolsInfrastructure.Resources;

// Only resource requests use this handler. A failed certificate check is never accepted as HTTPS.
internal sealed class ResourceHttpFallbackHandler : HttpMessageHandler
{
    private readonly HttpMessageInvoker _transport;
    private bool _expiredCertificate;

    internal ResourceHttpFallbackHandler(Func<RemoteCertificateValidationCallback, HttpMessageHandler> createTransport)
    {
        _transport = new HttpMessageInvoker(createTransport((_, certificate, chain, errors) =>
        {
            _expiredCertificate = IsExpiredCertificate(certificate, chain, errors);
            return errors == SslPolicyErrors.None;
        }));
    }

    internal static bool IsExpiredCertificate(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (certificate == null || chain == null || errors != SslPolicyErrors.RemoteCertificateChainErrors)
            return false;
        var statuses = chain.ChainStatus;
        return statuses.Length > 0 &&
               statuses.All(status => (status.Status & ~X509ChainStatusFlags.NotTimeValid) == 0) &&
               chain.ChainElements.Cast<X509ChainElement>().Any(element => element.Certificate.NotAfter.ToUniversalTime() < DateTime.UtcNow);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _expiredCertificate = false;
        try
        {
            return await _transport.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException) when (_expiredCertificate &&
                                           request.Method == HttpMethod.Get &&
                                           request.RequestUri is { Scheme: "https" })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uri = new UriBuilder(request.RequestUri) { Scheme = "http" };
            if (request.RequestUri.IsDefaultPort) uri.Port = -1;
            Logger.Log($"资源服务器证书过期，改用 HTTP 下载: {uri.Uri}", LogLevel.Warning);
            using var fallback = new HttpRequestMessage(HttpMethod.Get, uri.Uri);
            return await _transport.SendAsync(fallback, cancellationToken);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _transport.Dispose();
        base.Dispose(disposing);
    }
}
