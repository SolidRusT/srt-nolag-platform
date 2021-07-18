<?php
session_start();
require 'steamauth/steamauth.php';
require 'main.conf.php';

if (isset($_SESSION['steamid'])) {
    header('location: discord.php');
} else {
    include 'steamauth/userInfo.php';
}
?>

<html>

<head>
    <title><?php echo $SiteTitle; ?></title>
    <meta charset="utf-8">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="description" content="SolidRusT Server Account Linking">
    <meta name="keywords" content="rust, SolidRusT, SRT, NOLAG, link, rust server, server">
    <meta name="author" content="Suparious">
    <meta property="og:title" content="SolidRusT &bull; Account Linking">
    <meta property="og:description" content="Link your Discord and Steam accounts to our servers.">
    <meta property="og:image" content="https://solidrust.net/images/SoldRust_Logo.png">
    <meta property="og:site_name" content="solidrust.net">
    <meta name="theme-color" content="#ff0000">
    <link href="https://fonts.googleapis.com/css?family=Montserrat:200,400,700" rel="stylesheet">
    <script src="https://kit.fontawesome.com/9e14982b30.js" crossorigin="anonymous"></script>
    <link type="text/css" rel="stylesheet" href="css/main.css" />
    <link type="text/css" rel="stylesheet" href="css/login.css">
    <style type="text/css">
    body {
        background-image: url(images/SR-Demo-Loot-1.PNG);
    }
    </style>
    <script async src="https://www.googletagmanager.com/gtag/js?id=UA-188473063-1"></script>
    <script>
    window.dataLayer = window.dataLayer || [];

    function gtag() {
        dataLayer.push(arguments);
    }
    gtag('js', new Date());

    gtag('config', 'UA-188473063-1');
    </script>


</head>

<body>

    <div class="container">
        <div class="logo">
            <img src="<?php echo $logoURL; ?>" alt="" width="130" />
        </div>
        <nav>
            <ul>
                <li><a href='/'>Home</a></li>
            </ul>
        </nav>
    </div>
    <div id="logincontent">
        <center>Step 1: Steam Auth (Click Steam Button)</center>
        <br />
        <a href="?login" style="font-size:30px; background-color: #444;" class="steamButton"><i class="fa fa-steam"
                aria-hidden="true"></i> Login with Steam</a>
    </div>


    <a href="https://topg.org/rust-servers/server-631719" target="_blank"><img src="https://topg.org/topg2.gif"
            width="88" height="31" border="0" alt="Solidrust 5x trio - Rust server"></a>
    <a href="https://ipv6-test.com/validate.php?url=referer"><img src="https://ipv6-test.com/button-ipv6-80x15.png"
            alt="ipv6 ready" title="ipv6 ready" border="0" /></a>
</body>

</html>
