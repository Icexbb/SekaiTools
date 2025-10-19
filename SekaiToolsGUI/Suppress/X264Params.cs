namespace SekaiToolsGUI.Suppress;

internal class X264Params
{
    public static X264Params Instance { get; } = new();
    public int BFrames { get; set; } = 16;
    public int BAdapt { get; set; } = 2;
    public string Me { get; set; } = "umh";
    public int MeRange { get; set; } = 24;
    public int SubMe { get; set; } = 11;
    public int AqMode { get; set; } = 3;
    public int Ref { get; set; } = 10;
    public string PsyRd { get; set; } = "0.2:0.0";
    public string DeBlock { get; set; } = "1:2";
    public int KeyInt { get; set; } = 600;
    public int Crf { get; set; } = 21;

    public string GetX264Params()
    {
        return $"bframes={BFrames}:" +
               $"b-adapt={BAdapt}:" +
               $"me={Me}:" +
               $"merange={MeRange}:" +
               $"subme={SubMe}:" +
               $"aq-mode={AqMode}:" +
               $"ref={Ref}:" +
               $"psy-rd='{PsyRd}':" +
               $"deblock='{DeBlock}':" +
               $"keyint={KeyInt}:" +
               $"crf={Crf}";
    }

    public string GetSimpleX264Params()
    {
        return $"psy-rd='{PsyRd}':crf={Crf}";
    }
}