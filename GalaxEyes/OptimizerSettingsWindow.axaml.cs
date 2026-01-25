using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using GalaxEyes.Optimizers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace GalaxEyes;

public partial class OptimizerSettingsWindow : Window, INotifyPropertyChanged
{
    public OptimizerSettingsWindow(Optimizer associatedOptimizer)
    {
        AssociatedOptimizer = associatedOptimizer;
        InitializeComponent();
        DataContext = this;
        if (AssociatedOptimizer.Settings != null)
        {
            AllSettings.ItemsSource = AssociatedOptimizer.Settings.GetEditableEntries();
        }

    }

    public Optimizer AssociatedOptimizer { get; set; }
}