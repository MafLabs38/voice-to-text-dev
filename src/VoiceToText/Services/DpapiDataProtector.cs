using System.Security.Cryptography;

namespace VoiceToText.Services;

public sealed class DpapiDataProtector : IDataProtector
{
    public byte[] Protect(byte[] plainBytes) =>
        ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] encryptedBytes) =>
        ProtectedData.Unprotect(encryptedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
}
