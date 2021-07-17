<?php
    session_start();
    if(!isset($_SESSION['steamid'])) header('location: /');
    if(isset($_SESSION['guilds'])) header('location: complete');
    require('functions.php');
    require('main.conf.php');

    if (isset($_GET['code'])) {
        $_SESSION['code'] = $_GET['code'];
        init($redirect, $client_id, $client_secret, $bot_token);
        get_user();
        join_guild($guild_id);
        $_SESSION['guilds'] = get_guilds();
        redirect("complete");
    }
?>

<html>
    <head>
        <title><?php echo $SiteTitle; ?> &bull; Verification</title>
        <link rel="stylesheet" href="style.css">
        <link href="vendor/bootstrap/css/bootstrap.min.css" rel="stylesheet">
        <script src="https://kit.fontawesome.com/9e14982b30.js" crossorigin="anonymous"></script>
    </head>
    <body>
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
        <div id="logincontent">
            <center>Step 2: Discord Auth (Click Discord Button)</center>
            <br />
            <a href="<?php echo url($client_id, $redirect, $scope); ?>" style="font-size:30px; background-color: #444;" class="discordButton"><i class="fab fa-discord" aria-hidden="true"></i> Login with Discord</a>
        </div>
    </body>
</html>