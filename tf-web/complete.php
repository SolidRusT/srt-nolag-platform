<?php
include('header.php');
    session_start();
    require('main.conf.php');
    if (!isset($_SESSION['steamid'], $_SESSION['user_id'])) header('location: /');

    include ('steamauth/userInfo.php');
    include ('functions.php');

    function getUserIP() {
  
        $client  = @$_SERVER['HTTP_CLIENT_IP'];
        $forward = @$_SERVER['HTTP_X_FORWARDED_FOR'];
        $remote  = $_SERVER['REMOTE_ADDR'];
      
        if(filter_var($client, FILTER_VALIDATE_IP)) { $ip = $client; }
        elseif(filter_var($forward, FILTER_VALIDATE_IP)) { $ip = $forward; }
        else { $ip = $remote; }
      
        return $ip;
    }

    $check = $database->prepare("SELECT * FROM users WHERE discord_id = ? OR steam_id = ?");
    $check->execute(array($_SESSION['user_id'], $_SESSION['steamid']));
    $rowCount = $check->rowCount();

    if ($rowCount >= 1) {
        $status = "Discord ID or Steam ID already exists. Data updated in database.";

        $ip = getUserIP();
        $date = new DateTime(null, new DateTimeZone('America/New_York'));
        $timestamp = $date->getTimestamp();

        $updateQuery = $database->prepare("UPDATE users SET `steam_name` = ?, `discord_name` = ?, `discord_discrim` = ?, `user_locale` = ?, `timestamp` = ?, `access_token` = ? WHERE steam_id = ?");
        $updateQuery->execute(array($_SESSION['steam_personaname'], $_SESSION['username'], $_SESSION['user']['discriminator'], $_SESSION['user']['locale'], $timestamp, $_SESSION['access_token'], $_SESSION['steamid']));

    } else {
        $status = "Success! You may now leave this page.";
        
        $ip = getUserIP();
        $date = new DateTime(null, new DateTimeZone('America/New_York'));
        $timestamp = $date->getTimestamp();

        $insertQuery = $database->prepare("INSERT INTO `users`(`id`, `steam_id`, `steam_name`, `discord_id`, `discord_name`, `discord_discrim`, `user_locale`, `user_ip`, `nitro`, `staff_flag`, `timestamp`, `access_token`) VALUES (NULL,?,?,?,?,?,?,?,?,?,?,?)");
        $insertQuery->execute(array($_SESSION['steamid'], $_SESSION['steam_personaname'], $_SESSION['user_id'], $_SESSION['username'], $_SESSION['user']['discriminator'], $_SESSION['user']['locale'], $ip, 0, 0, $timestamp, $_SESSION['access_token']));

        $user = get_user();
        $user = json_decode($user, true);

        sendVerify($user, $steamprofile, $user['username'] . " has succesfully verified their accounts!", $webhook, "#00FF00");
        add_role($guild_id, $user, $VerifiedRoleID);
        
        
    }
?>
<link rel="stylesheet" href="css/login.css">
    <link href="vendor/bootstrap/css/bootstrap.min.css" rel="stylesheet">
    <script src="https://kit.fontawesome.com/9e14982b30.js" crossorigin="anonymous"></script>
        <div class="container">
            <div class="logo">
                <img src="<?php echo $logoURL; ?>" alt="" width="130"/>
            </div>
            <nav>
                <ul>
                    <li><a href='/'>Home</a></li>
                </ul>
            </nav>
        </div>
        <div id="logincontent" style='height:325px;'>
            <center>
                <font color='yellow'><?php echo $status; ?></font>
                <br /><br />
                <font color='cyan'>Discord:</font> <?php echo $_SESSION['username'] . "#" . $_SESSION['discrim'] . " - " . $_SESSION['user_id']; ?>
                <br />
                <font color='lime'>Steam:</font> <?php echo $_SESSION['steam_personaname'] . " - " . $_SESSION['steamid']; ?>
                <br /><br />
                <img style='margin-right: 5px; border-radius: 4px; border: 1px solid cyan; display: inline-block; height:125px;' src="<?php echo "https://cdn.discordapp.com/avatars/{$_SESSION['user_id']}/{$_SESSION['user_avatar']}.png" ?>">
                <img style='margin-left: 5px; border-radius: 4px; border: 1px solid lime; display: inline-block; height:125px;' src="<?php echo $_SESSION['steam_avatarfull'];?>">
				<ul>
					<li><a href='/'>Back to Home</a></li>
				</ul>
				
            </center>
        </div>
<?php header('Refresh: 1; URL=/index'); ?>
<?php include('footer_simple.php'); ?>