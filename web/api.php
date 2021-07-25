<?php

include('main.conf.php');
header("Content-Type: application/json; charset=utf-8");

if (!isset($_GET['action']) || !isset($_GET['secret']))
{
    http_response_code(400);
    die();
}

if ($_GET['secret'] != '2f7ea85f8b8')
{
    http_response_code(403);
    die();
}

switch ($_GET['action']) {
    case 'isLinkedSteamBulk':
    case 'steamChecks':
    {
        if (!isset($_GET['id'])) {
            http_response_code(400);
            break;
        }

        $values = explode(",", $_GET['id']);

        $placeholders = str_repeat('?, ', count($values) - 1) . '?';
        $query = $database->prepare("SELECT steam_id FROM users WHERE steam_id IN($placeholders)");
        $query->execute($values);

        $result = $query->fetchAll(PDO::FETCH_ASSOC);

        $present = array_fill_keys(array_column($result, 'steam_id'), true);
        $allValues = array_fill_keys($values, false);
        $result = array_replace($allValues, $present);

        echo json_encode($result);
        break;
    }



    case 'update':
    {
        if (!isset($_GET['id']) || !isset($_GET['mode']) || !isset($_GET['role'])) {
            http_response_code(400);
            break;
        }
        
        switch ($_GET['mode']) {
            case "add": $modeDiscord = "PUT";
                break;
            case "remove": $modeDiscord = "DELETE";
                break;
            default: http_response_code(400);
                break;
        }
        
        $values = explode(",", $_GET['id']);
        $errors = false;
        
        $placeholders = str_repeat('?, ', count($values) - 1) . '?';
        $query = $database->prepare("SELECT discord_id, steam_id, access_token FROM users WHERE steam_id IN($placeholders)");
        $query->execute($values);

        $result = $query->fetchAll(PDO::FETCH_ASSOC);
        foreach ($result AS $row) {
            $present = array_fill_keys(array_column($result, 'steam_id'), true);
            $allValues = array_fill_keys($values, false);
            $result = array_replace($allValues, $present);

            $curl = curl_init();
            $post_data = json_encode(array("access_token" => $row["access_token"]), JSON_FORCE_OBJECT);

            curl_setopt_array($curl, array(
                CURLOPT_URL => "https://discordapp.com/api/guilds/".$guild_id."/members/".$row["discord_id"]."/roles/".$_GET['role'],
                CURLOPT_RETURNTRANSFER => true,
                CURLOPT_ENCODING => "",
                CURLOPT_MAXREDIRS => 10,
                CURLOPT_POSTFIELDS => $post_data,
                CURLOPT_TIMEOUT => 30,
                CURLOPT_CUSTOMREQUEST => $modeDiscord,
                CURLOPT_HTTPHEADER => array(
                    "Authorization: Bot " . $bot_token,
                    "Content-Type: application/json"
                ),
            ));

            $response = curl_exec($curl);
            $err = curl_error($curl);

            curl_close($curl);

            if ($err) {
                $errors = true;
            }
        }
        
        if ($errors) { echo json_encode("Errors while updating VIP"); }
        else { echo json_encode("Roles updated"); }
        
        break;
    }
	
	case 'nitroChecks':
    {
        if (!isset($_GET['id'])) {
            http_response_code(400);
            break;
        }

        $values = explode(",", $_GET['id']);

        $placeholders = str_repeat('?, ', count($values) - 1) . '?';
        $query = $database->prepare("SELECT steam_id FROM users WHERE steam_id IN($placeholders) AND nitro = 1");
        $query->execute($values);

        $result = $query->fetchAll(PDO::FETCH_ASSOC);

        $present = array_fill_keys(array_column($result, 'steam_id'), true);
        $allValues = array_fill_keys($values, false);
        $result = array_replace($allValues, $present);

        echo json_encode($result);
        break;
    }

    case 'groupChecks':
    {
        if (!isset($_GET['id'])) {
            http_response_code(400);
            break;
        }

        if(!isset($_GET['group'])) {
            echo json_encode("No Steam Group specified.");
            http_response_code(400);
            break;
        }

        include('steamauth/SteamConfig.php');

        $values = explode(",", $_GET['id']);
        $usersArray = array();

        foreach($values as $v) {
            $url = "http://api.steampowered.com/ISteamUser/GetUserGroupList/v1/?key={$steamauth['apikey']}&steamid={$v}";

            $ch = curl_init();
            curl_setopt($ch, CURLOPT_URL, $url);
            curl_setopt($ch,CURLOPT_USERAGENT,"Mozilla/5.0 (Windows; U; Windows NT 5.1; en-US; rv:1.8.1.13) Gecko/20080311 Firefox/2.0.0.13");
            curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
            $search = curl_exec($ch);
            curl_close($ch);
            $raw = json_decode($search, true);

            if ( ($raw['response']['success']) && (!is_null($raw['response']['groups']))) {
                
                foreach ($raw['response']['groups'] as $key => $value) {
                    if ($value['gid'] == $_GET['group']) {
                        array_push($usersArray, $v);
                        continue;
                    }
                }
            }
        }
        
        if (is_null($usersArray)) {
            echo json_encode (json_decode ("{}"));
        } else {
            $present = array_fill_keys($usersArray, true);
            $allValues = array_fill_keys($values, false);
            $result = array_replace($allValues, $present);

            echo json_encode($result);
        }
        break;
    }
	
	case 'updateNitro':
    {
        $updateQuery = $database->prepare("UPDATE users SET nitro = ? WHERE discord_id = ?");
        $updateQuery->execute(array($_GET['status'], $_GET['id']));
        echo json_encode("Nitro updated");
        break;
    }

    case 'listNitro':
    {
        $query = $database->prepare("SELECT discord_id FROM users WHERE nitro = ?");
        $query->execute(array(1));

        $result = $query->fetchAll(PDO::FETCH_ASSOC);
        $countResult = $query->rowCount();
        if ($countResult == 0)
        {
            echo json_encode(array("Result" => 0));
            http_response_code(404);
            break;
        }

        echo json_encode(array("Result" => $result));
        break;
    }

    case 'remove':
    {
        $removeQuery = $database->prepare("DELETE FROM users WHERE discord_id = ?");
        $removeQuery->execute(array($_GET['id']));
        echo json_encode("User Removed");
        break;
    }

    case 'updateSteam':
    {
        $query = $database->prepare("SELECT discord_id FROM users WHERE steam_id = ?");
        $query->execute(array($_GET['id']));

        $result = $query->fetchColumn();
        if ($result == true)
        {
            $updateQuery = $database->prepare("UPDATE users SET steam_name = ? WHERE steam_id = ?");
            $updateQuery->execute(array($_GET['name'], $_GET['id']));
        }
        break;
    }

    case 'getSteam':
    {
        $query = $database->prepare("SELECT steam_name FROM users WHERE discord_id = ?");
        $query->execute(array($_GET['id']));

        $result = $query->fetchColumn();
        echo json_encode($result);
        break;
    }

    case 'getDiscord':
    {
        $query = $database->prepare("SELECT discord_name FROM users WHERE discord_id = ?");
        $query->execute(array($_GET['id']));

        $result = $query->fetchColumn();
        echo json_encode($result);
        break;
    }

    case 'discordCheck':
    case 'findByDiscord':
    {
        if (!isset($_GET['id'])) {
            echo json_encode("No Discord ID specified.");
            http_response_code(400);
            break;
        }

        $query = $database->prepare("SELECT steam_id FROM users WHERE discord_id = ?");
        $query->execute(array($_GET['id']));

        $result = $query->fetchColumn();
        if ($result == false)
        {
            echo json_encode("No users found");
            http_response_code(404);
            break;
        }

        echo $result;
        break;
    }

    case 'count':
    {
        $getCount = $database->prepare("SELECT * FROM users");
        $getCount->execute();
        $countResult = $getCount->rowCount();
        //$rowCount = $countResult->rowCount();

        echo json_encode(array("Total" => $countResult));
        break;
    }

    case 'findBySteam':
    {
        if (!isset($_GET['id'])) {
            echo json_encode("No SteamID specified.");
            http_response_code(400);
            break;
        }

        $query = $database->prepare("SELECT discord_id FROM users WHERE steam_id = ?");
        $query->execute(array($_GET['id']));

        $result = $query->fetchColumn();
        if ($result == false)
        {
            echo json_encode("No users found.");
            http_response_code(404);
            break;
        }

        echo $result;
        break;
    }

    default:
    {
        echo json_encode("No proper action set.");
        break;
    }
}

die();