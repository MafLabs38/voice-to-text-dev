namespace VoiceToText.Services;

public interface IDataProtector
{
    byte[] Protect(byte[] plainBytes);
    byte[] Unprotect(byte[] encryptedBytes);
}
