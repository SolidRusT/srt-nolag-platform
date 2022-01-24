-- MySQL dump 10.19  Distrib 10.3.29-MariaDB, for debian-linux-gnu (aarch64)
--
-- Host: localhost    Database: srt_web_auth
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
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `users` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `discord_id` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `steam_id` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `user_ip` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `access_token` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nitro` int(11) NOT NULL DEFAULT 0,
  `steam_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `discord_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `discord_discrim` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `user_locale` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `timestamp` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `staff_flag` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `discord_id` (`discord_id`),
  KEY `steam_id` (`steam_id`),
  KEY `user_ip` (`user_ip`)
) ENGINE=InnoDB AUTO_INCREMENT=8 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `users`
--

LOCK TABLES `users` WRITE;
/*!40000 ALTER TABLE `users` DISABLE KEYS */;
INSERT INTO `users` VALUES (1,'163085987293954049','76561198024774727','24.80.116.178','kEnCW2Uszrb5BgH0iPb5aaSPihASgW',1,'Suparious','Suparious','5638','en-US','1630648081',0),(2,'140496445798219776','76561198907351815','100.34.192.37','rV3PDTns08ePzH5zGa9o1qfKECdoy8',0,'Ryz0r','Ryz0r','0101','en-US','1626812081',0),(3,'857297025455226900','76561198416399632','50.92.183.181','acUmYis2c803YdYAtyrJdHNLPzN0oX',0,'joe_3451','farmer_joe','1344','en-US','1626845879',0),(4,'804395134481203232','76561198886543733','50.64.128.18','xcfdtamXtVCvZsY0icbGvCnqQKXfbq',0,'SmokeQc','SolidRusT','5854','en-US','1629770474',0),(5,'879544447208148992','76561199182551125','50.64.128.18','xcfdtamXtVCvZsY0icbGvCnqQKXfbq',0,'SolidRusT','SolidRusT','5854','en-US','1629770604',0),(6,'791140465575067669','76561198412883374','144.137.70.253','tGfpPfr7gSmnUu7CiegtrFrB7VHez9',0,'miketaylor121','miketaylor1928','9394','en-US','1630303664',0),(7,'166292765850861570','76561198404312567','72.85.38.48','bV7JPeytX5W9mZ4m68bsHMUaVT17Hn',0,'Rango™','Rango','1563','en-US','1630901043',0);
/*!40000 ALTER TABLE `users` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `users_removed`
--

DROP TABLE IF EXISTS `users_removed`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `users_removed` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `discord_id` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `steam_id` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `user_ip` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `access_token` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nitro` int(11) NOT NULL DEFAULT 0,
  `steam_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `discord_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `discord_discrim` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `user_locale` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `timestamp` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `staff_flag` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `discord_id` (`discord_id`),
  KEY `steam_id` (`steam_id`),
  KEY `user_ip` (`user_ip`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `users_removed`
--

LOCK TABLES `users_removed` WRITE;
/*!40000 ALTER TABLE `users_removed` DISABLE KEYS */;
/*!40000 ALTER TABLE `users_removed` ENABLE KEYS */;
UNLOCK TABLES;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2021-09-06  6:17:28
