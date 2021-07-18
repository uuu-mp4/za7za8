#include <sys/ioctl.h>
#include <net/if.h>
#include <linux/if_tun.h>
#include <arpa/inet.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <fcntl.h>
#include <unistd.h>
#include <stdint.h>
#include "dtls.h"

//创建一个TUN设备
int tun_open(char **namp)
{
    struct ifreq ifr;
    int fd;
    char path[20];
    snprintf(path, 20, "/dev/net/tun");
    if ((fd = open(path, O_RDWR)) < 0) {
        perror(path);
        return -1;
    }
    fcntl(fd, F_SETFL, O_NONBLOCK);
    memset(&ifr, 0, sizeof(ifr));
    ifr.ifr_flags = IFF_TUN | IFF_NO_PI;
    if (ioctl(fd, TUNSETIFF, &ifr) < 0) {
        perror("TUNSETIFF");
        close(fd);
        return -1;
    }
    *namp = strdup(ifr.ifr_name);
    return fd;
}

//执行IOCTL调用
static int _e_ioctl(int fd, uint32_t cmd, const char *cmds, void *arg)
{
    if (ioctl(fd, cmd, arg) < 0) {
        perror(cmds);
        return -1;
    }
    return 0;
}

//宏替换
#define e_ioctl(fd, cmd, arg)       _e_ioctl(fd, cmd, #cmd, arg)

//关闭接口
static int if_down(int s, struct ifreq *ifr)
{
    int r;
    if ((r = e_ioctl(s, SIOCGIFFLAGS, ifr)) == 0) {
        ifr->ifr_flags &= ~IFF_UP;
        r = e_ioctl(s, SIOCSIFFLAGS, ifr);
    }
    return r;
}

//打开接口
static int if_up(int s, struct ifreq *ifr)
{
    int r;
    if ((r = e_ioctl(s, SIOCGIFFLAGS, ifr)) == 0) {
        ifr->ifr_flags |= IFF_UP;
        r = e_ioctl(s, SIOCSIFFLAGS, ifr);
    }
    return r;
}

//设置接口地址
static int if_setxaddr(int s, struct ifreq *ifr, uint32_t cmd, char *cmds, uint32_t addr)
{
    struct sockaddr_in sin;
    sin.sin_family = AF_INET;
    sin.sin_addr.s_addr = htonl(addr);
    memcpy(&ifr->ifr_addr, &sin, sizeof (sin));
    return _e_ioctl(s, cmd, cmds, ifr);
}

//设置点对点接口本地地址
static int if_setifaddr(int s, struct ifreq *ifr, uint32_t addr)
{
    return if_setxaddr(s, ifr, SIOCSIFADDR, "SIOCSIFADDR", addr);
}

//设置点对点接口远程地址
static int if_setifdstaddr(int s, struct ifreq *ifr, uint32_t addr)
{
    return if_setxaddr(s, ifr, SIOCSIFDSTADDR, "SIOCSIFDSTADDR", addr);
}

//设置TUN接口地址,点对点模式
int tun_setaddrs(const char *ifname, uint32_t local, uint32_t remote)
{
    struct ifreq ifr;
    int s, r;
    s = socket(AF_INET, SOCK_DGRAM, 0);
    memset(&ifr, 0, sizeof (ifr));
    strcpy(ifr.ifr_name, ifname);
    r = -1;
    if (if_down(s, &ifr) < 0) {
        goto out;
    }
    if (if_setifaddr(s, &ifr, local) < 0) {
        goto out;
    }
    if (if_setifdstaddr(s, &ifr, remote) < 0) {
        goto out;
    }
    r = if_up(s, &ifr);
out:
    close(s);
    return r;
}
