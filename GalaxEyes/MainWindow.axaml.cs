using Avalonia.Controls;

namespace GalaxEyes;

public class OptimizerRow
{
    public string Label { get; set; } = "";
    public bool IsChecked { get; set; }
}

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        OptimizerList.Items.Add(new OptimizerRow { Label = "Vanilla File Check" });
        OptimizerList.Items.Add(new OptimizerRow { Label = "Audio Table Check" });
        OptimizerList.Items.Add(new OptimizerRow { Label = "AST Optimizer" });
        OptimizerList.Items.Add(new OptimizerRow { Label = "RARC Optimizer" });
        OptimizerList.Items.Add(new OptimizerRow { Label = "Collision Optimizer" });
        OptimizerList.Items.Add(new OptimizerRow { Label = "BTI Optimizer" });
        OptimizerList.Items.Add(new OptimizerRow { Label = "BTI Checker" });
        OptimizerList.Items.Add(new OptimizerRow { Label = "Riivolution Checker" });
        OptimizerList.Items.Add(new OptimizerRow { Label = "Galaxy Banner Checker" });
    }

    private void ScrollHandler(object? sender, Avalonia.Controls.Primitives.ScrollEventArgs e)
    {
    }
}