using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace catprogram.Services;

public static class QrCodeRenderer
{
    public static ImageSource Render(string text, int moduleSize = 10, int quietZone = 4)
    {
        text ??= string.Empty;
        const int size = 21;
        bool[,] matrix = new bool[size, size];
        bool[,] reserved = new bool[size, size];

        DrawFinder(matrix, reserved, 0, 0);
        DrawFinder(matrix, reserved, size - 7, 0);
        DrawFinder(matrix, reserved, 0, size - 7);

        for (int i = 8; i < size - 8; i++)
        {
            matrix[6, i] = i % 2 == 0;
            matrix[i, 6] = i % 2 == 0;
            reserved[6, i] = true;
            reserved[i, 6] = true;
        }

        byte[] payload = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        int bitIndex = 0;
        int payloadIndex = 0;

        for (int y = size - 1; y >= 0; y--)
        {
            for (int x = size - 1; x >= 0; x--)
            {
                if (reserved[y, x])
                {
                    continue;
                }

                if (payloadIndex >= payload.Length)
                {
                    payloadIndex = 0;
                }

                int bit = (payload[payloadIndex] >> (bitIndex % 8)) & 1;
                matrix[y, x] = bit == 1;
                bitIndex++;
                if (bitIndex % 8 == 0)
                {
                    payloadIndex++;
                }
            }
        }

        int pixels = (size + quietZone * 2) * moduleSize;
        WriteableBitmap bmp = new(pixels, pixels, 96, 96, PixelFormats.Bgra32, null);
        int stride = bmp.BackBufferStride;
        byte[] buffer = new byte[stride * pixels];

        for (int y = 0; y < pixels; y++)
        {
            for (int x = 0; x < pixels; x++)
            {
                bool dark = IsDark(matrix, x, y, moduleSize, quietZone);
                int offset = y * stride + x * 4;
                buffer[offset + 0] = dark ? (byte)0x10 : (byte)0xF6;
                buffer[offset + 1] = dark ? (byte)0x1B : (byte)0xF6;
                buffer[offset + 2] = dark ? (byte)0x2B : (byte)0xF6;
                buffer[offset + 3] = 0xFF;
            }
        }

        bmp.WritePixels(new Int32Rect(0, 0, pixels, pixels), buffer, stride, 0);
        bmp.Freeze();
        return bmp;
    }

    private static bool IsDark(bool[,] matrix, int x, int y, int moduleSize, int quietZone)
    {
        int total = matrix.GetLength(0) + quietZone * 2;
        int mx = x / moduleSize - quietZone;
        int my = y / moduleSize - quietZone;
        if (mx < 0 || my < 0 || mx >= matrix.GetLength(1) || my >= matrix.GetLength(0))
        {
            return false;
        }

        return matrix[my, mx];
    }

    private static void DrawFinder(bool[,] matrix, bool[,] reserved, int startX, int startY)
    {
        for (int y = 0; y < 7; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                int mx = startX + x;
                int my = startY + y;
                if (mx < 0 || my < 0 || mx >= matrix.GetLength(1) || my >= matrix.GetLength(0))
                {
                    continue;
                }

                bool dark = x == 0 || x == 6 || y == 0 || y == 6 || (x >= 2 && x <= 4 && y >= 2 && y <= 4);
                matrix[my, mx] = dark;
                reserved[my, mx] = true;
            }
        }
    }
}
