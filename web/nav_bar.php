<?php
include 'header.php';
session_start();
require 'steamauth/steamauth.php';
require 'main.conf.php';
include 'steamauth/userInfo.php';
include 'functions.php';
?>
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.1.0/dist/js/bootstrap.bundle.min.js" integrity="sha384-U1DAWAznBHeqEIlVSCgzq+c9gqGAJn5c/t99JyeKa9xxaYpSvHU5awsuZVVFIhvj" crossorigin="anonymous">
    </script>
    <nav class="navbar navbar-expand-lg navbar-dark bg-dark">
        <div class="container-fluid">
		  <div class="navbar-header">
            <a class="navbar-brand" href="/index">
              <img src="/images/SoldRust_Logo.png" alt="" width="42" class="d-inline-block align-text-top"></a>
		  </div>
          <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarSupportedContent" aria-controls="navbarSupportedContent" aria-expanded="false" aria-label="Toggle navigation">
      <span class="navbar-toggler-icon"></span>
    </button>
            <div class="collapse navbar-collapse" id="navbarSupportedContent">
                <ul class="navbar-nav me-auto mb-2 mb-lg-0">
                    <li class="nav-item">
                        <a class="nav-link active" aria-current="page" href="/index">Home</a>
                    </li>
                    <li class="nav-item">
                        <a class="nav-link" href="#">Discord</a>
                    </li>
                    <li>
                        <a class="nav-link" href="#">Store</a>
                    </li>
                    <li>
                        <a class="nav-link" href="#">Connect</a>
                    </li>
                    <li>
                        <a class="nav-link" href="#">Apply</a>
                    </li>
                    <li>
                        <a class="nav-link" href="#">Learn</a>
                    </li>
                    <li>
                        <a class="nav-link" href="#">Gallery</a>
                    </li>
                    <li>
                        <a class="nav-link" href="#">Help</a>
                    </li>
                    <li>
                        <a class="nav-link disabled" href="#" tabindex="-1" aria-disabled="true">Report</a>
                    </li>
                </ul><span class="navbar-text"><?php
				if (!isset($_SESSION['steamid'])) {
					$target = "/link";
					$linkname = "Account Login";
					echo "<a href=\"$target\">$linkname</a>";
				} else {
					$avatar = $steamprofile['avatar'];
					$profile_name = $steamprofile['personaname'];
					echo "<img src=\"$avatar\" align=\"top\"> $profile_name";
				}
				?>&nbsp;&nbsp;<?php
				if (isset($_SESSION['steamid'])) {
					logoutbutton("rectangle");
				}
				?></span>
            </div>
        </div>
    </nav>