using Avalonia;
using Avalonia.Controls.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GalaxEyes;

public partial class MainSettings : FileSettings<MainSettings>
{
    private static MainSettings? _instance;
    public static MainSettings Instance => _instance ??= Load();
    [JsonIgnore] public override string FileName => "main_settings.json";

    [ObservableProperty] private string _currentTheme = "System";
    [ObservableProperty] private string _modDirectory = "";
}

public interface IHaveSettings
{
    string FileName { get; }
    void Save();
    List<LiveSettingEntry> GetEditableEntries();
}

public partial class LiveSettingEntry : ObservableObject
{
    private readonly PropertyInfo _property;
    private readonly object _owner;

    public string Name => _property.Name;

    public object? Value
    {
        get => _property.GetValue(_owner);
        set
        {
            try
            {
                var convertedValue = Convert.ChangeType(value, _property.PropertyType);
                _property.SetValue(_owner, convertedValue);

                OnPropertyChanged(nameof(Value));
                (_owner as IHaveSettings)?.Save();
            }
            catch (Exception)
            {
                Debug.WriteLine("Invalid value entered, this shouldn't be printed.");
            }
        }
    }

    public LiveSettingEntry(PropertyInfo property, object owner)
    {
        _property = property;
        _owner = owner;
    }
}

public abstract partial class FileSettings<T> : ObservableObject, IHaveSettings
    where T : FileSettings<T>, new()
{
    public abstract string FileName { get; }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(FileName, JsonSerializer.Serialize(this, GetType(), options));
    }

    public static T Load()
    {
        string path = new T().FileName;
        if (!File.Exists(path)) return new T();

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? new T();
        }
        catch { return new T(); }
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Save();
    }
    public List<LiveSettingEntry> GetEditableEntries()
    {
        return this.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != nameof(IHaveSettings.FileName))
            .Select(p => new LiveSettingEntry(p, this))
            .ToList();
    }
}