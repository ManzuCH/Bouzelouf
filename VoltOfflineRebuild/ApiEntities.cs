namespace volt_design.Api.Entities;

public sealed class AuthResponse
{
    public enum ErrorCode
    {
        None = 0,
        Credentials = 1,
        HwidResetTime = 2,
        Version = 3,
        ApiFormat = 4,
        Unknown = 5,
        ServerConnection = 6,
        LibraryContent = 7,
        LibraryHeaders = 8,
        LibraryOffsets = 9,
        LibraryInjection = 10,
        Purchase = 98
    }

    public int error;
    public string apitoken = "";
    public string username = "";
    public string prefix = "";
    public int user_id;
    public long client_timestamp;
    public long server_timestamp;
    public string buffer = "";
    public string additionalBuffer = "";
    public LibOffsets offsets = LibOffsets.LocalDefault;
    public string userconfig = "";
    public byte[] processedBuffer = Array.Empty<byte>();
    public byte[] processedAdditionalBuffer = Array.Empty<byte>();

    public ErrorCode Code
    {
        get => Enum.IsDefined(typeof(ErrorCode), error) ? (ErrorCode)error : ErrorCode.Unknown;
        init => error = (int)value;
    }

    public string Message { get; init; } = "";
    public string Username
    {
        get => username;
        init => username = value;
    }
}

public sealed class ConfigResponse
{
    public enum ErrorCode
    {
        None = 0,
        ApiToken = 1,
        ServerConnection = 2,
        InvalidProduct = 3,
        ApiFormat = 4,
        ServerError = 666,
        OutdatedVersion = 98,
        ConfigFormat = 1337
    }

    public ErrorCode error;
    public ErrorCode Code { get => error; init => error = value; }
    public string Message { get; init; } = "";
    public volt_design.Models.UserConfig Config { get; init; } = volt_design.Models.UserConfig.CreateOfflineDefault();
}

public sealed class LibOffsets
{
    public long isOnAWhiteListedSlot;
    public long setTargetWindow;
    public long getCurrentSlot;
    public long dettach;
    public long attach;
    public long sendClick;
    public long setWhitelist;
    public long getInGame;
    public long getWhitelist;
    public long setCurrentSlot;
    public long setSlotKeybind;
    public long sendRightClick;
    public long setInGameFlag;
    public long sendBreakBlockClick;
    public long isClicking;
    public long oneClick;
    public long selfDestruct;
    public long boxMuller;

    public static LibOffsets Empty { get; } = new();
    public static LibOffsets LocalDefault { get; } = new();
}
