#!/usr/bin/python
# -*- coding: utf-8 -*-
# 海康威视设备扫描工具
import os
import re
import sys
import time
import random
import inspect
import logging
import argparse
import requests
import resource
import threading

try:
    from Queue import Queue as queue
except:
    from queue import Queue as queue
logging.getLogger('urllib3').setLevel(logging.WARNING)

header={
    "User-Agent":"Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0)",
    "Accept":"*/*",
    "Accept-Language":"zh-CN,zh;q=0.9",
    "Accept-Encoding":"gzip, deflate",
    "Authorization":"Basic YWRtaW46MTIzNDU=",
    "x-requested-with":"XMLHttpRequest",
    "Cookie":"enableAnonymous81=false; language=zh"
}

class ThreadManager(object):
    def __init__(self,num):
        self.thread_num=num
        self.queue=queue()
        self.threadlist=list()
        self.shutdown=threading.Event()
    
    def add_task(self,task):
        self.queue.put(task)
    
    def __start__(self):
        for i in range(self.thread_num):
            i=ThreadWork(self.queue,self.shutdown,i)
            i.start()
            self.threadlist.append(i)
    
    def loop(self):
        for i in self.threadlist:
            if not i.isAlive():
                i=ThreadWork(self.queue,self.shutdown,i)
                i.start()
    
    def waitcomplete(self):
        for i in self.threadlist:
            if i.isAlive():
                i.join()

    def isEmpty(self):
        return self.queue.empty()
    
    def __close__(self):
        self.shutdown.set()

class ThreadWork(threading.Thread):
    def __init__(self,queue,flag,num):
        threading.Thread.__init__(self)
        self.setName(str(num))
        self.taskqueue=queue
        self.shutdown=flag
        self.setDaemon(True)

    def run(self):
        while True:
            if self.shutdown.isSet():
                logging.info("线程%s检测到退出标志!"%(self.getName()))
                break
            try:
                task=self.taskqueue.get(timeout=5)
            except:
                continue
            else:
                if len(task)>0:
                    hkvision(task.strip())

def hkvision(ipaddr):
    url=("http://%s/PSIA/Custom/SelfExt/userCheck"%ipaddr)
    try:
        result=requests.get(url,headers=header,timeout=6)
    except requests.exceptions.ConnectionError:
        logging.info("[×]TCP连接异常(%s)"%ipaddr)
        return
    except requests.exceptions.Timeout or requests.exceptions.ConnectTimeout:
        logging.info("[×]HTTP连接超时(%s)"%ipaddr)
        return
    except requests.exceptions.HTTPError as e:
        logging.info("[×]HTTP请求错误(%s):%s"%(ipaddr,e))
        return
    except requests.exceptions.SSLError as e:
        logging.info("[×]SSL握手错误(%s):%s"%(ipaddr,e))
        return
    else:
        result.encoding='utf-8'
        if result.status_code == 200 and len(result.text)>0:
            if parse_result(result.text):
                logging.critical("[√]成功:%s"%ipaddr)
                return

def parse_result(text):
    try:
        login_code=re.findall(r'<statusValue>(.*)</statusValue>',text)
    except:
        return False
    else:
        if len(login_code)>0 and login_code[0]=='200':
            return True
        else:
            return False

if __name__=='__main__':
    os.chdir(os.path.dirname(os.path.realpath(inspect.getfile(inspect.currentframe()))))
    #修改最大文件描述符数量以及修改栈大小,需要root权限,不兼容WinNT
    #proc_handle=os.popen("cat /proc/sys/fs/file-max","r")
    #max_file=proc_handle.readline().strip()
    #proc_handle.close()
    #max_file=int(max_file)
    #resource.setrlimit(resource.RLIMIT_NOFILE,(max_file,max_file))
    #resource.setrlimit(resource.RLIMIT_STACK,(resource.RLIM_INFINITY,resource.RLIM_INFINITY))
    cmdline=argparse.ArgumentParser()
    cmdline.add_argument('-o',dest='output',help='输出结果到文件')
    cmdline.add_argument('--level',type=int,default=3,help='日志级别(1-5)') #1:critical,2:error,3:warning,4:info,5:debug
    cmdline.add_argument('-n',dest='num',type=int,default=8,help='并发线程数')
    cmdline.add_argument('-f',dest='iplist',default="iplist.txt",help='ip列表文件,格式:127.0.0.1:81')
    cmdarg=cmdline.parse_args()
    logging.addLevelName(50,'CRIT')
    logging.addLevelName(30,'WARN')
    if cmdarg.level < 1 or cmdarg.level > 5:
        cmdarg.level = 3
    if cmdarg.level == 1:
        if cmdarg.output and len(cmdarg.output)>0:
            logging.basicConfig(level=logging.CRIT,filename=cmdarg.output,format='%(asctime)s %(levelname)-6s %(message)s',datefmt='%Y-%m-%d %H:%M:%S')
        else:
            logging.basicConfig(level=logging.CRIT,format='%(asctime)s %(levelname)-6s %(message)s',datefmt='%Y-%m-%d %H:%M:%S')
    elif cmdarg.level == 2:
        if cmdarg.output and len(cmdarg.output)>0:
            logging.basicConfig(level=logging.ERROR,filename=cmdarg.output,format='%(asctime)s %(levelname)-6s %(message)s',datefmt='%Y-%m-%d %H:%M:%S')
        else:
            logging.basicConfig(level=logging.ERROR,format='%(asctime)s %(levelname)-6s %(message)s',datefmt='%Y-%m-%d %H:%M:%S')
    elif cmdarg.level == 3:
        if cmdarg.output and len(cmdarg.output)>0:
            logging.basicConfig(level=logging.WARN,filename=cmdarg.output,format='%(asctime)s %(levelname)-6s %(message)s',datefmt='%Y-%m-%d %H:%M:%S')
        else:
            logging.basicConfig(level=logging.WARN,format='%(asctime)s %(levelname)-6s %(message)s',datefmt='%Y-%m-%d %H:%M:%S')
    elif cmdarg.level == 4:
        if cmdarg.output and len(cmdarg.output)>0:
            logging.basicConfig(level=logging.INFO,filename=cmdarg.output,format='%(asctime)s %(levelname)-6s %(message)s',datefmt='%Y-%m-%d %H:%M:%S')
        else:
            logging.basicConfig(level=logging.INFO,format='%(asctime)s %(levelname)-6s %(message)s',datefmt='%Y-%m-%d %H:%M:%S')
    elif cmdarg.level == 5:
        if cmdarg.output and len(cmdarg.output)>0:
            logging.basicConfig(level=logging.DEBUG,filename=cmdarg.output,format='%(asctime)s %(levelname)-6s %(message)s',datefmt='%Y-%m-%d %H:%M:%S')
        else:
            logging.basicConfig(level=logging.DEBUG,format='%(asctime)s %(levelname)-6s %(message)s',datefmt='%Y-%m-%d %H:%M:%S')
    work_manager=ThreadManager(cmdarg.num)
    work_manager.__start__()
    try:
        fd = open(cmdarg.iplist,'r')
    except:
        logging.critical("IP列表文件不存在:%s"%cmdarg.iplist)
        exit(-1)
    while True:
        line = fd.readline()
        if line and len(line)>0:
            work_manager.add_task(line)
        else:
            break
    fd.close()
    while not work_manager.isEmpty():
        work_manager.loop()
        time.sleep(1)
    logging.info("设置程序关闭标志")
    work_manager.__close__()
    logging.info("等待所有线程退出")
    work_manager.waitcomplete()
    sys.exit(0)

