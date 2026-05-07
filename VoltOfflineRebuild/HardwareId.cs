using System.Collections.ObjectModel;

namespace volt_design.Api.HardwareId;

public static class HWID
{
    public static string GetHardwareID() => "OFFLINE-HWID-PLACEHOLDER";

    public static string GetUniqueSerialId() => "OFFLINE-SERIAL-PLACEHOLDER";
}

public static class MAC
{
    public static IReadOnlyList<string> GetMACAddress()
    {
        return new ReadOnlyCollection<string>(new[] { "00-00-00-00-00-00" });
    }
}
