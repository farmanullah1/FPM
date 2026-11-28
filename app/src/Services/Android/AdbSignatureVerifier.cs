using System.Security.Cryptography.X509Certificates;

namespace PCManager.Services.Android;

public interface IAdbSignatureVerifier
{
    bool IsGoogleSigned(string executablePath);
}

/// <summary>
/// Verifies the Authenticode signer is Google and the certificate chain builds. Revocation is
/// not checked online; the pinned package checksum is the separate transport check.
/// </summary>
public sealed class AuthenticodeAdbSignatureVerifier : IAdbSignatureVerifier
{
    private const string GoogleSubject = "O=Google LLC";

    public bool IsGoogleSigned(string executablePath)
    {
        try
        {
            using X509Certificate certificate = X509Certificate.CreateFromSignedFile(executablePath);
            using var signer = new X509Certificate2(certificate);

            if (!signer.Subject.Contains(GoogleSubject, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(signer);
        }
        catch
        {
            return false;
        }
    }
}
