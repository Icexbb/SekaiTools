import subprocess
import os

script = r".\lim5994.vpy"
source = r"F:\ProjectSekai\录屏存档\VBS Archives\VBS Archives_#001.mp4"
subtitle = r"F:\ProjectSekai\录屏存档\VBS Archives\VBS Archives_#001.ass"
command1 = rf'.\vspipe "{script}" - -c y4m -a "source={source}" -a "subtitle={subtitle}"'
command2=    rf'.\ffmpeg -f yuv4mpegpipe -i - -i "{source}" -map 0:v -map 1:1 -c:v libx264 -psy-rd 0.4:0.15 -c:a copy output.mp4 -y'
print(command1)
print(command2)

rid,wid = os.pipe()
# 创建命名管道
piperead = os.fdopen(rid, 'r')
pipewrite = os.fdopen(wid, 'w')
subprocess.Popen(command1,stdout=pipewrite,stderr=None)
subprocess.Popen(command2,stdin=piperead,stdout=None)