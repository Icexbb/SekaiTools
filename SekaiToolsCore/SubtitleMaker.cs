using System.Drawing;
using System.Text;
using SekaiToolsCore.Process;
using SekaiToolsCore.Story.Event;
using SekaiToolsCore.SubStationAlpha;
using SekaiToolsCore.SubStationAlpha.AssDraw;
using SekaiToolsCore.SubStationAlpha.Tag;
using SekaiToolsCore.SubStationAlpha.Tag.Modded;
using SubtitleEvent = SekaiToolsCore.SubStationAlpha.Event;

namespace SekaiToolsCore;

public class SubtitleMaker(VideoInfo videoInfo, TemplateManager templateManager, TypewriterSetting typewriterSetting)
{
    #region Dialog

    private GaMat GetNameTag(string name) => new(templateManager.GetEbTemplate(name));

    private static Queue<char> FormatDialogBodyArr(string body)
    {
        var bodyCopy = body
            .Replace("…", "...")
            .Replace("... ...", "......")
            .Replace("\\N", "\n").Replace("\\n", "\n");
        var lineCount = bodyCopy.Count(t => t == '\n');
        if (lineCount == 2) bodyCopy = bodyCopy.Replace("\n", "");
        var queue = new Queue<char>();
        foreach (var c in bodyCopy) queue.Enqueue(c);
        return queue;
    }

    private string MakeDialogTypewriter(string body)
    {
        var queue = FormatDialogBodyArr(body);
        var fadeTime = typewriterSetting.FadeTime;
        var charTime = typewriterSetting.CharTime;
        if (fadeTime <= 0 && charTime <= 0)
            return string.Join("", queue);

        var sb = new StringBuilder();
        sb.Append(queue.Dequeue());

        var nextStart = 0;
        foreach (var s in queue)
        {
            var ft = fadeTime / (char.IsAscii(s) ? 2 : 1);
            var ct = charTime / (char.IsAscii(s) ? 2 : 1);

            var start = nextStart + (s == '\n' ? 300 : 0);
            var alphaTag = $@"{{\alphaFF\t({start},{start + ft},1,\alpha0)}}";
            sb.Append(alphaTag);
            sb.Append(s == '\n' ? "\\N" : s);
            nextStart = start + ct;
        }

        return sb.ToString();
    }

    private string MakeDialogTypewriter(string body, int frameCount)
    {
        var queue = FormatDialogBodyArr(body);
        var fadeTime = typewriterSetting.FadeTime;
        var charTime = typewriterSetting.CharTime;
        if (fadeTime <= 0 && charTime <= 0)
            return string.Join("", queue);

        var nowTime = (int)(1000 / videoInfo.Fps.Fps() * frameCount);
        var charTimeEnd = 0;
        var sb = new StringBuilder();
        sb.Append(queue.Dequeue());
        while (queue.Count != 0)
        {
            var s = queue.Dequeue();
            var ft = fadeTime / (char.IsAscii(s) ? 2 : 1);
            var ct = charTime / (char.IsAscii(s) ? 2 : 1);

            charTimeEnd += ct;
            charTimeEnd += s == '\n' ? 300 : 0;

            int alphaPercent;
            if (nowTime <= charTimeEnd - ft)
                alphaPercent = 100;
            else if (nowTime < charTimeEnd)
                alphaPercent = (charTimeEnd - nowTime) * 100 / ft;
            else
                alphaPercent = 0;

            var alphaTag = $@"{{\alpha{Convert.ToString((int)(255 * alphaPercent / 100.0), 16).ToUpper()}}}";
            if (alphaPercent != 0) sb.Append(alphaTag);
            sb.Append(s == '\n' ? "\\N" : s);
            if (alphaPercent == 100) break;
        }

        foreach (var s in queue) sb.Append(s == '\n' ? "\\N" : s);

        return sb.ToString();
    }

    private List<Style> MakeDialogStyles()
    {
        var fontsize = (int)((videoInfo.FrameRatio > 16.0 / 9
            ? videoInfo.Resolution.Height * 0.043
            : videoInfo.Resolution.Width * 0.024) * (70 / 61D));

        var outlineSize = (int)Math.Ceiling(fontsize / 15.0);
        var marginV = _nameTagPosition.Y + (int)(fontsize * 2.3);
        var marginH = _nameTagPosition.X + (int)(fontsize * 0.4);

        var charaFontsize = (int)(fontsize * 0.9);
        var charaOutlineSize = (int)Math.Ceiling(charaFontsize / 15.0);
        const string fontName = "思源黑体 CN Bold";

        var blackColor = new AlphaColor(0, 255, 255, 255);
        var outlineColor = new AlphaColor(50, 73, 71, 102);
        var result = new List<Style>
        {
            new("Line1", fontName, fontsize,
                primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7, marginL: marginH, marginR: marginH, marginV: marginV),

            new("Line2", fontName, fontsize,
                primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7, marginL: marginH, marginR: marginH,
                marginV: marginV + (int)(fontsize * 1.01)),

            new("Line3", fontName, fontsize,
                primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7, marginL: marginH, marginR: marginH,
                marginV: marginV + (int)(fontsize * 1.01 * 2)),

            new("Character", fontName, charaFontsize,
                primaryColour: blackColor, outlineColour: outlineColor,
                outline: charaOutlineSize, shadow: 0, alignment: 7),

            new("Screen", fontName, charaFontsize,
                primaryColour: blackColor, outlineColour: outlineColor,
                outline: outlineSize, shadow: 0, alignment: 7)
        };

        return result;
    }

    private List<SubtitleEvent> MakeDialogEvents(List<DialogFrameSet> dialogList)
    {
        var result = new List<SubtitleEvent>();

        var dialogIndex = 0;
        foreach (var set in dialogList)
        {
            dialogIndex++;
            var dialogEvents = new List<SubtitleEvent>();
            var dialogMarker = $"-----  {dialogIndex:000}  -----";
            dialogEvents.Add(SubtitleEvent.Comment($"{dialogMarker}  Start",
                set.StartTime(), set.EndTime(), "Screen"));

            if (set.UseSeparator)
            {
                var items = SeparateDialogSet(set);
                dialogEvents.Add(SubtitleEvent.Comment($"{dialogMarker}  Line 1 ↓",
                    set.StartTime(), set.EndTime(), "Screen"));

                dialogEvents.AddRange(GenerateDialogEvent(items[0]));

                dialogEvents.Add(SubtitleEvent.Comment($"{dialogMarker}  Line 2 ↓",
                    set.StartTime(), set.EndTime(), "Screen"));

                dialogEvents.AddRange(GenerateDialogEvent(items[1]));
            }
            else
            {
                if (set.Data.BodyTranslated.LineCount() == 3)
                    set.Data.SetTranslationContent(set.Data.BodyTranslated.TrimAll());
                dialogEvents.AddRange(GenerateDialogEvent(set));
            }

            if (dialogEvents.Count > 3)
            {
                dialogEvents.Add(SubtitleEvent.Comment($"{dialogMarker}  Debug ↓",
                    set.StartTime(), set.EndTime(), "Screen"));
                var t = GenerateNoneJitterDialogEvents(set)
                    .Select(item => item.ToComment()).ToList();
                dialogEvents.AddRange(t);
            }

            dialogEvents.Add(SubtitleEvent.Comment($"{dialogMarker}  End",
                set.StartTime(), set.EndTime(), "Screen"));

            result.AddRange(dialogEvents);
        }

        return result;


        List<DialogFrameSet> SeparateDialogSet(DialogFrameSet dialogFrameSet)
        {
            var sepCount = dialogFrameSet.Separate.SeparateFrame - dialogFrameSet.StartIndex();

            var sepSet1 = new DialogFrameSet((Dialog)dialogFrameSet.Data.Clone(), videoInfo.Fps);
            var sepSet2 = new DialogFrameSet((Dialog)dialogFrameSet.Data.Clone(), videoInfo.Fps);

            sepSet1.Frames.AddRange(dialogFrameSet.Frames[..sepCount]);
            sepSet2.Frames.AddRange(dialogFrameSet.Frames[sepCount..]);

            var content = dialogFrameSet.Data.FinalContent.TrimAll();
            sepSet1.Data.BodyTranslated = content[..dialogFrameSet.Separate.SeparatorContentIndex];
            sepSet2.Data.BodyTranslated = content[dialogFrameSet.Separate.SeparatorContentIndex..];

            return [sepSet1, sepSet2];
        }

        IEnumerable<SubtitleEvent> GenerateDialogEvent(DialogFrameSet set)
        {
            var subtitleEventItems = new List<SubtitleEvent>();
            subtitleEventItems.AddRange(set.IsJitter
                ? GenerateJitterDialogEvents(set)
                : GenerateNoneJitterDialogEvents(set));
            return subtitleEventItems;
        }

        IEnumerable<SubtitleEvent> GenerateNoneJitterDialogEvents(DialogFrameSet dialogFrameSet)
        {
            var content = dialogFrameSet.Data.FinalContent;
            var characterName = dialogFrameSet.Data.FinalCharacter;
            var originLineCount = dialogFrameSet.Data.BodyOriginal.Split("\n").Length;
            var styleName = "Line" + originLineCount;

            var startTime = dialogFrameSet.StartTime();
            var endTime = dialogFrameSet.EndTime();

            var body = MakeDialogTypewriter(content);

            var dialogItem = SubtitleEvent.Dialog(body, startTime, endTime, styleName);

            var characterItemPosition =
                dialogFrameSet.Start().Point +
                new Size(GetNameTag(dialogFrameSet.Data.CharacterOriginal).Size.Width + 10, 0);
            var characterItemPositionTag = $@"{{\pos({characterItemPosition.X},{characterItemPosition.Y})}}";
            var characterItem = SubtitleEvent.Dialog(
                characterItemPositionTag + characterName, startTime, endTime, "Character");
            // if (characterName == "") characterItem = characterItem.ToComment();

            return [characterItem, dialogItem];
        }

        IEnumerable<SubtitleEvent> GenerateJitterDialogEvents(DialogFrameSet dialogFrameSet)
        {
            var content = dialogFrameSet.Data.FinalContent;
            var characterName = dialogFrameSet.Data.FinalCharacter;
            var originLineCount = dialogFrameSet.Data.BodyOriginal.Split("\n").Length;

            var styleName = "Line" + originLineCount;
            var styles = MakeDialogStyles();
            var style = styles.Find(s => s.Name == styleName)!;

            var constPosition = dialogFrameSet.Start().Point;
            var lastPosition = new Point(0, 0);
            var dialogEvents = new List<SubtitleEvent>();
            var characterEvents = new List<SubtitleEvent>();
            foreach (var frame in dialogFrameSet.Frames)
            {
                var x = style.MarginL;
                var y = style.MarginV;
                x += frame.Point.X - constPosition.X;
                y += frame.Point.Y - constPosition.Y;
                var body = @$"{{\pos({x},{y})}}"
                           + MakeDialogTypewriter(content, frame.Index - dialogFrameSet.StartIndex());

                if (lastPosition.X == x && lastPosition.Y == y && body == dialogEvents[^1].Text)
                {
                    dialogEvents[^1].End = frame.EndTime();
                }
                else
                {
                    dialogEvents.Add(SubtitleEvent.Dialog(body, frame.StartTime(), frame.EndTime(), styleName));
                }

                if (lastPosition.X == x && lastPosition.Y == y && body == characterEvents[^1].Text)
                {
                    characterEvents[^1].End = frame.EndTime();
                }
                else
                {
                    var offset = GetNameTag(dialogFrameSet.Data.CharacterOriginal).Size.Width;
                    var position = frame.Point + new Size(offset + 10, 0);
                    var tag = $@"{{\pos({position.X},{position.Y})}}";

                    var characterItem = SubtitleEvent.Dialog(
                        tag + characterName, frame.StartTime(), frame.EndTime(), "Character");
                    // if (characterName == "") characterItem = characterItem.ToComment();
                    characterEvents.Add(characterItem);
                }

                lastPosition = new Point(x, y);
            }

            var returnVal = new List<SubtitleEvent>();
            returnVal.AddRange(dialogEvents);
            returnVal.AddRange(characterEvents);
            return returnVal;
        }
    }

    #endregion

    #region Banner

    private List<SubtitleEvent> MakeBannerEvents(List<BannerFrameSet> bannerList)
    {
        var result = new List<SubtitleEvent>();
        var count = 0;
        foreach (var set in bannerList)
        {
            count++;

            var events = new List<SubtitleEvent>();
            var markerString = $"-----  {count:000}  -----";
            events.Add(SubtitleEvent.Comment($"{markerString}  Start", set.StartTime(), set.EndTime(), "Screen"));
            events.AddRange(GenerateBannerEvent(set));
            events.Add(SubtitleEvent.Comment($"{markerString}  End", set.StartTime(), set.EndTime(), "Screen"));
            result.AddRange(events);
        }

        return result;

        IEnumerable<SubtitleEvent> GenerateBannerEvent(BannerFrameSet set)
        {
            var offset = templateManager.DbTemplateMaxSize().Height;
            var center = videoInfo.Resolution.Center();
            center.Y += (int)(offset * 2.5);
            center.Y = center.Y / 20 * 20;
            var content = set.Data.FinalContent;
            var startTime = set.StartTime();
            var endTime = set.EndTime();

            var maskFade = Tags.Fade(set.Data.TotalIndex == 0 ? 300 : 100, 200);
            var maskBlur = maskFade + Tags.Blur(30) + Tags.Anchor(7) + Tags.Paint(1);

            var body = maskFade + Tags.Anchor(5) + Tags.FontSize(offset) +
                       Tags.Move(center.X - offset / 3, center.Y, center.X, center.Y, 0, 200) + content;

            var contentItem = SubtitleEvent.Dialog(body, startTime, endTime, "BannerText");

            var cRec = Utils.FromCenter(center,
                new Size((offset * 12) / 20 * 20, (int)(offset * 1.4) / 20 * 20));
            var mRec = Utils.FromCenter(center,
                new Size((offset * 12) / 20 * 20, (int)(offset * 2) / 20 * 20));
            var mask = AssDraw.Rectangle(mRec).ToString();
            var clipLeft = (
                    Tags.Clip(0, cRec.Y, cRec.X, cRec.Y + cRec.Height) +
                    Tags.Transformation(
                        0, 200, Tags.Clip(0, cRec.Y, cRec.X + cRec.Width, cRec.Y + cRec.Height)))
                .ToString();

            var clipRight = (
                    Tags.Clip(cRec.X, cRec.Y, videoInfo.Resolution.Width, cRec.Y + cRec.Height) +
                    Tags.Transformation(0, 200,
                        Tags.Clip(cRec.X + cRec.Width, cRec.Y,
                            videoInfo.Resolution.Width, cRec.Y + cRec.Height)))
                .ToString();


            var shift = ModdedTags.LeadingHorizontal(offset * 5) +
                        Tags.Transformation(0, 200, ModdedTags.LeadingHorizontal(0));


            var maskItem1 =
                SubtitleEvent.Dialog(maskBlur + clipLeft + mask, startTime, endTime, "BannerMask");
            var maskItem2 =
                SubtitleEvent.Dialog(maskBlur + clipRight + shift + mask, startTime, endTime, "BannerMask");

            return [maskItem1, maskItem2, contentItem];
        }
    }

    private List<Style> MakeBannerStyles()
    {
        var result = new List<Style>();
        var fontsize = (int)((videoInfo.FrameRatio > 16.0 / 9
            ? videoInfo.Resolution.Height * 0.043
            : videoInfo.Resolution.Width * 0.024) * (70 / 61D));
        const string fontName = "思源黑体 CN Bold";

        var whiteColor = new AlphaColor(0, 255, 255, 255);
        var outlineColor = new AlphaColor(30, 95, 92, 123);
        result.Add(new Style("BannerMask", fontName, fontsize, primaryColour: outlineColor,
            outlineColour: outlineColor,
            outline: 0, shadow: 0, alignment: 7));
        result.Add(new Style("BannerText", fontName, fontsize, primaryColour: whiteColor,
            outlineColour: outlineColor,
            outline: 0, shadow: 0, alignment: 7));
        return result;
    }

    #endregion

    #region Marker

    private List<SubtitleEvent> MakeMarkerEvents(List<MarkerFrameSet> markerList)
    {
        List<SubtitleEvent> result = [];
        var count = 0;
        foreach (var set in markerList)
        {
            count++;

            var events = new List<SubtitleEvent>();
            var markerString = $"-----  {count:000}  -----";
            events.Add(SubtitleEvent.Comment($"{markerString}  Start", set.StartTime(), set.EndTime(), "Screen"));
            events.AddRange(GenerateMarkerEvent(set));
            events.Add(SubtitleEvent.Comment($"{markerString}  End", set.StartTime(), set.EndTime(), "Screen"));
            result.AddRange(events);
        }

        return result;

        List<SubtitleEvent> GenerateMarkerEvent(MarkerFrameSet frameSet)
        {
            List<SubtitleEvent> markerEventText = [];
            List<SubtitleEvent> markerEventMask = [];
            var content = frameSet.Data.FinalContent;
            var contentLength = (content.Length + content.Count(c => c > 127)) / 2;

            foreach (var frame in frameSet.Frames)
            {
                var startTime = frame.StartTime();
                var endTime = frame.EndTime();
                var position = frame.Point;
                var fs = _styles.First(style => style.Name == "MarkerText").Fontsize;
                var tagText = new Tags(Tags.Position(position.X, position.Y + (int)(fs * 1.6)));

                var tagMask = new Tags(
                    Tags.Bord(0), Tags.Blur(50), Tags.Clip(
                        new Point(0, position.Y + (int)(fs * 1.6)),
                        new Point((int)(fs * contentLength * 1.5), position.Y + (int)(fs * 2.65))),
                    Tags.Paint(1)
                );
                var mask = AssDraw.Rectangle(
                    new Rectangle(new Point(-50, 0), new Size(100 + fs * contentLength + position.X, fs * 4))
                ).ToString();


                var maskText = tagMask + mask;
                var bodyText = tagText + content;
                if (markerEventMask.Count > 0 && markerEventText.Count > 0)
                {
                    if (markerEventMask[^1].Text == maskText && markerEventText[^1].Text == bodyText)
                    {
                        markerEventMask[^1].End = endTime;
                        markerEventText[^1].End = endTime;
                        continue;
                    }
                }

                markerEventMask.Add(
                    SubtitleEvent.Dialog(maskText, startTime, endTime, "MarkerMask"));
                markerEventText.Add(
                    SubtitleEvent.Dialog(bodyText, startTime, endTime, "MarkerText"));
            }

            return [.. markerEventMask, .. markerEventText];
        }
    }

    private List<Style> MakeMarkerStyles()
    {
        var result = new List<Style>();
        var fontsize = (int)((videoInfo.FrameRatio > 16.0 / 9
            ? videoInfo.Resolution.Height * 0.043
            : videoInfo.Resolution.Width * 0.024) * (70 / 61D));
        const string fontName = "思源黑体 CN Bold";

        var whiteColor = new AlphaColor(0, 255, 255, 255);
        var outlineColor = new AlphaColor(30, 95, 92, 123);
        result.Add(new Style("MarkerMask", fontName, fontsize, primaryColour: outlineColor,
            outlineColour: outlineColor,
            outline: 0, shadow: 0, alignment: 7));
        result.Add(new Style("MarkerText", fontName, fontsize, primaryColour: whiteColor,
            outlineColour: outlineColor,
            outline: 0, shadow: 0, alignment: 7));
        return result;
    }

    #endregion

    private Point _nameTagPosition = new(0, 0);
    private readonly List<Style> _styles = [];

    public Subtitle Make(
        List<DialogFrameSet> dialogList,
        List<BannerFrameSet> bannerList,
        List<MarkerFrameSet> markerList)
    {
        var events = new List<SubtitleEvent>();

        if (dialogList.Count != 0)
        {
            _nameTagPosition = dialogList[0].Frames[0].Point;
            _styles.AddRange(MakeDialogStyles());
            events.AddRange(MakeDialogEvents(dialogList));
        }

        if (bannerList.Count != 0)
        {
            _styles.AddRange(MakeBannerStyles());
            events.AddRange(MakeBannerEvents(bannerList));
        }

        if (markerList.Count != 0)
        {
            _styles.AddRange(MakeMarkerStyles());
            events.AddRange(MakeMarkerEvents(markerList));
        }


        return new Subtitle(
            new ScriptInfo(videoInfo.Resolution.Width, videoInfo.Resolution.Height),
            new Garbage(Path.GetFileName(videoInfo.Path), Path.GetFileName(videoInfo.Path)),
            new Styles(_styles.ToArray()),
            new Events(events.ToArray())
        );
    }
}