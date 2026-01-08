using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SkiaSharp;

namespace DownloadHDAvalonia.Services
{
    public class TreatmentAdjusts
    {
        public float linearSaturation { get; set; } = 1.0f;
        public float linearRed { get; set; } = 1.0f;
        public float linearBlue { get; set; } = 1.0f;
        public float exposure { get; set; } = 1.0f;
        public float contrast { get; set; } = 1.0f;
        public float brightness { get; set; } = 1.0f;
        public float sharpness { get; set; } = 1.0f;

        public bool HasAdjustments()
        {
            return linearSaturation != 1.0f ||
                    linearRed != 1.0f ||
                    linearBlue != 1.0f ||
                    exposure != 1.0f ||
                    contrast != 1.0f ||
                    brightness != 1.0f ||
                    sharpness != 1.0f;
        }
    }

    public static class ColorCorrection
    {
        public class ImageProcessor
        {
            private const float GAMMA = 2.2f;

            public static byte[] ProcessImage(byte[] source, TreatmentAdjusts adjusts)
            {
                if (source == null || source.Length == 0)
                    return source;

                if (!adjusts.HasAdjustments())
                    return source;

                using var input = new MemoryStream(source);
                using var skBitmap = SKBitmap.Decode(input);

                if (skBitmap == null)
                    return source;

                int width = skBitmap.Width;
                int height = skBitmap.Height;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];

                // Copia os pixels da imagem para um array
                Marshal.Copy(skBitmap.GetPixels(), pixels, 0, pixels.Length);

                // Processamento
                ProcessPixels(pixels, width, height, adjusts);

                // Cria novo bitmap com os pixels processados
                var outputBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                Marshal.Copy(pixels, 0, outputBitmap.GetPixels(), pixels.Length);

                // Exporta como JPEG (ou outro formato)
                using var outputStream = new MemoryStream();
                using (var image = SKImage.FromBitmap(outputBitmap))
                using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 100))
                {
                    data.SaveTo(outputStream);
                }

                return outputStream.ToArray();
            }

            private static void ProcessPixels(byte[] pixels, int width, int height, TreatmentAdjusts adjusts)
            {
                System.Threading.Tasks.Parallel.For(0, height, y =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width + x) * 4;

                        // Get RGB values (BGR order in memory)
                        float b = pixels[index] / 255.0f;
                        float g = pixels[index + 1] / 255.0f;
                        float r = pixels[index + 2] / 255.0f;
                        byte a = pixels[index + 3];

                        // Convert to linear space
                        r = (float)Math.Pow(r, GAMMA);
                        g = (float)Math.Pow(g, GAMMA);
                        b = (float)Math.Pow(b, GAMMA);

                        // Calculate mean and protection mask
                        float mean = (r + g + b) / 3.0f;
                        float protection = 1.0f - Math.Min(Math.Max((mean - 0.8f) / 0.2f, 0.0f), 1.0f);

                        // Apply chromatic correction with protection
                        float saturationEffectR = (r - mean) * adjusts.linearSaturation;
                        float saturationEffectG = (g - mean) * adjusts.linearSaturation;
                        float saturationEffectB = (b - mean) * adjusts.linearSaturation;

                        r = mean + (saturationEffectR * protection);
                        g = mean + (saturationEffectG * protection);
                        b = mean + (saturationEffectB * protection);

                        // Apply white balance
                        r *= adjusts.linearRed;
                        b *= adjusts.linearBlue;

                        // Convert back from linear space
                        r = (float)Math.Pow(Math.Min(Math.Max(r, 0.0f), 1.0f), 1.0f / GAMMA);
                        g = (float)Math.Pow(Math.Min(Math.Max(g, 0.0f), 1.0f), 1.0f / GAMMA);
                        b = (float)Math.Pow(Math.Min(Math.Max(b, 0.0f), 1.0f), 1.0f / GAMMA);

                        // Apply exposure
                        r *= adjusts.exposure;
                        g *= adjusts.exposure;
                        b *= adjusts.exposure;

                        // Apply contrast
                        r = ((r - 0.5f) * adjusts.contrast) + 0.5f;
                        g = ((g - 0.5f) * adjusts.contrast) + 0.5f;
                        b = ((b - 0.5f) * adjusts.contrast) + 0.5f;

                        // Apply brightness
                        float brightnessAdjust = (adjusts.brightness - 1.0f);
                        r += brightnessAdjust;
                        g += brightnessAdjust;
                        b += brightnessAdjust;

                        // Convert back to bytes with proper clamping
                        pixels[index] = (byte)(Math.Min(Math.Max(b * 255.0f, 0.0f), 255.0f));
                        pixels[index + 1] = (byte)(Math.Min(Math.Max(g * 255.0f, 0.0f), 255.0f));
                        pixels[index + 2] = (byte)(Math.Min(Math.Max(r * 255.0f, 0.0f), 255.0f));
                        pixels[index + 3] = a;
                    }
                });
            }
        }
    }
}




