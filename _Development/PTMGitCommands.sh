#!/bin/bash
clear

## Compliments to David C. Rankin
## https://stackoverflow.com/questions/30182086/how-to-use-goto-statement-in-shell-script#answer-30182634

## array of menu entries
entries=( "Sync current Branch with Main Repo(This will remove all changes you made!)"
          "Exit" )

## set prompt for select menu
PS3='Selection: '

function pause(){
    echo "Press any key to continue..."
   read -p "$*"
}

while [ "$menu" != "2" ]; do                ## outer loop redraws menu each time
    echo "PTMagic Git Commands"
    echo "The following Commands are available:"
    printf "\n\nMain Menu:\n\n"             ## heading for menu
    select choice in "${entries[@]}"; do  ## select displays choices in array
        case "$choice" in                 ## case responds to choice
            "Sync current Branch with Main Repo(This will remove all changes you made!)" )
                git remote add upstream https://github.com/PTMagicians/PTMagic.git
                git fetch upstream
                git checkout develop
                git reset --hard upstream/develop  
                git push origin develop --force
                git pull origin develop

                break                     ## break returns control to outer loop
                ;;
            "Exit" )         
                clear
                exit 0                    ## variable setting exit condition
                break
                ;;
            * )
                echo "Invalid option"
                pause
                break
                ;;
        esac
    done
done

