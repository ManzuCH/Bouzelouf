namespace volt_design;

public static class Config
{
    public static float defaultLMin;
    public static float defaultLMax;
    public static float defaultRMin;
    public static float defaultRMax;
    public static System.Windows.Forms.Keys defaultKeybind;
    public static System.Windows.Forms.Keys defaultRKeybind;
    public static System.Windows.Forms.Keys defaultHideKeybind;
    public static System.Windows.Forms.Keys defaultDestructKeybind;
    public static EncryptedString version = new("local-dev");
    public static EncryptedString updateDate = new("local");
    public static EncryptedString product_id = new("volt-local-dev");
    public static EncryptedString requestKey = new("disabled");
    public static EncryptedString responseKey = new("disabled");
    public static EncryptedString hostname = new("local://disabled");
    public static EncryptedString authCredentialsRoute = new("/local/auth/credentials");
    public static EncryptedString authHwidRoute = new("/local/auth/hwid");
    public static EncryptedString saveConfigRoute = new("/local/config/save");
    public static EncryptedString module = new("VoltLib_from_Anydesk_dump.dll");
    public static EncryptedString moduleB = new("volt-wrapper_from_Anydesk_dump.dll");
    public static long x;
    public static long y;

    public static float DefaultLMin => defaultLMin;
    public static float DefaultLMax => defaultLMax;
    public static float DefaultRMin => defaultRMin;
    public static float DefaultRMax => defaultRMax;
    public static EncryptedString Version => version;
    public static EncryptedString ProductId => product_id;
    public static EncryptedString Hostname => hostname;

    public static void InitOfflineDefaults()
    {
        defaultLMin = 9f;
        defaultLMax = 12f;
        defaultRMin = 13f;
        defaultRMax = 16f;
        defaultKeybind = System.Windows.Forms.Keys.None;
        defaultRKeybind = System.Windows.Forms.Keys.None;
        defaultHideKeybind = System.Windows.Forms.Keys.None;
        defaultDestructKeybind = System.Windows.Forms.Keys.None;
        version = new EncryptedString("local-dev");
        updateDate = new EncryptedString(DateTime.UtcNow.ToString("yyyy-MM-dd"));
        product_id = new EncryptedString("volt-local-dev");
        requestKey = new EncryptedString("disabled");
        responseKey = new EncryptedString("disabled");
        hostname = new EncryptedString("local://disabled");
        authCredentialsRoute = new EncryptedString("/local/auth/credentials");
        authHwidRoute = new EncryptedString("/local/auth/hwid");
        saveConfigRoute = new EncryptedString("/local/config/save");
        module = new EncryptedString("VoltLib_from_Anydesk_dump.dll");
        moduleB = new EncryptedString("volt-wrapper_from_Anydesk_dump.dll");
        x = 0;
        y = 0;
    }
}

public sealed class EncryptedString
{
    public EncryptedString(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
