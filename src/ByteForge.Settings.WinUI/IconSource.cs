// Copyright (c) 2026 ByteForge
using System.IO;
using ByteForge.Core;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ByteForge.Settings.WinUI;

/// <summary>给 WinUI 页面提供一个解码到目标宽度的应用图标，避免把 1248x1248 大图硬缩到 40x40 发虚。</summary>
internal static class IconSource
{
    public static ImageSource? Get(int decodeWidth = 128)
    {
        try
        {
            string path = AppPaths.DisplayIconPath;
            if (!File.Exists(path)) return null;

            var uri = new System.Uri(path, System.UriKind.Absolute);
            return new BitmapImage(uri) { DecodePixelWidth = decodeWidth };
        }
        catch
        {
            return null;
        }
    }
}
