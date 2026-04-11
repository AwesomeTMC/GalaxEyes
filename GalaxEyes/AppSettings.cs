using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GalaxEyes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class FolderAttribute(string desc) : Attribute
{
    public string Title { get; } = desc;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class NameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

public enum ArchiveHandler
{
    HackIO = 0,
    JKRLib = 1
}

public partial class IgnoreEntry : ObservableObject
{
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [ObservableProperty] private string? _hash;
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [ObservableProperty] private string? _path;
    [ObservableProperty] private List<string> _inspectors = new();

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        MainSettings.TrySave();
    }
}

  
public partial class MainSettings : FileSettings<MainSettings>
{
    private static MainSettings? _instance;
    public static MainSettings Instance => _instance ??= Load();

    public static void TrySave()
    {
        if (_instance != null && !_instance.Loading)
        {
            _instance.Save();
        }
    }
    [JsonIgnore] public override string FileName => "main_settings.json";
    public string CurrentTheme { get => GetField("System"); set => SetField(value); }
    public ArchiveHandler ArchiveHandler { get => GetField(ArchiveHandler.JKRLib); set => SetField(value); }
    public string ModDirectory { get => GetField(""); set => SetField(value); }

    public ObservableCollection<IgnoreEntry> IgnoreEntries
    {
        get
        {
            var val = GetField<ObservableCollection<IgnoreEntry>?>(null);
            if (val == null)
            {
                val = new ObservableCollection<IgnoreEntry>();
                SetField(val);
            }
            return val;
        }
        set => SetField(value);
    }

    public void AddIgnoreEntry(IgnoreEntry entry)
    {
        lock (Lock)
        {
            IgnoreEntries.Add(entry);
        }
        Save();
    }
}

public interface IHaveInspectorSettings : IHaveSettings
{
    bool IsEnabled { get; set; }
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

    public string Name { get
        {
            var attr = GetAttribute<NameAttribute>();
            var defname = Util.CamelCaseSpace(_property.Name);

            return attr == null ? defname : attr.Name;
        } 
    }

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

    public T? GetAttribute<T>() where T : Attribute =>
        _property.GetCustomAttribute<T>(inherit: true);
}

public abstract partial class InspectorSettings<T> : FileSettings<T>, IHaveInspectorSettings
    where T : InspectorSettings<T>, new()
{
    public bool IsEnabled { get => GetField(true); set => SetField(value); }

    public new List<LiveSettingEntry> GetEditableEntries()
    {
        return this.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != nameof(IHaveSettings.FileName) && p.Name != nameof(IHaveInspectorSettings.IsEnabled) && p.Name != nameof(Loading))
            .Select(p => new LiveSettingEntry(p, this))
            .ToList();
    }
}

public abstract partial class FileSettings<T> : ObservableObject, IHaveSettings, IJsonOnDeserializing, IJsonOnDeserialized
    where T : FileSettings<T>, new()
{

    private readonly ConcurrentDictionary<string, object?> _settings = new();

    protected static readonly object Lock = new();

    [JsonIgnore]
    public bool Loading { get; private set; } = false;

    public void OnDeserializing()
    {
        Loading = true;
    }

    public void OnDeserialized()
    {
        Loading = false;
    }

    protected TVal GetField<TVal>(TVal defaultValue, [CallerMemberName] string key = "")
    {
        if (_settings.TryGetValue(key, out var val) && val is TVal typedVal)
            return typedVal;

        return defaultValue;
    }

    protected void SetField<TVal>(TVal value, [CallerMemberName] string key = "")
    {
        bool valueExists = _settings.TryGetValue(key, out var oldVal) && oldVal != null;

        if (valueExists && !EqualityComparer<TVal>.Default.Equals((TVal)oldVal, value) || !valueExists)
        {
            _settings[key] = value;
            OnPropertyChanged(key);
            Save();
        }
    }

    public abstract string FileName { get; }

    public string GetFilePath() { 
        string baseDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Settings");
        Directory.CreateDirectory(baseDir);
        return System.IO.Path.Combine(baseDir, FileName);
    }

    protected virtual void InitializeNew() { }

    public void Save()
    {
        if (Loading) return;
        lock (Lock)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(GetFilePath(), JsonSerializer.Serialize(this, GetType(), options));
        }
        
    }

    public static T Load()
    {
        lock (Lock)
        {
            T newSettings = new T();
            string path = newSettings.GetFilePath();
            if (File.Exists(path))
            {
                try
                {
                    T? serializedSettings = JsonSerializer.Deserialize<T>(File.ReadAllText(path));
                    if (serializedSettings != null)
                        return serializedSettings;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error while loading settings!!! {e.Message}");
                }
            }

            newSettings.InitializeNew();
            return newSettings;

        }
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
            .Where(p => p.Name != nameof(IHaveSettings.FileName) && p.Name != nameof(Loading))
            .Select(p => new LiveSettingEntry(p, this))
            .ToList();
    }
}