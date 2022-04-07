# HAProxy setup for MetalLB

This doc is currently fucked.

sudo apt install -y haproxy

sudo cp haproxy.cfg /etc/haproxy/haproxy.cfg
sudo service haproxy restart
systemctl status haproxy.service

* https://www.haproxy.com/blog/haproxy-configuration-basics-load-balance-your-servers/