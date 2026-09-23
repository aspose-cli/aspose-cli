using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Host.Updating;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class ReleaseManifestVerifierTests
{
    [Fact]
    public void VerifiesEphemeralP256SignatureAndArchiveHash()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string root = Directory.CreateTempSubdirectory("aspose-release-signature-").FullName;
        try
        {
            string archive = Path.Combine(root, "release.zip");
            File.WriteAllBytes(archive, Encoding.UTF8.GetBytes("release"));
            string archiveHash = Hash(archive);
            byte[] publicKey = key.ExportSubjectPublicKeyInfo();
            string keyId = Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
            byte[] payload = ReleaseManifestVerifier.CreateSigningPayload(
                "aspose-cli",
                "commercial",
                "win-x64",
                "1.2.3",
                new string('a', 40),
                "release.zip",
                new FileInfo(archive).Length,
                archiveHash,
                false,
                Links,
                "signed",
                "ECDSA-P256-SHA256",
                "rfc3279-der",
                keyId,
                "RELEASE-MANIFEST.sig");
            string manifest = Path.Combine(root, "RELEASE-MANIFEST.json");
            File.WriteAllText(
                manifest,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    productId = "aspose-cli",
                    edition = "commercial",
                    runtimeIdentifier = "win-x64",
                    artifactVersion = "1.2.3",
                    sourceRevision = new string('a', 40),
                    archive = new { path = "release.zip", size = new FileInfo(archive).Length, sha256 = archiveHash },
                    enginePackages = Links.Select(static link => new { product = link.Product, packageId = link.PackageId, version = link.Version, contentHash = link.ContentHash }),
                    buildDirty = false,
                    signature = new { status = "signed", algorithm = "ECDSA-P256-SHA256", format = "rfc3279-der", keyId, path = "RELEASE-MANIFEST.sig" },
                }),
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(root, "RELEASE-MANIFEST.sig"),
                Convert.ToBase64String(key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence)),
                Encoding.ASCII);

            ReleaseManifestInfo result = ReleaseManifestVerifier.Verify(
                manifest,
                new TrustedReleaseKeyRing([new TrustedReleaseKey(keyId, key.ExportSubjectPublicKeyInfoPem())]),
                archivePath: archive,
                expectedEdition: "commercial",
                expectedRuntimeIdentifier: "win-x64");

            Assert.Equal("1.2.3", result.ArtifactVersion);
            Assert.Equal(keyId, result.KeyId);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TamperedManifestAndArchiveAreRejected()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string root = Directory.CreateTempSubdirectory("aspose-release-tamper-").FullName;
        try
        {
            string archive = Path.Combine(root, "release.zip");
            File.WriteAllText(archive, "release", new UTF8Encoding(false));
            string hash = Hash(archive);
            string keyId = Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
            string manifest = Path.Combine(root, "RELEASE-MANIFEST.json");
            File.WriteAllText(manifest, Manifest(key, keyId, hash), Encoding.UTF8);
            File.WriteAllText(Path.Combine(root, "RELEASE-MANIFEST.sig"), Signature(key, keyId, hash, 7), Encoding.ASCII);

            File.AppendAllText(archive, "tampered", Encoding.UTF8);

            Assert.Throws<ReleaseVerificationException>(() => ReleaseManifestVerifier.Verify(
                manifest,
                new TrustedReleaseKeyRing([new TrustedReleaseKey(keyId, key.ExportSubjectPublicKeyInfoPem())]),
                archivePath: archive));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EmptyTrustRingIsExplicitlyUnavailable()
    {
        Assert.Equal(0, TrustedReleaseKeyRing.Empty.Count);
    }

    [Fact]
    public void OpenSslDerSignature_IsAcceptedByTheVerifier()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string root = Directory.CreateTempSubdirectory("aspose-release-openssl-").FullName;
        try
        {
            string? openssl = Environment.GetEnvironmentVariable("ASPOSE_CLI_OPENSSL_PATH")
                ?? "openssl.exe";
            string privateKey = Path.Combine(root, "key.pem");
            string payload = Path.Combine(root, "payload.txt");
            string signature = Path.Combine(root, "signature.bin");
            string archive = Path.Combine(root, "release.zip");
            string manifest = Path.Combine(root, "RELEASE-MANIFEST.json");
            File.WriteAllText(archive, "release", new UTF8Encoding(false));
            string archiveHash = Hash(archive);
            string keyId = KeyId(key);
            File.WriteAllText(privateKey, key.ExportPkcs8PrivateKeyPem(), Encoding.ASCII);
            byte[] signed = ReleaseManifestVerifier.CreateSigningPayload(
                "aspose-cli", "commercial", "win-x64", "1.2.3", new string('a', 40),
                "release.zip", 7, archiveHash, false, Links, "signed",
                "ECDSA-P256-SHA256", "rfc3279-der", keyId, "RELEASE-MANIFEST.sig");
            File.WriteAllBytes(payload, signed);
            File.WriteAllText(manifest, Manifest(key, keyId, archiveHash), Encoding.UTF8);

            var start = new ProcessStartInfo(openssl)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (string argument in new[] { "dgst", "-sha256", "-sign", privateKey, "-out", signature, payload })
            {
                start.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(start) ?? throw new InvalidOperationException("OpenSSL could not start.");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(process.StandardError.ReadToEnd());
            }
            byte[] der = File.ReadAllBytes(signature);
            Assert.Equal(0x30, der[0]);
            Assert.True(der.Length > 8);
            File.WriteAllText(
                Path.Combine(root, "RELEASE-MANIFEST.sig"),
                Convert.ToBase64String(der),
                Encoding.ASCII);
            ReleaseManifestVerifier.Verify(
                manifest,
                new TrustedReleaseKeyRing([new TrustedReleaseKey(keyId, key.ExportSubjectPublicKeyInfoPem())]),
                archivePath: archive);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return;
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TrustRingRejectsAKeyIdThatDoesNotMatchThePublicKey()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string root = Directory.CreateTempSubdirectory("aspose-release-ring-").FullName;
        try
        {
            string path = Path.Combine(root, "keys.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                keys = new[] { new { keyId = new string('0', 64), publicKeyPem = key.ExportSubjectPublicKeyInfoPem() } },
            }), Encoding.UTF8);
            string? previous = Environment.GetEnvironmentVariable(ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable, path);
                Assert.Throws<ReleaseVerificationException>(ReleaseManifestVerifier.LoadConfiguredKeyRing);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable, previous);
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TrustRingRejectsCaseVariantDuplicateKeyIdsAsStructuredFailure()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string root = Directory.CreateTempSubdirectory("aspose-release-ring-duplicate-").FullName;
        try
        {
            string keyId = KeyId(key);
            string path = Path.Combine(root, "keys.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                keys = new[]
                {
                    new { keyId, publicKeyPem = key.ExportSubjectPublicKeyInfoPem() },
                    new { keyId = keyId.ToUpperInvariant(), publicKeyPem = key.ExportSubjectPublicKeyInfoPem() },
                },
            }), Encoding.UTF8);
            string? previous = Environment.GetEnvironmentVariable(ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable, path);
                ReleaseVerificationException error = Assert.Throws<ReleaseVerificationException>(
                    ReleaseManifestVerifier.LoadConfiguredKeyRing);
                Assert.True(error.TrustedKeysConfigured);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable, previous);
            }

            Assert.Throws<ReleaseVerificationException>(() => new TrustedReleaseKeyRing(
            [
                new TrustedReleaseKey(keyId, "first"),
                new TrustedReleaseKey(keyId.ToUpperInvariant(), "second"),
            ]));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void MalformedPemAndOversizedTrustRingAreStructuredFailures()
    {
        string root = Directory.CreateTempSubdirectory("aspose-release-ring-malformed-").FullName;
        string? previous = Environment.GetEnvironmentVariable(ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable);
        try
        {
            string path = Path.Combine(root, "keys.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                keys = new[] { new { keyId = new string('0', 64), publicKeyPem = "not a PEM" } },
            }), Encoding.UTF8);
            Environment.SetEnvironmentVariable(ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable, path);
            Assert.Throws<ReleaseVerificationException>(ReleaseManifestVerifier.LoadConfiguredKeyRing);

            File.WriteAllText(path, "{\"keys\":[{\"keyId\":42,\"publicKeyPem\":true}]}", Encoding.UTF8);
            Assert.Throws<ReleaseVerificationException>(ReleaseManifestVerifier.LoadConfiguredKeyRing);

            File.WriteAllBytes(path, new byte[64 * 1024 + 1]);
            Assert.Throws<ReleaseVerificationException>(ReleaseManifestVerifier.LoadConfiguredKeyRing);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ReleaseManifestVerifier.TrustedKeyRingEnvironmentVariable, previous);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void OversizedManifestAndSignatureAreRejectedBeforeUnboundedReads()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string root = Directory.CreateTempSubdirectory("aspose-release-bounds-").FullName;
        try
        {
            string keyId = KeyId(key);
            var ring = new TrustedReleaseKeyRing(
                [new TrustedReleaseKey(keyId, key.ExportSubjectPublicKeyInfoPem())]);
            string manifest = Path.Combine(root, "RELEASE-MANIFEST.json");
            string signature = Path.Combine(root, "RELEASE-MANIFEST.sig");

            File.WriteAllBytes(manifest, new byte[64 * 1024 + 1]);
            Assert.Throws<ReleaseVerificationException>(() =>
                ReleaseManifestVerifier.Verify(manifest, ring, signature));

            File.WriteAllText(manifest, Manifest(key, keyId, new string('a', 64)), Encoding.UTF8);
            File.WriteAllBytes(signature, new byte[16 * 1024 + 1]);
            Assert.Throws<ReleaseVerificationException>(() =>
                ReleaseManifestVerifier.Verify(manifest, ring, signature));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string Manifest(ECDsa key, string keyId, string hash) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            productId = "aspose-cli",
            edition = "commercial",
            runtimeIdentifier = "win-x64",
            artifactVersion = "1.2.3",
            sourceRevision = new string('a', 40),
            archive = new { path = "release.zip", size = 7L, sha256 = hash },
            enginePackages = Links.Select(static link => new { product = link.Product, packageId = link.PackageId, version = link.Version, contentHash = link.ContentHash }),
            buildDirty = false,
            signature = new { status = "signed", algorithm = "ECDSA-P256-SHA256", format = "rfc3279-der", keyId, path = "RELEASE-MANIFEST.sig" },
        });

    private static string Signature(ECDsa key, string keyId, string hash, long size) =>
        Convert.ToBase64String(key.SignData(
            ReleaseManifestVerifier.CreateSigningPayload(
                "aspose-cli", "commercial", "win-x64", "1.2.3", new string('a', 40), "release.zip", size, hash,
                false, Links, "signed", "ECDSA-P256-SHA256", "rfc3279-der", keyId, "RELEASE-MANIFEST.sig"),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence));

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string KeyId(ECDsa key) =>
        Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo())).ToLowerInvariant();

    private static ReleaseEnginePackage[] Links =>
    [
        new("cells", "Test.cells", "1.0.0", Convert.ToBase64String(Enumerable.Repeat((byte)1, 64).ToArray())),
        new("pdf", "Test.pdf", "1.0.0", Convert.ToBase64String(Enumerable.Repeat((byte)2, 64).ToArray())),
        new("slides", "Test.slides", "1.0.0", Convert.ToBase64String(Enumerable.Repeat((byte)3, 64).ToArray())),
        new("words", "Test.words", "1.0.0", Convert.ToBase64String(Enumerable.Repeat((byte)4, 64).ToArray())),
    ];
}
