create database PermissionGroupSync;
create database RustPlayers;
create database oxide;

CREATE TABLE IF NOT EXISTS oxide.ibn_table (
    `id` INT(11) NOT NULL AUTO_INCREMENT,
    `itransaction_id` VARCHAR(60) NOT NULL,
    `ipayerid` VARCHAR(60) NOT NULL,
    `iname` VARCHAR(60) NOT NULL,
    `iemail` VARCHAR(60) NOT NULL,
    `itransaction_date` DATETIME NOT NULL,
    `ipaymentstatus` VARCHAR(60) NOT NULL,
    `ieverything_else` TEXT NOT NULL,
    `item_name` VARCHAR(255) DEFAULT NULL,
    `claimed` INT(11) NOT NULL DEFAULT '0',
    `claim_date` DATETIME DEFAULT NULL,
    PRIMARY KEY (`id`)
)  ENGINE=MYISAM AUTO_INCREMENT=9 DEFAULT CHARSET=LATIN1;

CREATE DEFINER=`root`@`localhost` PROCEDURE oxide.claim_donation(IN email_address VARCHAR(255))
BEGIN

set email_address = REPLACE(email_address,'@@','@');

set @ID = (
select    IBN.id
from    oxide.ibn_table as IBN
where    IBN.iemail = email_address
        and IBN.claimed = 0
        and IBN.claim_date IS NULL
        and IBN.ipaymentstatus = "Completed"
ORDER BY IBN.itransaction_date DESC
LIMIT 1);

UPDATE oxide.ibn_table
SET    claimed = 1, claim_date = NOW()
WHERE id = @ID;

select    IBN.item_name
from    oxide.ibn_table as IBN
where    IBN.id = @ID;

END