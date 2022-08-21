<?php
include 'header.php';
session_start();
require 'steamauth/steamauth.php';
include 'steamauth/userInfo.php';
include 'functions.php';
?>
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.1.0/dist/js/bootstrap.bundle.min.js" integrity="sha384-U1DAWAznBHeqEIlVSCgzq+c9gqGAJn5c/t99JyeKa9xxaYpSvHU5awsuZVVFIhvj" crossorigin="anonymous">
</script>
<nav class="navbar navbar-expand-lg navbar-dark bg-dark">
  <div class="container-fluid">
    <div class="navbar-header">
      <a class="navbar-brand" href="/index">
        <img src="/images/SoldRust_Logo.png" alt="" width="42" class="d-inline-block align-text-top">
      </a>
    </div>
    <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarSupportedContent" aria-controls="navbarSupportedContent" aria-expanded="false" aria-label="Toggle navigation">
      <span class="navbar-toggler-icon"></span>
    </button>
    <div class="collapse navbar-collapse" id="navbarNavDropdown">
      <ul class="navbar-nav me-auto mb-2 mb-lg-0">
			  <li class="nav-item dropdown">
          <a class="nav-link dropdown-toggle" href="#" id="navbarDropdownMenuLink" role="button" data-bs-toggle="dropdown" aria-expanded="false">Connect</a>
				  <ul class="dropdown-menu" aria-labelledby="navbarDropdownMenuLink">
            <li><a class="dropdown-item" href="steam://connect/us-west-10x.solidrust.net:28015">US West 10x</a></li>
	          <li><a class="dropdown-item" href="steam://connect/us-east-1000x.solidrust.net:28015">US East 1000x</a></li>
          </ul>
        </li>
        <li class="nav-item"><a class="nav-link disabled" href="#" tabindex="-1" aria-disabled="true">Shop</a></li>
        <li class="nav-item"><a class="nav-link" href="https://discord.solidrust.net">Discord</a></li>
        <li class="nav-item"><a class="nav-link" href="https://steamcommunity.com/groups/solidrust-networks">Community</a></li>
        <li class="nav-item"><a class="nav-link" href="/players">Players</a></li>
        <li class="nav-item"><a class="nav-link disabled" href="#" tabindex="-1" aria-disabled="true">Gallery</a></li>
        <li class="nav-item"><a class="nav-link disabled" href="#" tabindex="-1" aria-disabled="true">Help</a></li>
        <li class="nav-item"><a class="nav-link disabled" href="#" tabindex="-1" aria-disabled="true">Report</a></li>
	    </ul>
	    <ul class="navbar-nav navbar-right"><?php
	if (!isset($_SESSION['steamid'])) {
		$target = "/link";
		$linkname = "Account Login";
		echo "<li class=\"nav-item\"><a class=\"navbar-text\" href=\"$target\">$linkname</a>></li>";
	} else {
		$target = "/profile";
		$avatar = $steamprofile['avatar'];
		$profile_name = $steamprofile['personaname']; echo "
		<li class=\"nav-item\"><a class=\"navbar-text\" href=\"$target\">$profile_name</a>&nbsp;&nbsp;&nbsp;</li>
		<li class=\"nav-item\"><a class=\"navbar-text\" href=\"$target\"><img src=\"$avatar\"></a>&nbsp</li>";
	}
	?><li><?php
	if (isset($_SESSION['steamid'])) {
		logoutbutton("rectangle");
	}
	?></li>
      </ul>
    </div>
  </div>
</nav>