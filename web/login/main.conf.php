<?php
	$user = "srt_sl_lcy";
	$pass = "lcy_402";
	$details = "mysql:dbname=srt_web_auth;host=data.solidrust.net";
	$database = new PDO($details, $user, $pass);

	$redirect = 'https://solidrust.net/discord';

	$VerifiedRoleID = Survivor;
	$webhook = "https://discordapp.com/api/webhooks/866039927782506556/vDRnCU-d5tMfHwuXcSGod7ADmL2D736Ym3OB5fzVXRPME444Oh_hwmzdMU0Bo5TaEL3n";
	$logoURL = "https://solidrust.net/images/SoldRust_Logo.png";
	$SiteTitle = "SolidRust NoLag Networks";

	$guild_id = 846525505083670580;

    $bot_token = 'ODU0NDQ4NDQxMzY2MTUxMjA4.YMkFHw.4ebLZ6jv67jxmFulFjTb959WGPU';
    $client_id = 854448441366151208;
    $client_secret = '2f7ea85f8b858f47645b7be893c44689840f19317ea4d8ef91db955f14383f24';


    // DO NOT MODIFY //
    $tokenURL = 'https://discordapp.com/api/oauth2/token';
    $scope = 'identify guilds guilds.join';
    // DO NOT MODIFY //
?>