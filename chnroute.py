#!/usr/bin/python
# coding=utf-8
import re
import sys
import math
import urllib2
import textwrap
from IPy import IP

def generate_iplist():
    results = fetch_ip_data()
    resfd=open('./result.txt','w+')
    for i in results:
        #output: 47.92.0.0-47.95.255.255
        #resfd.write("%s\n"%(IP("%s/%s"%(i[0],i[2])).strNormal(3)))
        #output: 47.92.0.0/255.255.0.0
        #resfd.write("%s\n"%(IP("%s/%s"%(i[0],i[2])).strNormal(2)))
        #output: 47.92.0.0/16
        resfd.write("%s\n"%(IP("%s/%s"%(i[0],i[2])).strNormal(1)))
    resfd.close()

def fetch_ip_data():
    '''从apnic获取所有属于中国的IP段,返回二维列表'''
    print "fetching data from apnic.net, please wait..."
    url=r'https://ftp.apnic.net/apnic/stats/apnic/delegated-apnic-latest'
    data=urllib2.urlopen(url).read()
    cnregex=re.compile(r'apnic\|cn\|ipv4\|[0-9\.]+\|[0-9]+\|[0-9]+\|a.*',re.IGNORECASE)
    cndata=cnregex.findall(data)
    results=[]
    for item in cndata:
        unit_items=item.split('|')
        starting_ip=unit_items[3]
        num_ip=int(unit_items[4])
        imask=0xffffffff^(num_ip-1)
        imask=hex(imask)[2:]
        mask=[0]*4
        mask[0]=imask[0:2]
        mask[1]=imask[2:4]
        mask[2]=imask[4:6]
        mask[3]=imask[6:8]
        mask=[int(i,16) for i in mask]
        mask="%d.%d.%d.%d"%tuple(mask)
        mask2=32-int(math.log(num_ip,2))
        results.append((starting_ip,mask,mask2))
    return results

if __name__=='__main__':
    generate_iplist()
    sys.exit(0)
