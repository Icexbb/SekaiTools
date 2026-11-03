using System.Runtime.InteropServices;

namespace SekaiToolsCore.Match.TemplateMatcher;

internal enum EvidenceRegion
{
    Dialog,
    Banner,
    Marker
}

internal static class VisualChangeEvidence
{
    public static byte[] Sample(FrameMatchContext frame, EvidenceRegion region)
    {
        var image = frame.Gray;
        var width = region == EvidenceRegion.Marker ? Math.Max(1, image.Width / 3) : image.Width;
        var top = region == EvidenceRegion.Dialog ? image.Height * 7 / 10
            : region == EvidenceRegion.Banner ? image.Height * 2 / 5 : 0;
        var height = region == EvidenceRegion.Dialog ? image.Height - top : Math.Max(1, image.Height / 5);
        var sample = new byte[128];
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 16; x++)
        {
            var px = Math.Min(image.Width - 1, (2 * x + 1) * width / 32);
            var py = Math.Min(image.Height - 1, top + (2 * y + 1) * height / 16);
            sample[y * 16 + x] = Marshal.ReadByte(image.DataPointer, py * image.Step + px);
        }

        return sample;
    }

    public static bool HasChanged(byte[]? previous, byte[] current)
    {
        if (previous == null || previous.Length != current.Length) return false;
        var total = 0;
        var changed = 0;
        for (var i = 0; i < current.Length; i++)
        {
            var delta = Math.Abs(current[i] - previous[i]);
            total += delta;
            if (delta >= 24) changed++;
        }

        return total >= current.Length * 12 && changed >= 8;
    }
}