namespace volt_design.Models;

public sealed class UserConfig
{
    public sealed class UserSettings
    {
        public System.Windows.Forms.Keys[] slotsKeys = new System.Windows.Forms.Keys[9];
        public int lclick_sound;
        public int rclick_sound;
        public string customclicksound = "";
        public int clicksound_volume = 100;
        public bool cpsprecision = true;
        public bool ignorebindsinmenu;
        public bool advanced_destruct;
        public bool left_handed;
        public bool beepontoggle;
        public bool slotsDetection = true;
        public int beepmode;
        public int mcdetection_mode;
        public string mcdetection_search = "Minecraft";
        public System.Windows.Forms.Keys hide_key = volt_design.Config.defaultHideKeybind;
        public System.Windows.Forms.Keys destruct_key = volt_design.Config.defaultDestructKeybind;
        public System.Windows.Forms.Keys lautoclicker_key = volt_design.Config.defaultKeybind;
        public System.Windows.Forms.Keys rautoclicker_key = volt_design.Config.defaultRKeybind;
        public System.Windows.Forms.Keys tntmacros_key;
        public int tntmacros_mode;
        public System.Windows.Forms.Keys tntmacros_manualkey;
        public int tntmacros_tntslot;
        public int tntmacros_flintslot = 1;
        public bool tntmacros_backslot;
        public bool[] lautoclicker_slots = new bool[9];
        public bool[] rautoclicker_slots = new bool[9];

        public void syncLSlots(bool[] slots) => CopySlots(slots, lautoclicker_slots);

        public void syncRSlots(bool[] slots) => CopySlots(slots, rautoclicker_slots);

        private static void CopySlots(bool[] source, bool[] destination)
        {
            for (var i = 0; i < destination.Length && i < source.Length; i++)
            {
                destination[i] = source[i];
            }
        }
    }

    public UserProfile[] profiles = Array.Empty<UserProfile>();
    public Profile currentProfile = new();
    public UserSettings settings = new();

    public ClickSettings Left => ClickSettings.From(currentProfile.leftautoclicker);
    public ClickSettings Right => ClickSettings.From(currentProfile.rightautoclicker);
    public UserSettings Settings => settings;

    public int getProfilesCount() => profiles.Length;

    public void clean()
    {
        profiles ??= Array.Empty<UserProfile>();
        currentProfile ??= new Profile();
        settings ??= new UserSettings();
    }

    public static UserConfig CreateOfflineDefault()
    {
        var profile = new UserProfile
        {
            name = "Local dev",
            leftautoclicker = new Profile.AutoclickerSettings { cps = 11f },
            rightautoclicker = new Profile.AutoclickerSettings { cps = 14f }
        };

        return new UserConfig
        {
            profiles = new[] { profile },
            currentProfile = profile,
            settings = new UserSettings()
        };
    }
}

public sealed class ClickSettings
{
    public bool Enabled { get; set; }
    public float MinCps { get; set; }
    public float MaxCps { get; set; }
    public bool ClickSound { get; set; }
    public bool OnlyInGame { get; set; }

    public static ClickSettings From(Profile.AutoclickerSettings settings)
    {
        var cps = settings.maxcps > 0 ? settings.maxcps : settings.cps;
        return new ClickSettings
        {
            Enabled = false,
            MinCps = cps,
            MaxCps = cps,
            ClickSound = settings.clicksounds,
            OnlyInGame = settings.ignoreinmenus
        };
    }
}

public class UserProfile : Profile
{
    public string name = "";
    public DateTime createdAt = DateTime.Now;
    public DateTime updatedAt = DateTime.Now;
}

public class Profile
{
    public class AutoclickerSettings
    {
        public float maxcps = float.MinValue;
        public float cps = 11f;
        public int random_mode = 1;
        public int click_mode;
        public float jitterpower = 0.1f;
        public bool jitter;
        public bool ignoreinmenus;
        public bool clicksounds;
        public bool oflag;
        public bool slot;
        public bool spikes;
        public bool ignoreOnShift;
        public bool allowInMenusOnShift;
        public int spikesCps = 4;
        public int spikesDelay = 75;
        public bool randomization = true;
        public float randomizationCps = 2f;
        public bool simulateExhaust;

        public void syncToInstance(AutoclickerSettings s)
        {
            maxcps = float.MinValue;
            cps = s.maxcps > 0f && s.cps != 11f ? s.maxcps : s.cps;
            random_mode = s.random_mode;
            click_mode = s.click_mode;
            jitterpower = s.jitterpower;
            jitter = s.jitter;
            ignoreinmenus = s.ignoreinmenus;
            clicksounds = s.clicksounds;
            oflag = s.oflag;
            slot = s.slot;
            spikes = s.spikes;
            ignoreOnShift = s.ignoreOnShift;
            allowInMenusOnShift = s.allowInMenusOnShift;
            spikesCps = s.spikesCps;
            spikesDelay = s.spikesDelay;
            randomization = s.randomization;
            randomizationCps = s.randomizationCps;
            simulateExhaust = s.simulateExhaust;
        }
    }

    public AutoclickerSettings leftautoclicker = new();
    public AutoclickerSettings rightautoclicker = new();

    public void syncToProfile(Profile p)
    {
        leftautoclicker.syncToInstance(p.leftautoclicker);
        rightautoclicker.syncToInstance(p.rightautoclicker);
    }
}
