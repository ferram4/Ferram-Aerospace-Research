#!/bin/bash

printf "Localization\n{"

for CFGFILE in *.cfg
do
	CFGNAME=${CFGFILE}
	CFGNAME=${CFGNAME%.cfg}
	CFGNAME=${CFGNAME#Localization_}
	printf "\n\t"
	printf '%s' "$CFGNAME"
	printf "\n\t{\n"

	cat $CFGFILE | sed -e $'s/^/\t\t/g'

	printf "\n\t}\n"
done

printf "}"
