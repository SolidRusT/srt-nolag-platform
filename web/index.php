<?php
session_start();
require 'steamauth/steamauth.php';
require 'main.conf.php';
include 'steamauth/userInfo.php';
include 'functions.php';
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
    <link type="text/css" rel="stylesheet" href="css/main.css" />
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
    <table style="width:100%">
        <tr valign="top">
            <p>
                <img src="images/SoldRust_Banner_trans.png" alt="SolidRusT Networks" />
                <?php
if (!isset($_SESSION['steamid'])) {
    loginbutton("rectangle"); //login button
} else {
    include 'steamauth/userInfo.php'; //To access the $steamprofile array
    $avatar = $steamprofile['avatar'];
    $profile_name = $steamprofile['personaname'];
    //Protected content
    logoutbutton("rectangle");
    echo "<h3><img src=\"$avatar\" align=\"top\">  $profile_name";
    echo "</h3>";
    if (isset($_SESSION['guilds'])) {
        echo "<h3>Discord is Linked</h3>";
        if (isset($_SESSION['user_id'])) {
            $user_id = $_SESSION['user_id'];
                echo "Discord User: $user_id";
        } else {
                echo "Can't find Discord user";
        }
    } else {
        echo "<h4><a href=\"link.php\">Link Discord</a></h4>";
    }
}
?>
            </p>
        </tr>
        <tr>
            <td valign="top">
                <script type="application/javascript">
                window.addEventListener('message', function(e) {
                    if (e.data.uid && e.data.type === 'sizeUpdate') {
                        var i = document.querySelector('iframe[name="' + e.data.uid + '"]');
                        i.style.width = e.data.payload.width;
                        i.style.height = e.data.payload.height;
                    }
                });
                </script><iframe
                    src="https://cdn.battlemetrics.com/b/horizontal500x80px/11593491.html?_token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6Imhvcml6b250YWw1MDB4ODBweCIsInNlcnZlciI6MTE1OTM0OTEsIm9wdGlvbnMiOnsiZm9yZWdyb3VuZCI6IiNFRUVFRUUiLCJiYWNrZ3JvdW5kIjoiIzIyMjIyMiIsImxpbmVzIjoiIzMzMzMzMyIsImxpbmtDb2xvciI6IiNmZmUzMDAiLCJjaGFydENvbG9yIjoiI0ZGMDcwMCJ9LCJsaXZlVXBkYXRlcyI6dHJ1ZSwidXNlcl9pZCI6MzU4MjIwLCJpYXQiOjE2MjM2MDU4OTh9.BJgFohCkxMaXHFEQzEiA4OwytUIPvKCH5nGwivQweR4"
                    frameborder=0 style="border:0" name="zweam"></iframe><br>



                <object data='https://www.youtube.com/embed/rVZktA3WwE4?autoplay=0' width='560px' height='315px'>
                </object>
            </td>
            <td>
                <p>
                    widgest go here
                </p>
            </td>

        </tr>
    </table>
    <a href="https://topg.org/rust-servers/server-631719" target="_blank"><img src="https://topg.org/topg2.gif"
            width="88" height="31" border="0" alt="Solidrust 5x trio - Rust server"></a>
    <a href="https://ipv6-test.com/validate.php?url=referer"><img src="https://ipv6-test.com/button-ipv6-80x15.png"
            alt="ipv6 ready" title="ipv6 ready" border="0" /></a>
</body>
</html>