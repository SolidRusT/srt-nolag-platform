<?php
include('nav_bar_simple.php');
session_start();
require 'steamauth/steamauth.php';
require 'main.conf.php';
include 'steamauth/userInfo.php';
include 'functions.php';
?>
<ul class="nav navbar-nav navbar-right">
<li><?php
if (!isset($_SESSION['steamid'])) {
	$target = "/link";
	$linkname = "Account Login";
	echo "<a href=\"$target\">$linkname</a>" ;
} else {
	include 'steamauth/userInfo.php';
    $avatar = $steamprofile['avatar'];
    $profile_name = $steamprofile['personaname'];
	echo "<img src=\"$avatar\" align=\"top\"> $profile_name";
}
?></li>
<li><?php
if (isset($_SESSION['steamid'])) {
	logoutbutton("rectangle");
}
?></li>
</ul>
</div>
</nav>