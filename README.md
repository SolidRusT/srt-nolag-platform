# SolidRusT Server Configuration

 - no lag gameplay
 - #ffe300
 - git config --global core.autocrlf false
 - git add --renormalize .

## Install Rust Server

`install_1_system.sh`

`install_2_server.sh`

create the initial `server/solidrust/cfg/server/cfg` file, ot use one from another server's backup.

`solidrust.sh`

Once Rust Community server is working great, then we can get to the mods.

Stop the server.

`install_3_mods.sh`

`solidrust.sh`

## Backups

`backup.sh`

## Updates - First Tuseday of each month

from the game F1 console

```
server.writecfg
server.backup
server.save
server.stop( string "Getting FacePunched monthy" )
```

Then apply the latest patch from Steam.

```bash
steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 +quit
steamcmd +login anonymous +force_install_dir ~/ +app_update 258550 validate +quit
```

Now, re-run the mods installer.

`install_3_mods.sh`

Start the service.

`solidrust.sh`


### Emergency restarting

```
#restart <seconds> <message>
restart 120 “Don’t wander too far off!”
```

### Other

```
bear.population
stag.population
wolf.population
boar.population
```




#### Notes

Universal:
- SRBOT (SolidRust)
- "Discord API Key": "ODA0OTkyMjk1NzM2NzA1MDI0.YBUZfw.a3N3MhU9x_dR63AlqcMzlWaBjgE",
- "Enable Discord Extension Debugging": false,
- "Bot Guild ID (Can be left blank if bot in only 1 guild)": "800206928562880522",
- "Discord Join Code": "xgGEdzv2bw",

SRVerify:
- CaptainBanHammer: https://discord.com/api/webhooks/805028753985503244/s9K-0aAiyyCficw5ToVpoRRwIz3I6dGCM9mAqZ3Yv9aPQktXeL3_cGP6iSZQNOXKeD-E
- SnitchBot: https://discord.com/api/webhooks/805028639921930260/O9g8bbaDpH0jnl3UXvZeLjTDf3KJo1a3GDcNpmjqCr5jwPUJvptFCUQclathu9jcAzxf
- PlzHelps: https://discord.com/api/webhooks/805027533107626014/ZT3nzjTjuT52r9Kwtl3QvfDPl0_Y7I4U_FBMQRSn4YLLdR-oQ-k2uljAXkxaWtZYyeUS
- GlobalChat: https://discord.com/api/webhooks/804099330939158559/IJhKfkLlm_wOxywNF4dxpPV9iIL7spYCZG3lViycnsGkTgsFDTe8pfc1K4CsVOETACvT
- MuteChat: https://discord.com/api/webhooks/805028314170654761/1SUHRGqHJpORAi3sRpQLGKLk0Df8kJK1fe0YA0hsIfRxVLZA28AJiRPUxfk5ShjQdEfY
- DeathNotes: https://discord.com/api/webhooks/804914766237270028/1SmwGIc0_8QNep3B1kL-ZSlz2qSNJ5iCRL9cpGK6NKrwPLVnuhyOQtQNqIVq05MlfplX

West:
- ServerStats
- https://discord.com/api/webhooks/804915479436722207/tHhjh4mxP6-ObVzAy_2LZJEcsnUZX7xMxJYd0Y1BgVqZNosNFlXxv4cy-bworQ2V5GM1

Connect:
- https://discord.com/api/webhooks/804915117413105664/yrpsyVxGP91rPwMG3JauK22Hj8qbARlrYPTJJOe8f5ugSmyEdBuOVxyLZJxNa38PxLSZ
Disconnect:
- https://discord.com/api/webhooks/804915479436722207/tHhjh4mxP6-ObVzAy_2LZJEcsnUZX7xMxJYd0Y1BgVqZNosNFlXxv4cy-bworQ2V5GM1
ServerStatus:
- https://discord.com/api/webhooks/804915479436722207/tHhjh4mxP6-ObVzAy_2LZJEcsnUZX7xMxJYd0Y1BgVqZNosNFlXxv4cy-bworQ2V5GM1

East:
- ServerStats
- https://discord.com/api/webhooks/805604705739997225/HtdsphVKtiu3SS6rrlzhBgK9B4dAf1KSFpaLXcNmbQX_jg-ZgDjggtYrqdd7U_PEc6kC

