using Newtonsoft.Json.Linq;

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
public struct SpecialEffect(int effectType, string stringVal, string stringValSub, double duration, double intVal)
{
    public readonly int EffectType = effectType;
    public readonly string StringVal = stringVal;
    public readonly double Duration = duration;
    public string StringValSub = stringValSub;
    public double IntVal = intVal;

    public static SpecialEffect FromJObject(JObject json)
    {
        return new SpecialEffect(
            json.Get("EffectType", 0),
            json.Get("StringVal", ""),
            json.Get("StringValSub", ""),
            json.Get("Duration", 0.0),
            json.Get("IntVal", 0.0)
        );
    }
}