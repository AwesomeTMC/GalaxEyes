using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Binary_Stream;
using GalaxEyes.Inspectors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace GalaxEyes;

public class ImageDetails(Bitmap image, ImageAndPaletteFormat format, long size)
{
    public Bitmap AvaloniaImage = image;
    public ImageAndPaletteFormat Format = format;
    public long Size = size;
}

public partial class ImagePreviewWindow : Window, INotifyPropertyChanged
{
    public ImageAndPaletteFormat BaseFormat { get; set; }
    public ImageAndPaletteFormat BestFormat { get; set; }
    public ImagePreviewWindow(string baseImageFullPath, ImageDetails baseImage, ImageDetails bestImage)
    {
        BaseFormat = baseImage.Format;
        BestFormat = bestImage.Format;
        InitializeComponent();
        DataContext = this;
        Title = "Previewing changes for " + baseImageFullPath;
        BaseImage.Source = baseImage.AvaloniaImage;
        BaseFormatSize.Text = baseImage.Size + " bytes";
        BestFormatSize.Text = bestImage.Size + " bytes";
        NewImage.Source = bestImage.AvaloniaImage;
    }

    public void UseNewClicked(object sender, RoutedEventArgs e)
    {
        Close(true);
    }

    public void UseOldClicked(object sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
