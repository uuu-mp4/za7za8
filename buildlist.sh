#!/bin/sh

function ergo(){
	local pardir
	local curdir
	pardir=$1 #父级目录
	curdir=$2 #当前目录
	cd $curdir
	for fp in ./*; do
		local fname
		fname=$(basename $fp)
		if [ -f "$curdir/$fname" ]; then
			echo "$curdir/$fname"
		elif [ -d "$curdir/$fname" ]; then
			ergo $curdir "$curdir/$fname"
		fi
	done
	cd $pardir
}

for ppath in /usr/lib/*; do
	bname=$(basename $ppath)
	if [ -d /usr/lib/$bname ]; then
		ergo /usr/lib /usr/lib/$bname
	elif [ -f /usr/lib/$bname ]; then
		echo /usr/lib/$bname
	fi
done
