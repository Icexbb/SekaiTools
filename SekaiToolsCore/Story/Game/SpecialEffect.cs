namespace SekaiToolsCore.Story.Game;

/* EffectType
 * 1 渐入
 * 2 渐出-黑
 * 3 闪烁
 * 4 渐出-白
 * 5 画面震动-开始
 * 6 对话震动-开始
 * 7 背景
 * 8 地点横幅
 * 18 地点角标
 * 23 选项
 * 25 画面震动-停止
 * 26 对话震动-停止
 */
public struct SpecialEffect()
{
    public SpecialEffect(int effectType, string stringVal, string stringValSub, double duration, double intVal) : this()
    {
        EffectType = effectType;
        StringVal = stringVal;
        Duration = duration;
        StringValSub = stringValSub;
        IntVal = intVal;
    }

    public int EffectType { get; set; }
    public string StringVal { get; set; }
    public double Duration { get; set; }
    public string StringValSub { get; set; }
    public double IntVal { get; set; }
}