<?php

    session_start();

    function is_animated($avatar)
    {
        $ext = substr($avatar, 0, 2);
        if ($ext == "a_")
        {
            return ".gif";
        }
        else
        {
            return ".png";
        }
    }

    function apiRequest($url, $token, $post=FALSE, $headers=array()) {
        $ch = curl_init($url);
        curl_setopt($ch, CURLOPT_IPRESOLVE, CURL_IPRESOLVE_V4);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, TRUE);
      
        $response = curl_exec($ch);
      
      
        if($post)
          curl_setopt($ch, CURLOPT_POSTFIELDS, http_build_query($post));
      
        $headers[] = 'Accept: application/json';
      
        if($token)
          $headers[] = 'Authorization: Bearer ' . $token;
      
        curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);
      
        $response = curl_exec($ch);
        return json_decode($response);
    }

    function redirect($url)
    {
        if (!headers_sent())
        {    
            header('Location: '.$url);
            exit;
            }
        else
            {  
            echo '<script type="text/javascript">';
            echo 'window.location.href="'.$url.'";';
            echo '</script>';
            echo '<noscript>';
            echo '<meta http-equiv="refresh" content="0;url='.$url.'" />';
            echo '</noscript>';
            exit;
        }
    }

    function gen_state()
    {
        $_SESSION['state'] = bin2hex(openssl_random_pseudo_bytes(12));
        return $_SESSION['state'];
    }

    function url($clientid, $redirect, $scope)
    {
        $state = gen_state();
        return 'https://discordapp.com/oauth2/authorize?response_type=code&client_id=' . $clientid . '&redirect_uri=' . $redirect . '&scope=' . $scope . "&state=" . $state;
    }

    function get_guilds()
    {
        $url = "https://discord.com/api/users/@me/guilds";
        $headers = array ('Content-Type: application/x-www-form-urlencoded', 'Authorization: Bearer ' . $_SESSION['access_token']);
        $curl = curl_init();
        curl_setopt($curl, CURLOPT_URL, $url);
        curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($curl, CURLOPT_HTTPHEADER, $headers);
        $response = curl_exec($curl);
        curl_close($curl);
        $results = json_decode($response, true);
        return $results;
    }

    function get_guild($id)
    {
        $url = "https://discord.com/api/guilds/$id";
        $headers = array ('Content-Type: application/x-www-form-urlencoded', 'Authorization: Bearer ' . $_SESSION['access_token']);
        $curl = curl_init();
        curl_setopt($curl, CURLOPT_URL, $url);
        curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($curl, CURLOPT_HTTPHEADER, $headers);
        $response = curl_exec($curl);
        curl_close($curl);
        $results = json_decode($response, true);
        return $results;
    }    

    function join_guild($guildid)
    {
        $data = json_encode(array("access_token" => $_SESSION['access_token']));
        $url = "https://discord.com/api/guilds/$guildid/members/" . $_SESSION['user_id'];
        $headers = array ('Content-Type: application/json', 'Authorization: Bot ' . $GLOBALS['bot_token']);
        $curl = curl_init();
        curl_setopt($curl, CURLOPT_URL, $url);
        curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($curl, CURLOPT_CUSTOMREQUEST, "PUT");
        curl_setopt($curl, CURLOPT_HTTPHEADER, $headers);
        curl_setopt($curl, CURLOPT_POSTFIELDS,$data);
        $response = curl_exec($curl);
        curl_close($curl);
        $results = json_decode($response, true);
        return $results;
    }

    function add_role($guildid, $user, $roleid)
    {
        $data = json_encode(array("access_token" => $_SESSION['access_token']));
        $url = "https://discordapp.com/api/guilds/".$guildid."/members/".$user['id']."/roles/".$roleid;
        $headers = array ('Content-Type: application/json', 'Authorization: Bot ' . $GLOBALS['bot_token']);
        $curl = curl_init();
        curl_setopt($curl, CURLOPT_URL, $url);
        curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($curl, CURLOPT_CUSTOMREQUEST, "PUT");
        curl_setopt($curl, CURLOPT_HTTPHEADER, $headers);
        curl_setopt($curl, CURLOPT_POSTFIELDS,$data);
        $response = curl_exec($curl);
        curl_close($curl);
        $results = json_decode($response, true);
        return $results;
    }

    function remove_role($guildid, $user, $roleid)
    {
        $data = json_encode(array("access_token" => $_SESSION['access_token']));
        $url = "https://discordapp.com/api/guilds/".$guildid."/members/".$user['id']."/roles/".$roleid;
        $headers = array ('Content-Type: application/json', 'Authorization: Bot ' . $GLOBALS['bot_token']);
        $curl = curl_init();
        curl_setopt($curl, CURLOPT_URL, $url);
        curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($curl, CURLOPT_CUSTOMREQUEST, "DELETE");
        curl_setopt($curl, CURLOPT_HTTPHEADER, $headers);
        curl_setopt($curl, CURLOPT_POSTFIELDS,$data);
        $response = curl_exec($curl);
        curl_close($curl);
        $results = json_decode($response, true);
        return $results;
    }

    function init($redirect_url, $client_id, $client_secret, $bot_token=null)
    {
        if ($bot_token != null)    
        $bot_token = $bot_token;
        $code = $_GET['code'];
        $state = $_GET['state'];

        $url = "https://discord.com/api/oauth2/token";
        $data = array(
        "client_id" => $client_id,
        "client_secret" => $client_secret,
        "grant_type" => "authorization_code",
        "code" => $code,
        "redirect_uri" => $redirect_url
        );
        $curl = curl_init();
        curl_setopt($curl, CURLOPT_URL, $url);
        curl_setopt($curl, CURLOPT_POST, true);
        curl_setopt($curl, CURLOPT_POSTFIELDS, http_build_query($data));
        curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
        $response = curl_exec($curl);
        curl_close($curl);
        $results = json_decode($response, true);
        $_SESSION['access_token'] = $results['access_token'];
    }

    function get_user()
    {
        $url = "https://discord.com/api/users/@me";
        $headers = array ('Content-Type: application/x-www-form-urlencoded', 'Authorization: Bearer ' . $_SESSION['access_token']);
        $curl = curl_init();
        curl_setopt($curl, CURLOPT_URL, $url);
        curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($curl, CURLOPT_HTTPHEADER, $headers);
        $response = curl_exec($curl);
        curl_close($curl);
        $results = json_decode($response, true);
        $_SESSION['user'] = $results;
        $_SESSION['username'] = $results['username'];
        $_SESSION['discrim'] = $results['discriminator'];
        $_SESSION['user_id'] = $results['id'];
        $_SESSION['user_avatar'] = $results['avatar'];
        return $response;
    }

    function check_state($state)
    {
        if ($state == $_SESSION['state'])
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    function sendVerify($user, $steamprofile, $message, $url, $color) {
        $avatarURL = "https://cdn.discordapp.com/avatars/" . $user['id'] . "/" . $user['avatar'] . ".png";
     
        $webhookurl = $url;
        $steamURL = "https://steamcommunity.com/profiles/" . $steamprofile['steamid'];

        $timestamp = date("c", strtotime("now"));

        $json_data = json_encode([
           
         

           // Embeds Array
           "embeds" => [
               [
                   // Embed Title
                   "title" => $message,

                   // Embed Type
                   "type" => "rich",

                   // Timestamp of embed must be formatted as ISO8601
                   "timestamp" => $timestamp,

                   // Embed left border color in HEX
                   "color" => hexdec( $color ),

                   "thumbnail" => [
                       "url" => $avatarURL,
                   ],


                   // Additional Fields array
                   "fields" => [
                       // Field 1
                       [
                           "name" => "Discord ID",
                           "value" => $user['id'],
                           "inline" => true
                       ],
                       [
                           "name" => "Discord Mention",
                           "value" => "<@!" . $user['id'] . ">",
                           "inline" => true
                       ],
                       [
                           "name" => "Discord Name",
                           "value" => $user['username'],
                           "inline" => true
                       ],
                       // Field 2
                       [
                           "name" => "Steam Name",
                           "value" => $steamprofile['personaname'],
                           "inline" => true
                       ],
                       [
                           "name" => "Steam Profile",
                           "value" => $steamURL,
                           "inline" => true
                       ]
                       // Etc..
                   ]
               ]
           ]

       ], JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE );


       $ch = curl_init( $webhookurl );
       curl_setopt( $ch, CURLOPT_HTTPHEADER, array('Content-type: application/json'));
       curl_setopt( $ch, CURLOPT_POST, 1);
       curl_setopt( $ch, CURLOPT_POSTFIELDS, $json_data);
       curl_setopt( $ch, CURLOPT_FOLLOWLOCATION, 1);
       curl_setopt( $ch, CURLOPT_HEADER, 0);
       curl_setopt( $ch, CURLOPT_RETURNTRANSFER, 1);

       $response = curl_exec( $ch );
       echo $response;
       curl_close( $ch );
}
?>