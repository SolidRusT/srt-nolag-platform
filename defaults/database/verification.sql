-- phpMyAdmin SQL Dump
-- version 5.1.0
-- https://www.phpmyadmin.net/
--
-- Host: localhost
-- Generation Time: Jul 01, 2021 at 11:40 PM
-- Server version: 8.0.25-0ubuntu0.20.04.1
-- PHP Version: 8.0.7

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `legends`
--

-- --------------------------------------------------------

--
-- Table structure for table `users`
--

CREATE TABLE `users` (
  `id` int NOT NULL,
  `discord_id` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `steam_id` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `user_ip` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `access_token` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nitro` int NOT NULL DEFAULT '0',
  `steam_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `discord_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `discord_discrim` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `user_locale` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `timestamp` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `staff_flag` int NOT NULL DEFAULT '0'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Table structure for table `users_removed`
--

CREATE TABLE `users_removed` (
  `id` int NOT NULL,
  `discord_id` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `steam_id` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `user_ip` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `access_token` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nitro` int NOT NULL DEFAULT '0',
  `steam_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `discord_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `discord_discrim` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `user_locale` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `timestamp` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N/A',
  `staff_flag` int NOT NULL DEFAULT '0'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Indexes for dumped tables
--

--
-- Indexes for table `users`
--
ALTER TABLE `users`
  ADD PRIMARY KEY (`id`),
  ADD KEY `discord_id` (`discord_id`),
  ADD KEY `steam_id` (`steam_id`),
  ADD KEY `user_ip` (`user_ip`);

--
-- Indexes for table `users_removed`
--
ALTER TABLE `users_removed`
  ADD PRIMARY KEY (`id`),
  ADD KEY `discord_id` (`discord_id`),
  ADD KEY `steam_id` (`steam_id`),
  ADD KEY `user_ip` (`user_ip`);

--
-- AUTO_INCREMENT for dumped tables
--

--
-- AUTO_INCREMENT for table `users`
--
ALTER TABLE `users`
  MODIFY `id` int NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `users_removed`
--
ALTER TABLE `users_removed`
  MODIFY `id` int NOT NULL AUTO_INCREMENT;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
