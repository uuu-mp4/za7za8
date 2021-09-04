#include <net/route.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <string.h>
#include <sys/ioctl.h>
#include <stdio.h>
#include <stdlib.h>
#include <ctype.h>
#include <unistd.h>
#include <net/if.h>       
#include <netdb.h>

int inet_setroute(char *args[]) {
	int op = 1; //add
	struct rtentry route;
	char dstaddr[128] = {0};
	char gateway[128] = {0};
	char netmask[128] = {0};
	struct sockaddr_in *addr;
	memset((char*)&route, 0x00, sizeof(route));
	route.rt_flags = RTF_UP;
	while(args) {
		args++;
		if(*args == NULL) {
			break;
		}
		if(!strcmp(*args, "del")) {
			op = 0; //del
			continue;
		}
		if(!strcmp(*args, "net")) {
			args++;
			printf("dstaddr:%s\n", *args);
			strcpy(dstaddr, *args);
			addr = (struct sockaddr_in*)&route.rt_dst;
			addr->sin_family = AF_INET;
			addr->sin_addr.s_addr = inet_addr(dstaddr);
			continue;
		} else if(!strcmp(*args, "host")) {
			args++;
			strcpy(dstaddr, *args);
			addr = (struct sockaddr_in*)&route.rt_dst;
			addr->sin_family = AF_INET;
			addr->sin_addr.s_addr = inet_addr(dstaddr);
			route.rt_flags |= RTF_HOST;
			continue;
		}
		if(!strcmp(*args, "netmask")) {
			args++;
			printf("netmask:%s\n", *args);
			strcpy(netmask, *args);
			addr = (struct sockaddr_in*) &route.rt_genmask;
			addr->sin_family = AF_INET;
			addr->sin_addr.s_addr = inet_addr(netmask);
			continue;
		}
		if(!strcmp(*args, "gateway")) {
			args++;
			printf("gateway:%s\n", *args);
			strcpy(gateway, *args);
			addr = (struct sockaddr_in*) &route.rt_gateway;
			addr->sin_family = AF_INET;
			addr->sin_addr.s_addr = inet_addr(gateway);
			route.rt_flags |= RTF_GATEWAY;
			continue;
		}
		if(!strcmp(*args, "dev")) {
			args++;
			route.rt_dev = *args;
			continue;
		}
	}
	int skfd = socket(AF_INET, SOCK_DGRAM, 0);
	if(skfd < 0) {
		perror("socket");
		return -1;
	}
	if(ioctl(skfd, op>0?SIOCADDRT:SIOCDELRT, &route) < 0) {
		perror("ioctl");
		close(skfd);
		return -1;
	}
	close(skfd);
	return 0;
}

int main(int argc, char *argv[]) {
	return inet_setroute(argv);
}
