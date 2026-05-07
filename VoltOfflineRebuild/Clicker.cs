namespace volt_design.Clicker;

public enum ClickKind
{
    Left,
    Right
}

public sealed class Clicker
{
    private readonly volt_design.Clicker.Library.ClickerLibrary _library;
    private readonly volt_design.Models.ClickSettings _settings;

    public Clicker(
        volt_design.Clicker.Library.ClickerLibrary library,
        ClickKind kind,
        volt_design.Models.ClickSettings settings)
    {
        _library = library;
        Kind = kind;
        _settings = settings;
    }

    public ClickKind Kind { get; }
    public bool Enabled { get; private set; }

    public void Enable()
    {
        Enabled = true;
        _settings.Enabled = true;
    }

    public void Disable()
    {
        Enabled = false;
        _settings.Enabled = false;
    }

    public void TickOnce()
    {
        if (!Enabled)
        {
            return;
        }

        var delay = CalculateDelayMs(_settings.MinCps, _settings.MaxCps);
        if (Kind == ClickKind.Left)
        {
            _library.SendClick(delay);
            return;
        }

        _library.SendRightClick(delay);
    }

    private static int CalculateDelayMs(float minCps, float maxCps)
    {
        var cps = Math.Clamp((minCps + maxCps) / 2f, 1f, 100f);
        return (int)Math.Round(1000f / cps);
    }
}
