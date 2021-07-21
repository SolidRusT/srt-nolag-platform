<?php
	$user = "srt_sl_lcy";
	$pass = "lcy_402";
	$db = "srt_web_auth";
	$db_host = "data.solidrust.net";
	$details = "mysql:dbname=$db;host=$db_host";
	$database = new PDO($details, $user, $pass);

	$redirect = 'https://solidrust.net/discord.php';

	$VerifiedRoleID = 847515430737674310;
	$webhook = "https://discordapp.com/api/webhooks/866039927782506556/vDRnCU-d5tMfHwuXcSGod7ADmL2D736Ym3OB5fzVXRPME444Oh_hwmzdMU0Bo5TaEL3n";
	$logoURL = "https://solidrust.net/images/SoldRust_Logo.png";
	$SiteTitle = "SolidRust NoLag Networks";

	$guild_id = 846525505083670580;

    $bot_token = 'ODU0NDQ4NDQxMzY2MTUxMjA4.YMkFHw.4ebLZ6jv67jxmFulFjTb959WGPU';
    $client_id = 854448441366151208;
    $client_secret = '6ppes6_VjEyE43qHj7Jh-oke88xwDJAH';
	
    // DO NOT MODIFY //
    $tokenURL = 'https://discordapp.com/api/oauth2/token';
    $scope = 'identify guilds guilds.join';
    // DO NOT MODIFY //

	// integration links
	$secret_id = $client_secret;
	$scopes = $scope;
	$redirect_url = "https://solidrust.net/discord-test.php";
?>
