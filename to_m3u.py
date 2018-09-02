#!/usr/bin/python
# -*- coding: utf-8 -*-
import os
import sys
import inspect
os.chdir(os.path.dirname(os.path.realpath(inspect.getfile(inspect.currentframe()))))
output=open('playlist.m3u','wb+')
output.write("#EXTM3U\n")
with open('list.txt','r') as fd:
    while True:
        lines=fd.readline()
        if len(lines)>0:
            line=lines.split(',')
            output.write("#EXTINF:-1,"+line[0]+"\n")
            output.write(line[1])
        else:
            break
sys.exit(0)
