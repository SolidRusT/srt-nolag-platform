-- MySQL dump 10.19  Distrib 10.3.29-MariaDB, for debian-linux-gnu (aarch64)
--
-- Host: localhost    Database: PermissionGroupSync
-- ------------------------------------------------------
-- Server version	10.3.29-MariaDB-0+deb10u1

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `permissiongroupsync`
--

DROP TABLE IF EXISTS `permissiongroupsync`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `permissiongroupsync` (
  `id` int(32) NOT NULL AUTO_INCREMENT,
  `steamid` varchar(17) DEFAULT NULL,
  `groupname` varchar(255) DEFAULT NULL,
  `serverid` varchar(255) NOT NULL DEFAULT '_ALL',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=17 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `permissiongroupsync`
--

LOCK TABLES `permissiongroupsync` WRITE;
/*!40000 ALTER TABLE `permissiongroupsync` DISABLE KEYS */;
INSERT INTO `permissiongroupsync` VALUES (1,'76561198886543733','discord','_ALL'),(2,'76561199135759930','discord','_ALL'),(3,'76561198421090963','discord','_ALL'),(4,'76561198852895608','discord','_ALL'),(5,'76561198024774727','discord','_ALL'),(6,'76561198852895608','vip','_ALL'),(7,'76561198421090963','vip','_ALL'),(8,'76561199135759930','vip','_ALL'),(9,'76561198886543733','dev','_ALL'),(10,'76561198206550912','GM','_ALL'),(11,'76561198024774727','admin','_ALL'),(12,'76561199078529202','discord','_ALL'),(13,'76561199051464652','discord','_ALL'),(14,'76561199078529202','vip','_ALL'),(15,'76561199051464652','vip','_ALL'),(16,'76561199016007366','discord','_ALL');
/*!40000 ALTER TABLE `permissiongroupsync` ENABLE KEYS */;
UNLOCK TABLES;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2021-09-06  7:51:06
