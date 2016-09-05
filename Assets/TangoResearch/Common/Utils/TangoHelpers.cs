﻿using System.Collections;
using Tango;
using UnityEngine;

/// <summary>
/// Collection of helpers functions for tango data.
/// </summary>
public static class TangoHelpers {

    /// <summary>
    /// Gets the YUV color from an image.
    /// </summary>
    /// <param name="imageBuffer">The image buffer.</param>
    /// <param name="x">The x coordinate of the image.</param>
    /// <param name="y">The y coordinate of the image.</param>
    /// <returns>The unscaled YUV color. Each component ranges from 0 to 255. </returns>
    public static Vector3 GetYUV(TangoUnityImageData imageBuffer, int x, int y)
    {
        int width = (int)imageBuffer.width;
        int height = (int)imageBuffer.height;
        int size = width * height;

        int x_index = x;
        if (x % 2 != 0)
        {
            x_index = x - 1;
        }

        int Y = imageBuffer.data[(y * width) + x];
        int U = imageBuffer.data[size + ((y / 2) * width) + x_index + 1];
        int V = imageBuffer.data[size + ((y / 2) * width) + x_index];

        Vector3 result = new Vector3(Y, U, V);
        return result;
    }

    /// <summary>
    /// Converts an unscaled YUV to RGB.
    /// </summary>
    /// <param name="yuv">The unscaled (0-255) YUV.</param>
    /// <param name="gammaCorrect">if set to <c>true</c> [gamma correct].</param>
    /// <param name="scaleRGB">if set to <c>true</c> [scale RGB between 0-1].</param>
    /// <returns></returns>
    public static Vector3 YUVToRGB(Vector3 yuv, bool gammaCorrect = false, bool scaleRGB = true) {
        
        float r = yuv.x + (1.370705f * (yuv.z - 128));
        float g = yuv.x - (0.689001f * (yuv.z - 128)) - (0.337633f * (yuv.y - 128));
        float b = yuv.x + (1.732446f * (yuv.y - 128));

        Vector3 result = new Vector3(r, g, b);

        if (gammaCorrect)
        {
            result.x = Mathf.Pow(Mathf.Max(0.0f, result.x), 2.2f);
            result.y = Mathf.Pow(Mathf.Max(0.0f, result.y), 2.2f);
            result.z = Mathf.Pow(Mathf.Max(0.0f, result.z), 2.2f);
        }

        return (scaleRGB) ? result / 255f : result;
    }

}