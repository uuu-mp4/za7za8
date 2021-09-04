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

#define RTACTION_ADD 1   
#define RTACTION_DEL 2   

void usage();
int inet_setroute(int action, char **args);

int main(int argc, char **argv) {
	int action = 0;
	if(argc < 5) {
		usage();
		return -1;
	}
	if(strcmp(argv[1], "-A")) {
		usage();
		return -1;
	}
	if(!strcmp(argv[3], "add")) {
		action = RTACTION_ADD;
	}
	if(!strcmp(argv[3], "del")) {
		action = RTACTION_DEL;
	}
	if(!strcmp(argv[2], "inet")) { /* add or del */
		inet_setroute(action, argv+4);
	}
	return 0;
}

void usage() {
	printf("IPv4 Command: route -A inet add/del -net/-host TARGET netmask NETMASK gw GETWAY dev DEVICE mtu MTU\n");
	return ;
}

int inet_setroute(int action, char **args) {
	struct rtentry route;
	char target[128] = {0};
	char gateway[128] = {0};
	char netmask[128] = {0};
	struct sockaddr_in *addr;
	int skfd;
	memset((char*)&route, 0x00, sizeof(route));
	route.rt_flags = RTF_UP;
	args++;
	while(args) {
		if(*args == NULL) {
			break;
		}
		if(!strcmp(*args, "-net")) {
			args++;
			strcpy(target, *args);
			addr = (struct sockaddr_in*)&route.rt_dst;
			addr->sin_family = AF_INET;
			addr->sin_addr.s_addr = inet_addr(target);
			args++;
			continue;
		} else if(!strcmp(*args, "-host")) {
			args++;
			strcpy(target, *args);
			addr = (struct sockaddr_in*)&route.rt_dst;
			addr->sin_family = AF_INET;
			addr->sin_addr.s_addr = inet_addr(target);
			route.rt_flags |= RTF_HOST;
			args++;
			continue;
		} else {
			usage();
			return -1;
		}
		if(!strcmp(*args, "netmask")) {
			args++;
			strcpy(netmask, *args);
			addr = (struct sockaddr_in*) &route.rt_genmask;
			addr->sin_family = AF_INET;
			addr->sin_addr.s_addr = inet_addr(netmask);
			args++;
			continue;
		}
		if(!strcmp(*args, "gw") || !strcmp(*args, "gateway")) {
			args++;
			strcpy(gateway, *args);
			addr = (struct sockaddr_in*) &route.rt_gateway;
			addr->sin_family = AF_INET;
			addr->sin_addr.s_addr = inet_addr(gateway);
			route.rt_flags |= RTF_GATEWAY;
			args++;
			continue;
		}
		if(!strcmp(*args, "device") || !strcmp(*args, "dev")) {
			args++;
			route.rt_dev = *args;
			args++;
			continue;
		}
		if(!strcmp(*args, "mtu")) {
			args++;
			route.rt_flags |= RTF_MTU;
			route.rt_mtu = atoi(*args);
			args++;
			continue;
		}
	}
	skfd = socket(AF_INET, SOCK_DGRAM, 0);
	if(skfd < 0) {
		perror("socket");
		return -1;
	}
	if(action == RTACTION_DEL) {
		if(ioctl(skfd, SIOCDELRT, &route) < 0) {
			perror("SIOCDELRT");
			close(skfd);
			return -1;
		}
	} else {
		if(ioctl(skfd, SIOCADDRT, &route) < 0) {
			perror("SIOCADDRT");
			close(skfd);
			return -1;
		}
	}
	(void) close(skfd);
	return 0;
}
