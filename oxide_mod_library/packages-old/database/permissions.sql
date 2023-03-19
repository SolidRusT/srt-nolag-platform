GRANT ALL PRIVILEGES ON *.* TO 'modded'@'%' 
    IDENTIFIED BY 'Admin408' 
    WITH GRANT OPTION;
FLUSH PRIVILEGES;

/* GRANT ALL ON *.* TO 'modded'@'rust-east-mods' IDENTIFIED BY 'Admin408'; */
GRANT ALL ON *.* TO 'modded'@'%' IDENTIFIED BY 'Admin408';
/* GRANT ALL ON *.* TO 'modded'@'ec2-52-39-196-77.us-west-2.compute.amazonaws.com' IDENTIFIED BY 'Admin408'; */
FLUSH PRIVILEGES;