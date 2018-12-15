@echo off
:start
cls
echo PTMagic Git Commands
echo The following Commands are available:
echo 1: Sync current Branch with Main Repo(This will remove all changes you made!)
SET /P Input= Please enter an input(1): 

IF "%Input%" == "1" GOTO :sync

GOTO :start

:sync
git remote add upstream https://github.com/PTMagicians/PTMagic.git
git fetch upstream
git checkout develop
git reset --hard upstream/develop  
git push origin develop --force
git pull origin develop
GOTO :end

:end
pause