import time

class TokenBucket(object):
    '''令牌桶算法'''
    def __init__(self,rate,capacity):
        self.capacity=capacity   #令牌桶容量
        self.rate=rate           #令牌产生速度
        self.surplus=0           #剩余令牌数量
        self.last_time=time.time()

    def take(self,amount):
        '''从桶内取出令牌,不够返回False'''
        cur_time=time.time()
        increment=(cur_time-self.last_time)*self.rate
        #剩余令牌+新产生的令牌不得超过令牌桶容量
        self.surplus=min(increment+self.surplus,self.capacity)
        if amount>self.surplus:
            return False
        #减去拿走的令牌数量
        self.last_time=cur_time
        self.surplus-=amount
        return True

    def get_free(self):
        '''返回可取的令牌数量'''
        cur_time=time.time()
        increment=(cur_time-self.last_time)*self.rate
        self.surplus=min(increment+self.surplus,self.capacity)
        self.last_time=cur_time
        return self.surplus

class SpeedTester(object):
    '''漏桶算法,来自$$R'''
    def __init__(self,capacity=0):
        self.capacity=capacity*1024 #桶容量
        self.volume=0               #剩余水量
        self.last_time=time.time()  #上次漏水时间

    def add(self,datalen):
        '''往桶内注水'''
        if self.capacity>0:
            #注水
            self.volume+=datalen

    def isExceed(self):
        '''判断桶溢出'''
        if self.capacity>0:
            cut_time=time.time()
            #漏水
            self.volume-=(cut_time-self.last_time)*self.capacity
            if self.volume<0:
                self.volume=0
            self.last_time=cut_time
            #判断桶溢出
            return self.volume>=self.capacity
        return False

    def get_free(self):
        '''获取空闲容量'''
        if self.capacity>0:
            cut_time=time.time()
            self.volume-=(cut_time-self.last_time)*self.capacity
            if self.volume<0:
                self.volume=0
            self.last_time=cut_time
            #判断桶溢出
            return max(self.capacity-self.volume,0)
        return 1
