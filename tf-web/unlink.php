<?php
    session_start();
    require('main.conf.php');
    if (!isset($_SESSION['steamid'], $_SESSION['user_id'])) header('location: /');
    include ('functions.php');
    include ('steamauth/userInfo.php');

    $check = $database->prepare("SELECT * FROM users WHERE discord_id = ? OR steam_id = ?");
    $check->execute(array($_SESSION['user_id'], $_SESSION['steamid']));
    $rowCount = $check->rowCount();

    if ($rowCount >= 1) {
        $InsertUnlink = $database->prepare("INSERT INTO users_removed SELECT * FROM users WHERE discord_id = ? OR steam_id = ?");
        $InsertUnlink->execute(array($_SESSION['user_id'], $_SESSION['steamid']));

        $RemoveUser = $database->prepare("DELETE FROM `users` WHERE discord_id = ? OR steam_id = ?");
        $RemoveUser->execute(array($_SESSION['user_id'], $_SESSION['steamid']));

        $user = get_user();
        $user = json_decode($user, true);
        sendVerify($user, $steamprofile, $user['username'] . " has unlinked their accounts!", $webhook, "#FF0000");
        remove_role($guild_id, $user, $VerifiedRoleID);

        session_destroy();
        unset($_SESSION['access_token']);
        unset($_SESSION['steamid']);

        $URL="/";
        echo "<script type='text/javascript'>document.location.href='{$URL}';</script>";
        echo '<META HTTP-EQUIV="refresh" content="0;URL=' . $URL . '">';
    }
?>