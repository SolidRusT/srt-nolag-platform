<?php include('nav_bar.php'); ?>
<div class="container" style="margin-top:35px">
	<div class="row">
    	<div class="col-md-4"><?php
if (!isset($_SESSION['steamid'])) {
	echo "<p class=\"bg-warning\">Welcome, Guest. Please <a href=\"/link\">login</a> to access more content.</p>";
} else {
	include 'steamauth/userInfo.php';
	$avatar = $steamprofile['avatar'];
    $profile_name = $steamprofile['personaname'];
    echo "<p class=\"bg-success\">Welcome, <a href=\"/profile\">$profile_name</a>!</p>";
}?></div>
	</div>
</div>
<?php include('footer.php'); ?>