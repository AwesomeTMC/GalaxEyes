using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using System.Collections.Generic;
using System.Diagnostics;

namespace GalaxEyes;

public class SettingTemplateSelector : IDataTemplate
{
    [Content]
    public Dictionary<string, IDataTemplate> Templates { get; } = new();

    public Control? Build(object? param)
    {
        if (param is not LiveSettingEntry entry) return null;

        string typeName = entry.Value.GetType().Name;
        if (Templates.TryGetValue(typeName, out var template))
        {
            return template.Build(param);
        }

        return Templates["String"].Build(param);
    }

    public bool Match(object? data) => data is LiveSettingEntry;
}