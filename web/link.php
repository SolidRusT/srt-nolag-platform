<?php
include('header.php');
session_start();
require 'steamauth/steamauth.php';
require 'main.conf.php';
include 'steamauth/userInfo.php';
include 'functions.php';
if (isset($_SESSION['steamid'])) {
    header('location: discord.php');
} else {
    include 'steamauth/userInfo.php';
}
?>
	<link rel="stylesheet" href="css/login.css">
    <script src="https://kit.fontawesome.com/9e14982b30.js" crossorigin="anonymous"></script>
    <div id="logincontent">
        <center>Step 1: Steam Auth (Click Steam Button)</center>
        <br />
        <a href="?login" style="font-size:30px; background-color: #444;" class="steamButton"><i class="fa fa-steam"
                aria-hidden="true"></i> Login with Steam</a>
    </div>

<?php include('footer_simple.php'); ?>