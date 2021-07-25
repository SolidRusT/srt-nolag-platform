#!/bin/bash
export NEW_NAME="nine"

mkdir -p ${GAME_ROOT}
mkfs -t xfs /dev/nvme0n1

sed -i "/nine/d" /etc/hosts /etc/cloud/templates/hosts.debian.tmpl

echo ${NEW_NAME} | tee /etc/hostname
echo "127.0.0.1    ${NEW_NAME}" | tee -a /etc/hosts /etc/cloud/templates/hosts.debian.tmpl
echo "127.0.0.1    ${NEW_NAME}" | tee -a /etc/cloud/templates/hosts.debian.tmpl
hostnamectl set-hostname ${NEW_NAME}

sudo dpkg-reconfigure tzdata
#America/Los_Angeles or America/New_York

reboot
