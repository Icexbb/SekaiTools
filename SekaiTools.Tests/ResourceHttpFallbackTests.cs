using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SekaiToolsInfrastructure.Resources;

namespace SekaiTools.Tests;

public class ResourceHttpFallbackTests
{
    [Theory]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors, true)]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch, false)]
    [InlineData(SslPolicyErrors.RemoteCertificateNotAvailable, false)]
    public async Task OnlyExpiredCertificateFallsBackToHttp(SslPolicyErrors errors, bool expectedFallback)
    {
        using var rootKey = RSA.Create(2048);
        using var leafKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Resource Test Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(30));
        var leafRequest = new CertificateRequest("CN=localhost", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var leaf = leafRequest.Create(root, DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-1), [1, 2, 3]);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        Assert.False(chain.Build(leaf));
        Assert.All(chain.ChainStatus, status => Assert.Equal(X509ChainStatusFlags.NotTimeValid, status.Status));

        var requests = new List<Uri>();
        using var client = new HttpClient(new ResourceHttpFallbackHandler(validate => new FakeTransport(request =>
        {
            requests.Add(request.RequestUri!);
            if (request.RequestUri!.Scheme == "https")
            {
                Assert.False(validate(request, leaf, chain, errors));
                throw new HttpRequestException("Certificate validation failed");
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("resource") };
        })));
        if (expectedFallback)
        {
            Assert.Equal("resource", await client.GetStringAsync("https://localhost/resources/test.bin?q=1"));
            Assert.Equal(new Uri("http://localhost/resources/test.bin?q=1"), requests[1]);
        }
        else
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStringAsync("https://localhost/resources/test.bin"));
        }
        Assert.Equal(expectedFallback ? 2 : 1, requests.Count);
    }

    [Fact]
    public async Task OrdinaryNetworkErrorDoesNotDowngrade()
    {
        var requests = 0;
        using var client = new HttpClient(new ResourceHttpFallbackHandler(_ => new FakeTransport(_ =>
        {
            requests++;
            throw new HttpRequestException("Connection failed");
        })));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStringAsync("https://localhost/resource"));
        Assert.Equal(1, requests);
    }

    [Fact]
    public void MissingChainDoesNotQualifyForHttpFallback()
    {
        Assert.False(ResourceHttpFallbackHandler.IsExpiredCertificate(null, null, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    private sealed class FakeTransport(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
