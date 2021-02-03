#!/bin/bash

# Color Codes

Red='\033[0;31m'          # Red
Green='\033[0;32m'        # Green
Color_Off='\033[0m'       # Text Reset

#Init Vars

BASEDIR=$PWD

#Main Script

clear

if [[ $EUID -ne 0 ]];
then
   echo "This script must be run as root"
   exit
fi

echo "Profit Trailer Magic Auto Updater"
echo ""
echo "This Programm will update you to a Version of PTM you choose"
echo ""
echo -e "Please make sure that PTM is ${Red}NOT RUNNING!!!${Color_Off}"
echo ""
echo "Continue? (yes/no)"

read start

if [ "$start"  != "yes" ]
then
	echo "exiting"
	exit
fi

echo ""
echo "Please input the version NR you want to update to."
echo "It can be found on: https://github.com/PTMagicians/PTMagic/releases"
echo "Example: 2.5.1"

read VERSION

echo ""
echo "Checking for Required Programms:"

if ! command -v unzip &> /dev/null
then
        echo -e "Unzip: ${Red}[\u274c]${Color_Off}"
	UNZIPINSTALL="unzip"
else
        echo -e "Unzip: ${Green}[\u2714]${Color_Off}"
	UNZIPINSTALL=""
fi

if ! command -v wget &> /dev/null
then
        echo -e "Wget: ${Red}[\u274c]${Color_Off}"
        WGETINSTALL="wget"
else
        echo -e "Wget: ${Green}[\u2714]${Color_Off}"
        WGETINSTALL=""
fi


if [[ $UNZIPINSTALL != "" ]] || [[ $WGETINSTALL != "" ]]
then
	echo ""
	echo "Installing missing programms..."
	apt install -y $UNZIPINSTALL $WGETINSTALL >/dev/null
	echo "Missing Programms installed"
fi

echo ""
echo "Removing old upgrade files..."
rm -r -f -d $PWD/temp >/dev/null

echo ""
echo "Downloading PTM Version $VERSION..."

mkdir "$PWD/temp" >/dev/null
cd "$PWD/temp"

wget "https://github.com/PTMagicians/PTMagic/releases/download/$VERSION/PTM_$VERSION.zip" --output-file=logfile

if [ ! -f $PWD/PTM_$VERSION.zip ]; 
then
	echo -e "${Red}Download Failed${Color_Off}"
	exit
fi

echo ""
echo "Unpacking Archive..."

unzip $PWD/PTM_$VERSION.zip >/dev/null

echo ""
echo "Moving Files..."

cp -r $PWD/PTM_$VERSION/PTMagic/. $BASEDIR/

echo ""
echo "Cleaning up..."

cd $BASEDIR

rm -r -d $PWD/temp >/dev/null

echo ""
echo "Done"
