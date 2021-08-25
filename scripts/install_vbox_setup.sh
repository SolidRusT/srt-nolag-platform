# Run this to create an Amazon AMI-like enviro using VirtualBox

apt update
apt install -y build-essential linux-headers-$(uname -r) net-tools wget curl
mkdir -p /mnt/cdrom
mount /dev/cdrom /mnt/cdrom
cd /mnt/cdrom
./VboxLinuxAdditions.run
reboot

cd solidrust.net

#use install_3 and continue to follow the the scripts