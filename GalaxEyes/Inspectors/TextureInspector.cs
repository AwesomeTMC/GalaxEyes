using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Binary_Stream;
using CommunityToolkit.Mvvm.ComponentModel;
using Hack.io.BCSV;
using Hack.io.KCL;
using Hack.io.Utility;
using jkr_lib;
using NinTextures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GalaxEyes.Inspectors
{
    public class ImageInformation
    {
        public bool HasTransparent = false;
        public bool IsGrayscale = true;
        public bool IsI4Grayscale = true;
        public bool IsA4 = true;
        public int UniqueColorCount = 0;

        public ImageInformation()
        {
            
        }

        public override string ToString() => "T: " + HasTransparent + ". Grayscale: " + IsGrayscale + ". Color Count: " + UniqueColorCount + ". I4: " + IsI4Grayscale + ". A4: " + IsA4;
    }

    public struct ImageAndPaletteFormat(ImageFormat imageFormat, PaletteFormat paletteFormat=PaletteFormat.RGB5A3)
    {
        public ImageFormat ImageFormat = imageFormat;
        public PaletteFormat PaletteFormat = paletteFormat;
        public override readonly string ToString()
        {
            var ret = ImageFormat.ToString();
            if (NinTextures.Util.IsPaletteTexture(ImageFormat))
            {
                ret += " (" + PaletteFormat + " palette)";
            }
            return ret;
        }
    }
    public partial class TextureSettings : FileSettings<TextureSettings>
    {
        [JsonIgnore] public override string FileName => "bti_settings.json";

        [ObservableProperty] private bool _lossySuggestions = true;
    }
    public class TextureInspector : Inspector
    {
        public TextureInspector() : base("Texture Inspector")
        {
        }
        public override TextureSettings Settings { get; } = TextureSettings.Load();

        public override List<Result> Check(String filePath)
        {
            List<Result> resultList = new List<Result>();

            var arc = Util.TryLoadArchive(ref resultList, filePath, InspectorName, () => { return Check(filePath); });
            if (arc == null)
                return resultList;

            foreach (var fnode in arc.FileNodes)
            {
                if (!fnode.Name.EndsWith(".tpl") && !fnode.Name.EndsWith(".bti"))
                    continue;
                var file = Util.TryLoadFileFromArc(arc, fnode.Name);
                if (file == null)
                {
                    Util.AddError(ref resultList, filePath, "Failed to load file from arc", InspectorName, () => { return Check(filePath); }, fnode.Name);
                    continue;
                }
                file.Endian = arc.Endian;
                Image<Rgba32> image;
                ImageFormat format;
                if (fnode.Name.EndsWith(".tpl"))
                {
                    // TODO: make this work with multiple images in the TPL...?
                    // The game doesn't use multiple images in a TPL, though
                    var tpl = new TPL(file);
                    var tplImg = tpl.Images[0];
                    if (tpl.Images.Count > 1)
                    {
                        Util.AddError(ref resultList, filePath, "TPL image count > 1. Checking this TPL is not currently supported", InspectorName, Util.NULL_ACTION, fnode.Name);
                        continue;
                    }
                    image = tplImg.Image;
                    format = tplImg.Format;
                }
                else // filename ends with bti
                {
                    var bti = new BTI(file);

                    image = bti.Image;
                    format = bti.Format;
                }
                var bestFormat = FindBestFormat(image, format);
                //Debug.WriteLine(" -> " + bestFormat + ". " + fnode.Name);
                

                if (format != bestFormat.ImageFormat)
                {
                    List<InspectorAction> actions = new()
                    {
                        new InspectorAction(() => { return ReEncodeTexture(filePath, fnode.Name, bestFormat);  }, "Re-encode texture"),
                        new InspectorAction(() => { return PreviewTextureReEncode(filePath, fnode.Name, bestFormat); }, "Re-encode texture (Preview)"),
                        new InspectorAction(Util.NULL_ACTION, "Ignore this once")
                    };
                    resultList.Add(new Result(ResultType.Optimize, filePath, "Different image encoding recommended", InspectorName, actions, fnode.Name + "\nFormat " + format + " -> " + bestFormat));
                }
            }

            return resultList;
        }

        private async Task<List<Result>> PreviewTextureReEncode(string arcPath, string fileName, ImageAndPaletteFormat bestFormat)
        {
            var thisFunc = () => { return PreviewTextureReEncode(arcPath, fileName, bestFormat); };
            List<Result> resultList = new List<Result>();

            var arc = Util.TryLoadArchive(ref resultList, arcPath, InspectorName, thisFunc);
            if (arc == null)
                return resultList;

            var fileNode = Util.TryLoadFileNodeFromArc(arc, fileName);
            if (fileNode == null)
            {
                Util.AddError(ref resultList, arcPath, "Failed to load file from arc", InspectorName, thisFunc, fileName);
                return resultList;
            }
            var file = new BinaryStream(new MemoryStream(fileNode.Data), arc.Endian);
            Image<Rgba32> baseImage;
            ImageAndPaletteFormat baseFormat;
            if (fileName.EndsWith(".tpl"))
            {
                var tpl = new TPL(file);
                baseImage = tpl.Images[0].Image;
                baseFormat.ImageFormat = tpl.Images[0].Format;
                baseFormat.PaletteFormat = tpl.Images[0].PaletteFormat;
            }
            else // filename ends with bti
            {
                var bti = new BTI(file);
                baseImage = bti.Image;
                baseFormat.ImageFormat = bti.Format;
                baseFormat.PaletteFormat = bti.PaletteFormat;
            }

            bool choice = await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var window = new ImagePreviewWindow(baseImage, arcPath + "/" + fileName, baseFormat, bestFormat);
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;
                    if (mainWindow == null)
                        return false;
                    return await window.ShowDialog<bool>(mainWindow);
                }
                return false;
                
            });

            if (choice)
            {
                return ReEncodeTexture(arcPath, fileName, bestFormat);
            }
            return new();
        }

        private List<Result> ReEncodeTexture(string arcPath, string fileName, ImageAndPaletteFormat bestFormat)
        {
            var thisFunc = () => { return Task.FromResult(ReEncodeTexture(arcPath, fileName, bestFormat)); };
            List<Result> resultList = new List<Result>();

            var arc = Util.TryLoadArchive(ref resultList, arcPath, InspectorName, thisFunc);
            if (arc == null)
                return resultList;

            var fileNode = Util.TryLoadFileNodeFromArc(arc, fileName);
            if (fileNode == null)
            {
                Util.AddError(ref resultList, arcPath, "Failed to load file from arc", InspectorName, thisFunc, fileName);
                return resultList;
            }
            var file = new BinaryStream(new MemoryStream(fileNode.Data), arc.Endian);

            BinaryStream outStrm = new BinaryStream(file.Endian);
            if (fileName.EndsWith(".tpl"))
            {
                var tpl = new TPL(file);
                var tplImg = tpl.Images[0];
                tplImg.Format = bestFormat.ImageFormat;
                tplImg.PaletteFormat = bestFormat.PaletteFormat;
                tpl.Write(outStrm);
            }
            else // filename ends with bti
            {
                var bti = new BTI(file);
                bti.Format = bestFormat.ImageFormat;
                bti.PaletteFormat = bestFormat.PaletteFormat;
                bti.Write(outStrm);
            }

            fileNode.SetFileData(outStrm.ToArray());
            Util.TrySaveArchive(ref resultList, arcPath, InspectorName, arc, thisFunc);
            return resultList;
        }

        public override bool DoCheck(string filePath)
        {
            if (!base.DoCheck(filePath))
                return false;
            if (!filePath.EndsWith(".arc"))
                return false;
            string relativePath = Path.GetRelativePath(MainSettings.Instance.ModDirectory, filePath).Replace("\\", "/");
            if (!relativePath.StartsWith("ObjectData") && !relativePath.StartsWith("LocalizeData") && !relativePath.StartsWith("LayoutData"))
                return false;
            return true;
        }

        

        private ImageInformation CalculateImageInformation(HashSet<Rgba32> uniqueColors)
        {
            ImageInformation info = new ImageInformation();
            foreach (Rgba32 color in uniqueColors) {
                if (color.A < 255)
                    info.HasTransparent = true;
                if (color.R != color.G || color.R != color.B)
                {
                    info.IsGrayscale = false;
                    info.IsI4Grayscale = false;
                }
                else if (color.R % 0x11 != 0)
                {
                    info.IsI4Grayscale = false;
                }
                if (color.A % 0x11 != 0)
                {
                    info.IsA4 = false;
                }
            }
            info.UniqueColorCount = uniqueColors.Count;
            return info;
        }

        private static Rgba32 ReformatPixel(Rgba32 color, ImageFormat format)
        {
            switch (format)
            {
                case ImageFormat.IA8:
                    return NinTextures.Util.ReformatPixel(color, PaletteFormat.IA8);
                case ImageFormat.RGB565:
                    return NinTextures.Util.ReformatPixel(color, PaletteFormat.RGB565);
                case ImageFormat.RGB5A3:
                    return NinTextures.Util.ReformatPixel(color, PaletteFormat.RGB5A3);
                default:
                    throw new NotImplementedException("Reformatting pixels to " + format + " isn't supported!");
            }
        }

        private static HashSet<Rgba32> ReformatUniqueColors(HashSet<Rgba32> uniqueColors, ImageFormat format)
        {
            HashSet<Rgba32> newColors = new();
            foreach (Rgba32 color in uniqueColors)
            {
                var newColor = ReformatPixel(color, format);
                if (!newColors.Contains(newColor))
                {
                    newColors.Add(newColor);
                }
            }
            return newColors;
        }

        private ImageAndPaletteFormat FindBestFormat(Image<Rgba32> img, ImageFormat format)
        {

            // We already have the most efficient format possible
            if (format == ImageFormat.I4 || format == ImageFormat.CMPR)
            {
                return new ImageAndPaletteFormat(format); ;
            }

            HashSet<Rgba32> uniqueColors = NinTextures.Util.GetUniqueColorsSet(img);
            ImageInformation info = CalculateImageInformation(uniqueColors);
            Debug.Write(info.ToString() + ". " + format);

            // For RGBA32: Lossy but worth it
            // For C14X2: Already uses up 16 (14, 2 not used) bits per color plus palette data at the end, this will always shrink the file size with no loss
            if ((Settings.LossySuggestions && format == ImageFormat.RGBA32) || format == ImageFormat.C14X2)
            {
                if (info.IsGrayscale)
                    format = ImageFormat.IA8;
                else if (info.HasTransparent)
                    format = ImageFormat.RGB5A3;
                else
                    format = ImageFormat.RGB565;

                uniqueColors = ReformatUniqueColors(uniqueColors, format);
                info.UniqueColorCount = uniqueColors.Count();
            }

            // 4-bit encoding. Very storage efficient but limited grayscale with no transparency
            if (info.IsI4Grayscale && !info.HasTransparent)
                return new ImageAndPaletteFormat(ImageFormat.I4);

            // 4-bit encoding with palette. Palettes use up more space at the end than I4s.
            if (info.UniqueColorCount <= 16)
                return new ImageAndPaletteFormat(ImageFormat.C4, MakePaletteFormat(info));

            // 8-bit IA4 encoding. Limited grayscale and alpha.
            if (info.IsI4Grayscale && info.IsA4)
                return new ImageAndPaletteFormat(ImageFormat.IA4);

            // 8-bit I8 encoding. Grayscale with no transparency.
            if (info.IsGrayscale && !info.HasTransparent)
                return new ImageAndPaletteFormat(ImageFormat.I8);

            // 8-bit C8 encoding with palette. Ideal for a lot of circumstances.
            if (info.UniqueColorCount <= 256)
                return new ImageAndPaletteFormat(ImageFormat.C8, MakePaletteFormat(info));

            // Stick with the old format otherwise. No need for unnecessary noise
            return new ImageAndPaletteFormat(format);
        }

        PaletteFormat MakePaletteFormat(ImageInformation info)
        {
            if (info.IsGrayscale)
                return PaletteFormat.IA8;
            else if (info.HasTransparent)
                return PaletteFormat.RGB5A3;
            else
                return PaletteFormat.RGB565;
        }
    }
}
