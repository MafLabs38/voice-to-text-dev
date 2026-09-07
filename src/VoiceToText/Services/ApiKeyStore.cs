using System.IO;
using System.Text;

namespace VoiceToText.Services;

public sealed class ApiKeyStore
{
    private readonly IDataProtector _protector;
    private readonly string _apiKeyFilePath;

    public ApiKeyStore() : this(new DpapiDataProtector(), AppPaths.ApiKeyFilePath)
    {
    }

    public ApiKeyStore(IDataProtector protector, string apiKeyFilePath)
    {
        _protector = protector;
        _apiKeyFilePath = apiKeyFilePath;
    }

    public bool HasApiKey() => File.Exists(_apiKeyFilePath);

    public string? LoadApiKey()
    {
        if (!File.Exists(_apiKeyFilePath))
        {
            return null;
        }

        var encrypted = File.ReadAllBytes(_apiKeyFilePath);
        var plainBytes = _protector.Unprotect(encrypted);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public void SaveApiKey(string apiKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_apiKeyFilePath)!);
        var plainBytes = Encoding.UTF8.GetBytes(apiKey);
        var encrypted = _protector.Protect(plainBytes);
        File.WriteAllBytes(_apiKeyFilePath, encrypted);
    }
}
