<?php include 'nav_bar.php';?>
<div class="container" style="margin-top:35px">
	<div class="row">
    <div class="col-md-4"><p><?php
if (!isset($_SESSION['steamid'])) {
  echo "<p class=\"bg-warning\">Welcome, Guest. Please <a href=\"/link\">login</a> to access your SRT profile.</p>";
} else {
  include 'steamauth/userInfo.php';
  $avatar = $steamprofile['avatar'];
  $profile_name = $steamprofile['personaname'];
  echo "<p class=\"bg-success\">Welcome, <a href=\"/profile\">$profile_name</a>!</p>";
  if (isset($_SESSION['guilds'])) {
    if (isset($_SESSION['user_id'])) {
      $discordid = $_SESSION['user_id'];
      $steamid = $_SESSION['steamid'];
      echo "
      <div class=\"card\" style=\"width: 18rem;\">
        <div class=\"card-body\">
          <h5 class=\"card-title\">$profile_name&lsquo;s profile.</h5>
          <p class=\"card-text\">
          DiscordID: $discordid<br>
          SteamID: $steamid</p>
        </div>
      </div>";
    } else {
      echo "</p><p class=\"bg-danger\">Can't find Discord user</p>";
    }
  } else {
    echo "</p><p class=\"bg-warning\"><a href=\"link.php\">Link Discord</a></p>";
  }
}
?></p>
		</div>
	</div>
</div>
<?php include 'footer.php';?>