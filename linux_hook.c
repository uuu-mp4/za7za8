#define _GNU_SOURCE
#include <stdio.h>
#include <ctype.h>
#include <string.h>
#include <stdlib.h>
#include <dlfcn.h>
#include <openssl/ossl_typ.h>

static void writelog(void *buf,int len)
{
	FILE *fd=fopen("/home/hook.txt","a+");
	fwrite(buf,len,1,fd);
	fwrite("\n",1,1,fd);
	fclose(fd);
}

//build:gcc -g -Wall -fPIC -shared -o hook.so hook.c -ldl
//usage:LD_PRELOAD=/home/hook.so /usr/local/lfradius/lfradiusd
int RSA_public_decrypt(int inlen,unsigned char *in,unsigned char *out,RSA *rsa,int padding)
{
	//原始函数声明
	int (*origin_RSA_public_decrypt)(int,unsigned char*,unsigned char*,RSA*,int);
	//原始函数地址
	origin_RSA_public_decrypt=dlsym(RTLD_NEXT,"RSA_public_decrypt");
	
	char *outbuf=calloc(1,inlen);
	int len=(*origin_RSA_public_decrypt)(inlen,in,(unsigned char*)outbuf,rsa,padding);
	if(len>1&&NULL!=strstr(outbuf,"regname"))
	{
		char* prefix=strstr(outbuf,"regpoint=");
		char* suffix=strstr(prefix,"\n");
		int lead=(prefix-outbuf)+9;//前导长度
		if(NULL!=prefix&&NULL!=suffix)
		{
			strncpy((char*)out,outbuf,lead);
			strncpy((char*)out+lead,"999999999",9);
			lead+=9;
			strncpy((char*)out+lead,suffix,len-(suffix-outbuf));
			writelog(out,strlen((char*)out));
			len=strlen((char*)out);
		}
		else
		{
			memcpy(out,outbuf,len);
		}
		free(outbuf);
		return len;
	}
	else
	{
		free(outbuf);
		return -1;
	}
}
