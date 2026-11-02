using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FinancialAssistant.Shared.Hosting;

namespace FinancialAssistant.PublicApiGateway.Tests;

internal sealed class NativeHttpsTestCertificates : IDisposable
{
    public X509Certificate2 Root { get; }
    public X509Certificate2 Leaf { get; }

    public NativeHttpsTestCertificates(string host = "localhost", bool san = true, bool serverUsage = true,
        bool selfSigned = false, int keySize = 2048, DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null, X509Certificate2? issuer = null)
    {
        var now = DateTimeOffset.UtcNow;
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Synthetic Native TLS Root", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        Root = issuer is null ? rootRequest.CreateSelfSigned(now.AddYears(-2), now.AddYears(2)) : new X509Certificate2(issuer);
        using var key = RSA.Create(keySize);
        var request = new CertificateRequest("CN=" + host, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection
        { new(serverUsage ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2") }, true));
        if (san)
        {
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName(host); request.CertificateExtensions.Add(names.Build());
        }
        var start = notBefore ?? now.AddHours(-1); var finish = notAfter ?? now.AddDays(30);
        if (selfSigned) { Leaf = request.CreateSelfSigned(start, finish); }
        else
        {
            using var issued = request.Create(Root, start, finish, RandomNumberGenerator.GetBytes(16));
            Leaf = issued.CopyWithPrivateKey(key);
        }
    }

    public NativeHttpsPlan Plan(int port = 7443, int bodyLimit = 12 * 1024 * 1024) => NativeHttpsPlan.Create(new()
    {
        Enabled = true,
        PublicOrigin = $"https://localhost:{port}",
        CertificateSha256 = Leaf.GetCertHashString(HashAlgorithmName.SHA256),
        ServiceSid = "S-1-5-80-1-2-3-4-5",
        MaximumRequestBodyBytes = bodyLimit
    });

    public void Dispose() { Leaf.Dispose(); Root.Dispose(); }
}

internal sealed class SyntheticHttpsCertificateSource(X509Certificate2 certificate) : INativeHttpsCertificateSource
{
    public X509Certificate2 Load(NativeHttpsPlan plan)
    {
        if (!OperatingSystem.IsWindows()) { return new(certificate); }
        // Schannel needs a temporary persisted key; default import deletes it on disposal.
        // The PFX stays in memory and no certificate/trust store is changed.
        var pfx = certificate.Export(X509ContentType.Pkcs12);
        try { return new(pfx, (string?)null, X509KeyStorageFlags.DefaultKeySet); }
        finally { CryptographicOperations.ZeroMemory(pfx); }
    }
}
