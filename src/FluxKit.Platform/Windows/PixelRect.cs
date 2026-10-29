namespace FluxKit.Platform.Windows;

/// <summary>A rectangle in physical screen pixels.</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;

    public PixelRect Inflate(int by) => new(Left - by, Top - by, Right + by, Bottom + by);
}
