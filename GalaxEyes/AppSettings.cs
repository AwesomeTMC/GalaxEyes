using System.ComponentModel;
using System.Configuration;

namespace GalaxEyes;

public sealed class AppSettings : ApplicationSettingsBase, INotifyPropertyChanged
{
    private static AppSettings? _default;
    public static AppSettings Default => _default ??= new AppSettings();

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string ModDirectory
    {
        get => (string)this[nameof(ModDirectory)];
        set
        {
            if (!Equals(this[nameof(ModDirectory)], value))
            {
                this[nameof(ModDirectory)] = value;
                Save();
                PropertyChanged?.Invoke(this, new(nameof(ModDirectory)));
            }
        }
    }

    [UserScopedSetting]
    [DefaultSettingValue("System")]
    public string CurrentTheme
    {
        get => (string)this[nameof(CurrentTheme)];
        set
        {
            if (!Equals(this[nameof(CurrentTheme)], value))
            {
                this[nameof(CurrentTheme)] = value;
                Save();
                PropertyChanged?.Invoke(this, new(nameof(CurrentTheme)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
