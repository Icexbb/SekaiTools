using System.Text.Json;
using System.Security.Cryptography;
using DecoderProbe;
using Emgu.CV;
using Emgu.CV.CvEnum;
using PeriodicColorCaptureExperiment;
using SekaiToolsCore.Match.TemplateMatcher;

if (args.Length is not (3 or 4) || (args.Length == 4 && args[3] != "public-bind"))
    throw new ArgumentException("PeriodicColorCaptureProbe <video> <plugin> <new-report> [public-bind]");
var publicBinding = args.Length == 4;
var video = Path.GetFullPath(args[0]);
var report = Path.GetFullPath(args[2]);
static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
var inputHash = Hash(video);
if (File.Exists(report)) throw new IOException("Do not overwrite evidence");
CvInvoke.NumThreads = 4;
using var reference = new VideoCapture(video, VideoCapture.API.Ffmpeg, Tuple.Create((CapProp)70, 16));
var interval = Math.Max(1, (int)Math.Round(reference.Get(CapProp.Fps) / 5d));
using var owner = new ExperimentalPeriodicColorCapture(video, args[1], 16, interval, publicBinding);
var candidate = owner.Capture;
if (publicBinding) owner.VerifyFailedBindingPreservesCapture(video, 16, interval);
if (!reference.IsOpened || candidate.Get(CapProp.Fps) != reference.Get(CapProp.Fps) ||
    candidate.Get(CapProp.FrameCount) != reference.Get(CapProp.FrameCount))
    throw new InvalidDataException("Capture metadata differs");
using var referenceMatch = new FrameMatchContext();
using var candidateMatch = new FrameMatchContext();
using var color = new Mat();
using var expected = new Mat();
using var actual = new Mat();
var verified = 0;
var colorFrames = 0;
void VerifyFrame()
{
    if (!reference.Read(color) || !candidate.Read(actual)) throw new InvalidDataException("Read failed");
    var index = (int)reference.Get(CapProp.PosFrames);
    var isColor = index % interval == 0;
    if (actual.NumberOfChannels != (isColor ? 3 : 1)) throw new InvalidDataException("Wrong frame channels");
    if (isColor) { color.CopyTo(expected); colorFrames++; }
    else CvInvoke.CvtColor(color, expected, ColorConversion.Bgr2Gray);
    var pixels = BytePixelComparison.Compare(expected, actual);
    if (pixels.Maximum != 0 || candidate.Get(CapProp.PosFrames) != index ||
        candidate.Get(CapProp.PosMsec) != reference.Get(CapProp.PosMsec))
        throw new InvalidDataException($"Pixel or metadata difference at {index}");
    referenceMatch.Update(color);
    candidateMatch.Update(actual);
    if (BytePixelComparison.Compare(referenceMatch.Gray, candidateMatch.Gray).Maximum != 0)
        throw new InvalidDataException($"Recognition gray differs at {index}");
    verified++;
}
for (var i = 0; i < 96; i++) VerifyFrame();
var count = (int)reference.Get(CapProp.FrameCount);
var seeks = new[] { 0, interval - 2, interval - 1, interval, count / 4, count / 2, count * 3 / 4, count - 3 }
    .Select(p => Math.Max(0, p)).Distinct().ToArray();
foreach (var position in seeks)
{
    if (!reference.Set(CapProp.PosFrames, position) || !candidate.Set(CapProp.PosFrames, position))
        throw new InvalidDataException("Seek rejected");
    for (var i = 0; i < 3; i++) VerifyFrame();
}
if (reference.Read(color) || candidate.Read(actual)) throw new InvalidDataException("EOF differs");
// Keep an actual returned Mat alive while later callbacks reuse plugin storage.
// A clone alone would hide an incorrectly borrowed output header.
foreach (var position in new[] { 0, interval - 1 })
{
    using var retained = new Mat();
    if (!candidate.Set(CapProp.PosFrames, position) || !candidate.Read(retained))
        throw new InvalidDataException("Cannot obtain retained frame");
    using var snapshot = retained.Clone();
    for (var i = 0; i < interval * 2; i++)
    {
        if (!candidate.Read(actual)) throw new InvalidDataException("Cannot exercise callback storage reuse");
        if (BytePixelComparison.Compare(snapshot, retained).Maximum != 0)
            throw new InvalidDataException("Previously returned frame changed during subsequent capture");
    }
}
candidate.Dispose();
if (candidate.Ptr != IntPtr.Zero || colorFrames == 0) throw new InvalidDataException("Release or color coverage failed");
if (Hash(video) != inputHash) throw new InvalidDataException("Input changed during probe");
File.WriteAllText(report, JsonSerializer.Serialize(new { Video = video, InputHash = inputHash,
    owner.NativePath, owner.NativeHash, owner.PluginPath, owner.PluginHash,
    PublicCaptureBinding = publicBinding, FailedBindingsPreservedCapture = publicBinding,
    PreviewInterval = interval, FramesVerified = verified, ColorFramesVerified = colorFrames,
    SeekPoints = seeks.Length, RetainedColorAndGrayFramesUnchanged = interval > 1, CaptureDisposed = true, VerificationPassed = true,
    GuiPreviewQualified = false, NoPerformanceConclusion = true }, new JsonSerializerOptions { WriteIndented = true }));
