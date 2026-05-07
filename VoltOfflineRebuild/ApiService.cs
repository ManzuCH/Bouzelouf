namespace volt_design.Api;

public sealed class ApiService
{
    public delegate void ResponseDelegate(string text, bool error);
    public delegate void AuthResponseDelegate(volt_design.Api.Entities.AuthResponse responseInstance, volt_design.Api.Entities.AuthResponse.ErrorCode error);
    public delegate void ConfigResponseDelegate(volt_design.Api.Entities.ConfigResponse.ErrorCode code);

    private readonly volt_design.Models.UserConfig _offlineConfig;
    private readonly string _artifactDirectory;

    public volt_design.EncryptedString host;
    public volt_design.EncryptedString version;
    public volt_design.EncryptedString productId;
    public volt_design.EncryptedString requestKey;
    public volt_design.EncryptedString responseKey;
    public volt_design.EncryptedString hwidRoute;
    public volt_design.EncryptedString credentialsRoute;
    public volt_design.EncryptedString saveConfigRoute;
    public volt_design.EncryptedString module;
    public volt_design.EncryptedString moduleB;

    public ApiService(
        volt_design.EncryptedString host,
        volt_design.EncryptedString version,
        volt_design.EncryptedString productId,
        volt_design.EncryptedString requestKey,
        volt_design.EncryptedString responseKey,
        volt_design.EncryptedString hwidRoute,
        volt_design.EncryptedString credentialsRoute,
        volt_design.EncryptedString saveConfigRoute,
        volt_design.EncryptedString module,
        volt_design.EncryptedString moduleB)
    {
        this.host = host;
        this.version = version;
        this.productId = productId;
        this.requestKey = requestKey;
        this.responseKey = responseKey;
        this.hwidRoute = hwidRoute;
        this.credentialsRoute = credentialsRoute;
        this.saveConfigRoute = saveConfigRoute;
        this.module = module;
        this.moduleB = moduleB;
        _offlineConfig = volt_design.Models.UserConfig.CreateOfflineDefault();
        _artifactDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "reconstructed_dlls_complete"));
    }

    public static ApiService CreateOffline()
    {
        return new ApiService(
            volt_design.Config.hostname,
            volt_design.Config.version,
            volt_design.Config.product_id,
            volt_design.Config.requestKey,
            volt_design.Config.responseKey,
            volt_design.Config.authHwidRoute,
            volt_design.Config.authCredentialsRoute,
            volt_design.Config.saveConfigRoute,
            volt_design.Config.module,
            volt_design.Config.moduleB);
    }

    public Task<volt_design.Api.Entities.AuthResponse> AuthCredentialsRequest(string username, string password)
    {
        return Task.FromResult(BuildLocalAuth(username));
    }

    public void AuthCredentialsRequest(string username, string password, string? twoFactorCode, AuthResponseDelegate? callback)
    {
        var response = BuildLocalAuth(username);
        callback?.Invoke(response, response.Code);
    }

    public Task<volt_design.Api.Entities.AuthResponse> AuthHwidRequest()
    {
        return Task.FromResult(BuildLocalAuth("local-hwid-disabled"));
    }

    public void AuthHwidRequest(string? token, AuthResponseDelegate? callback)
    {
        var response = BuildLocalAuth("local-hwid-disabled");
        callback?.Invoke(response, response.Code);
    }

    public Task<volt_design.Models.UserConfig> GetConfig()
    {
        return Task.FromResult(_offlineConfig);
    }

    public Task<volt_design.Api.Entities.ConfigResponse> SaveConfig(volt_design.Models.UserConfig config)
    {
        return Task.FromResult(new volt_design.Api.Entities.ConfigResponse
        {
            Code = volt_design.Api.Entities.ConfigResponse.ErrorCode.None,
            Message = "Config accepted locally; no remote save was used.",
            Config = config
        });
    }

    public Task<byte[]> GetModule(string moduleName)
    {
        var safeName = Path.GetFileName(moduleName);
        var path = Path.Combine(_artifactDirectory, safeName);
        return Task.FromResult(File.Exists(path) ? File.ReadAllBytes(path) : Array.Empty<byte>());
    }

    private static volt_design.Api.Entities.AuthResponse BuildLocalAuth(string username)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new volt_design.Api.Entities.AuthResponse
        {
            Code = volt_design.Api.Entities.AuthResponse.ErrorCode.None,
            Message = "Local development auth accepted without API/HWID.",
            Username = string.IsNullOrWhiteSpace(username) ? "local-dev" : username,
            apitoken = "LOCAL-DEV-TOKEN",
            prefix = "DEV",
            user_id = 1,
            client_timestamp = now,
            server_timestamp = now,
            offsets = volt_design.Api.Entities.LibOffsets.LocalDefault,
            userconfig = "{}",
            processedBuffer = Array.Empty<byte>(),
            processedAdditionalBuffer = Array.Empty<byte>()
        };
    }
}
