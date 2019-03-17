import os
import sys
import random

macnum=45
dstmac = []
srcmac = []
def build_rand_mac():
    str1 = random.sample("0123456789ABCDEF",1)
    str2 = random.sample("02468ACE",1)
    mac5 = []
    for i in range(0,5):
	    mac5.append("".join(random.sample("0123456789ABCDEF",2)))
    return ''.join(str1+str2)+':'+':'.join(mac5)

for i in range(macnum):
    dstmac.append(build_rand_mac())

for i in range(1,macnum+1):
    srcmac.append("00:00:5E:00:01:"+str(i).zfill(2))

print("/interface bridge nat remove [find ]\n/interface bridge nat")
for i in range(macnum):
    print("add action=src-nat chain=srcnat src-mac-address=\"%s/FF:FF:FF:FF:FF:FF\" to-src-mac-address=\"%s\""%(srcmac[i],dstmac[i]))
    print("add action=dst-nat chain=dstnat dst-mac-address=\"%s/FF:FF:FF:FF:FF:FF\" to-dst-mac-address=\"%s\""%(dstmac[i],srcmac[i]))
