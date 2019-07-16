import time

class TokenBucket(object):
    '''令牌桶算法'''
    def __init__(self,rate,capacity):
        self.capacity=capacity   #令牌桶容量
        self.rate=rate           #令牌产生速度
        self.surplus=0           #剩余令牌数量
        self.last_time=time.time()

    def take(self,amount):
        '''从桶内取出令牌,令牌数量不够则返回False'''
        cur_time=time.time()
        increment=(cur_time-self.last_time)*self.rate
        #剩余令牌+新产生的令牌不得超过令牌桶容量
        self.surplus=min(increment+self.surplus,self.capacity)
        if amount>self.surplus:
            return False
        self.last_time=cur_time
        #减去拿走的令牌数量等于剩下的令牌数量
        self.surplus-=amount
        return True

    def size(self):
        '''返回桶内剩余令牌数量'''
        cur_time=time.time()
        increment=(cur_time-self.last_time)*self.rate
        self.surplus=min(increment+self.surplus,self.capacity)
        self.last_time=cur_time
        return self.surplus

class SpeedTester(object):
    '''漏桶算法,来自$$R'''
    def __init__(self,speed=0):
        self.speed=speed*1024       #桶容量
        self.sumlen=0               #剩余水量
        self.last_time=time.time()  #上次漏水时间

    def add(self,datalen):
        '''往桶内注水'''
        if self.speed>0:
            #注水
            self.sumlen+=datalen

    def isExceed(self):
        '''判断桶溢出'''
        if self.speed>0:
            cut_t=time.time()
            #漏水
            self.sumlen-=(cut_t-self.last_time)*self.speed
            if self.sumlen<0:
                self.sumlen=0
            self.last_time=cut_t
            #判断桶溢出
            return self.sumlen>=self.speed
        return False
