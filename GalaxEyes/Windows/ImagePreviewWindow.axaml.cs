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

public partial class ImagePreviewWindow : Window, INotifyPropertyChanged
{
    public ImageAndPaletteFormat BaseFormat { get; set; }
    public ImageAndPaletteFormat BestFormat { get; set; }
    public ImagePreviewWindow(Image<Rgba32> baseImage, string baseImageFullPath, ImageAndPaletteFormat baseFormat, ImageAndPaletteFormat bestFormat)
    {
        BaseFormat = baseFormat;
        BestFormat = bestFormat;
        InitializeComponent();
        DataContext = this;
        Title = "Previewing changes for " + baseImageFullPath;
        BaseImage.Source = Util.ToAvaloniaBitmap(baseImage);
        BinaryStream baseStream = new BinaryStream();
        // TODO: Make something in NinTextures that will calculate the size of the image (so I don't have to encode it to see)
        var basePalette = NinTextures.Util.EncodeTexture(baseStream, baseImage, baseFormat.ImageFormat, baseFormat.PaletteFormat);
        NinTextures.Util.EncodePalette(baseStream, basePalette, baseFormat.PaletteFormat);
        BaseFormatSize.Text = baseStream.Length + " bytes";
        BinaryStream bestStream = new BinaryStream();
        var palette = NinTextures.Util.EncodeTexture(bestStream, baseImage, bestFormat.ImageFormat, bestFormat.PaletteFormat);
        var palettePos = bestStream.Position;
        NinTextures.Util.EncodePalette(bestStream, palette, bestFormat.PaletteFormat);
        BestFormatSize.Text = bestStream.Length + " bytes";
        bestStream.Position = palettePos;
        var newPalette = NinTextures.Util.DecodePalette(bestStream, palette.Count, bestFormat.PaletteFormat);
        bestStream.Position = 0;
        var newImage = NinTextures.Util.DecodeTexture(bestStream, baseImage.Width, baseImage.Height, bestFormat.ImageFormat, newPalette);
        NewImage.Source = Util.ToAvaloniaBitmap(newImage);
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
