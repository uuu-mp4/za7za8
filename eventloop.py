#!/usr/bin/python
# -*- coding: utf-8 -*-

import os
import time
import errno
import socket
import select
import traceback
from collections import defaultdict

__all__ = ['EventLoop', 'POLL_NULL', 'POLL_IN', 'POLL_OUT', 'POLL_ERR', 'POLL_HUP', 'EVENT_NAMES', 'TIMEOUT_PRECISION']

POLL_NULL = 0x00
POLL_IN   = 0x01
POLL_OUT  = 0x04
POLL_ERR  = 0x08
POLL_HUP  = 0x10

EVENT_NAMES = {
    POLL_NULL:  'POLL_NULL',
    POLL_IN:    'POLL_IN',
    POLL_OUT:   'POLL_OUT',
    POLL_ERR:   'POLL_ERR',
    POLL_HUP:   'POLL_HUP'
}

TIMEOUT_PRECISION = 2

class SelectLoop(object):

    def __init__(self):
        self._r_list = set() #可读
        self._w_list = set() #可写
        self._x_list = set() #错误

    def poll(self, timeout):
        r, w, x = select.select(self._r_list, self._w_list, self._x_list, timeout)
        results = defaultdict(lambda: POLL_NULL)
        for p in [(r,POLL_IN),(w,POLL_OUT),(x,POLL_ERR)]:
            for fd in p[0]:
                results[fd] |= p[1] #fd:POLL_IN|fd:POLL_OUT|fd:POLL_ERR
        return results.items()

    def register(self, fd, mode):
        if mode & POLL_IN:
            self._r_list.add(fd)
        if mode & POLL_OUT:
            self._w_list.add(fd)
        if mode & POLL_ERR:
            self._x_list.add(fd)

    def unregister(self, fd):
        if fd in self._r_list:
            self._r_list.remove(fd)
        if fd in self._w_list:
            self._w_list.remove(fd)
        if fd in self._x_list:
            self._x_list.remove(fd)

    def modify(self, fd, mode):
        self.unregister(fd)
        self.register(fd, mode)

    def close(self):
        pass

class EventLoop(object):
    def __init__(self):
        if hasattr(select, 'epoll'):
            self._impl = select.epoll()
        elif hasattr(select, 'select'):
            self._impl = SelectLoop()
        else:
            raise AttributeError('There is no matching I/O event monitor')
        self._last_time = time.time()
        self._fdmap = {}                #fileno:(connobj,callback)
        self._periodic_callbacks = []   #socket错误回调列表
        self._stopping = False          #EventLoop停止标志

    def poll(self, timeout=None):
        '''获取事件'''
        events = self._impl.poll(timeout)
        return [(self._fdmap[fd][0], fd, event) for fd, event in events] #connobj,fileno,event

    def add(self, conn, mode, handler):
        '''根据连接对象添加需要监听的描述符'''
        fd=conn.fileno()
        self._fdmap[fd]=(conn,handler)
        self._impl.register(fd,mode)

    def remove(self, conn):
        '''根据连接对象移除监听的文件描述符'''
        fd=conn.fileno()
        del self._fdmap[fd]
        self._impl.unregister(fd)

    def add_periodic(self, callback):
        '''添加超时回调'''
        self._periodic_callbacks.append(callback)

    def remove_periodic(self, callback):
        '''移除超时回调'''
        self._periodic_callbacks.remove(callback)

    def modify(self, f, mode):
        '''修改文件描述符监听模式'''
        fd = f.fileno()
        self._impl.modify(fd, mode)

    def stop(self):
        '''退出EventLoop标志'''
        self._stopping = True

    def run(self):
        events = []
        while not self._stopping:
            asap = False
            try:
                events = self.poll(TIMEOUT_PRECISION)
            except (OSError, IOError) as e:
                if errno_from_exception(e) in (errno.EPIPE, errno.EINTR):
                    asap = True
                else:
                    traceback.print_exc()
                    continue
            handle = False
            for connobj, fileno, event in events:
                handler = self._fdmap.get(fd, None)
                if handler is not None:
                    handler = handler[1]
                    try:
                        handle = handler.method_route(connobj, fileno, event)
                    except (OSError, IOError) as e:
                        traceback.print_exc()
            now = time.time()
            if asap or now - self._last_time >= TIMEOUT_PRECISION:
                for callback in self._periodic_callbacks:
                    callback()
                self._last_time = now

    def __del__(self):
        '''关闭epoll/kqueue/select事件管理器'''
        self._impl.close()

def errno_from_exception(e):
    '''返回socket错误代码'''
    if hasattr(e, 'errno'):
        return e.errno
    elif e.args:
        return e.args[0]
    else:
        return None

def get_sock_error(sock):
    '''返回socket错误代码和错误描述'''
    error_number = sock.getsockopt(socket.SOL_SOCKET, socket.SO_ERROR)
    return socket.error(error_number, os.strerror(error_number))

