import sys
import vapoursynth as vs
import mvsfunc as mvf
 
#载入vs 运行环境
#指定最大线程数和最大内存
core = vs.core
core.num_threads = 12
core.max_cache_size = 16384

#资源加载
#视频输入 填写名称(引号内)
# source = "" # r"F:\ProjectSekai\录屏存档\VBS Archives\VBS Archives_#001.mp4"
# subtitle = "" # r"F:\ProjectSekai\录屏存档\VBS Archives\VBS Archives_#001.ass"

#设置帧数(fpsnum/fpsden)
source = core.lsmas.LWLibavSource(source,fpsnum = 60000,fpsden = 1001) # type: ignore

#色调调节
#如果视频过亮 打开下面两行的注释
#source = mvf.Depth(source,depth = 32)
#source = core.tonemap.Mobius(clip = source,exposure = 0.95,transition = 0.3,peak = 1.05)


#设置matrix和range以及YUV(影响最终输出视频的色深 色域 采样)
resize = core.resize.Bicubic(source,matrix_in_s = "709",format = vs.YUV420P8,range_s = "limited")

#加载字幕 填写名称(引号内)
#无字幕请注释掉此行
resize = core.vsfm.TextSubMod(resize,subtitle)# type: ignore

##以下为画面旋转等操作
#整体画面旋转180°
#resize = core.std.Turn180(resize)
#整体画面矩阵转置
#resize = core.std.Transpose(resize)
#垂直镜像旋转
#resize = core.std.FlipVertical(resize)
#水平镜像旋转
#resize = core.std.FlipHorizontal(resize)

#最终输出
resize.set_output()