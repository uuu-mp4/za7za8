import sys
import time
import random
import socket
import struct
import threading
from optparse import OptionParser

def checksum(data):
    s=0
    n=len(data)%2
    for i in range(0,len(data)-n,2):
        s+=ord(data[i])+(ord(data[i+1])<<8)
    if n:
        s+=ord(data[i+1])
    while(s>>16):
        s=(s&0xFFFF)+(s>>16)
    s=~s&0xffff
    return s

class ETHER(object):
    def __init__(self,src,dst,type=0x0800):
        self.src=src
        self.dst=dst
        self.type=type

    def pack(self):
        return struct.pack('!6s6sH',self.dst,self.src,self.type)

class IP(object):
    def __init__(self,source,destination,payload=''):
        self.version=4
        self.ihl=5
        self.tos=0
        self.tl=20+len(payload)
        self.id=0
        self.flags=0
        self.offset=0
        self.ttl=255
        self.protocol=socket.IPPROTO_TCP
        self.checksum=0
        self.source=socket.inet_aton(source)
        self.destination=socket.inet_aton(destination)

    def pack(self):
        ver_ihl=(self.version<<4)+self.ihl
        flags_offset=(self.flags<<13)+self.offset
        ip_header=struct.pack("!BBHHHBBH4s4s",
            ver_ihl,
            self.tos,
            self.tl,
            self.id,
            flags_offset,
            self.ttl,
            self.protocol,
            self.checksum,
            self.source,
            self.destination)
        self.checksum=checksum(ip_header)
        ip_header=struct.pack("!BBHHHBBH4s4s",
            ver_ihl,
            self.tos,
            self.tl,
            self.id,
            flags_offset,
            self.ttl,
            self.protocol,
            socket.htons(self.checksum),
            self.source,
            self.destination)
        return ip_header

class TCP(object):
    def __init__(self,srcp,dstp,seq_number,ack_number):
        self.src_port=srcp
        self.dst_port=dstp
        self.seq=seq_number
        self.ack=ack_number
        self.offset=5
        self.reserved=0
        self.urg=0
        self.ack=1
        self.psh=0
        self.rst=0
        self.syn=0
        self.fin=0
        self.window=socket.htons(5840)
        self.checksum=0
        self.urgp=0
        self.payload=""

    def pack(self,source,destination):
        offset=(self.offset<<4)+0
        flags=self.fin+(self.syn<<1)+(self.rst<<2)+(self.psh<<3)+(self.ack<<4)+(self.urg<<5)
        tcp_header=struct.pack('!HHLLBBHHH',
            self.src_port,
            self.dst_port,
            self.seq,
            self.ack,
            offset,
            flags,
            self.window,
            self.checksum,
            self.urgp)
        reserved=0
        protocol=socket.IPPROTO_TCP
        total_length=len(tcp_header)+len(self.payload)
        psh=struct.pack("!4s4sBBH",source,destination,reserved,protocol,total_length)
        psh=psh+tcp_header+self.payload
        tcp_checksum=checksum(psh)
        tcp_header=struct.pack("!HHLLBBH",
            self.srcp,
            self.dstp,
            self.seqn,
            self.ackn,
            offset,
            flags,
            self.window)
        tcp_header+=struct.pack('H',tcp_checksum)+struct.pack('!H',self.urgp)
        return tcp_header

