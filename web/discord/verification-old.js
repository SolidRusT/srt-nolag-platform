const Discord = require('discord.js');
const client = new Discord.Client();
const fetch = require('node-fetch');
let settings = { method: "Get" };
const SteamAPI = require('steamapi');


//YOU MODIFY THIS//
const steam = new SteamAPI('YOUR STEAM API KEY');
const websiteURL = 'LINK WEBSITE URL (https://rustreborn.link/';
const logoURL = 'YOUR LOGO URL';
const botToken = 'YOUR BOT TOKEN';
const secret = "SECRET KEY (FROM api.php)";
const botName = "YOUR BOT NAME (Exactly as it appears in Discord with spaces, etc..)";
const serverName = "RUST SERVER NAME";
const staffAccessRoleID = "StaffRoleID";
const verifiedRoleID = "12345";
const syncNames = false;
const sendMessages = false;
const roleForStatus = false;
const roleTextForStatus = "Text To Check Here";
//DO NOT MODIFY BELOW//

let verifyEmbed = new Discord.MessageEmbed()
  .setColor('#FF0000')
  .setTitle(`Verification Instructions`)
  .setURL(websiteURL)
  .setDescription('The instructions to link your accounts to recieve your Discord Kit!')
  .setAuthor('Ryz0r', 'https://cdn.discordapp.com/avatars/140496445798219776/35c4454d8edb843d76a75fda5e06ff87.png', 'https://ryz0r.dev/')
  .setFooter(websiteURL + 'Link Bot by Ryz0r - v1.0.0')
  .setThumbnail(logoURL)
  .addField('Step 1', 'Navigate to ' + websiteURL + ' or click [here](' + websiteURL + ').')
  .addField('Step 2', 'Click Login with Discord, and then login with your current Discord account.')
  .addField('Step 3', 'Click Login with Steam, and then login with your current Steam account.')
  .addField('Step 4', 'Ensure both Steam and Discord say linked, and if so - you are done!');
  
client.login(botToken);

function updateNitro(discordID, status) {
    fetch(websiteURL + "api.php?action=updateNitro&status=" + status + "&secret=" + secret + "&id=" + discordID, settings)
        .then(res => res.text())
        .then(text => {
            try {
            const data = JSON.parse(text);
                console.log(data);
            } catch(err) {
                console.log(err);
            }
        }).catch(err => console.log(err));
}

function checkNitro() {
    fetch(websiteURL + "api.php?action=listNitro&secret=" + secret + "", settings)
        .then(res => res.text())
        .then(text => {
            try {
                const json = JSON.parse(text);
                console.log("Running nitro checker.");
                for (i = 0; i < json.Result.length; i++) {
                    var member = client.guilds.cache.first().members.cache.get(json.Result[i].discord_id);
                    if(!member.premiumSince) {
                        console.log(`Member has nitro ${member.user.id}, but is not premium.`)
                        updateNitro(member.user.id, 0)
                    }
                }
            } catch(err) {
               console.log(err);
            }
    }).catch(err => console.log(err));
}

client.on("guildMemberAdd", member => {
    fetch(websiteURL + "api.php?action=findByDiscord&id=" + member.user.id +"&secret=" + secret + "", settings).then(res => res.text()).then((res) => {
      if(res.toString().includes("No users")) return;

      vRole = member.guild.roles.cache.find(r => r.id === verifiedRoleID);
      member.roles.add(vRole);
    });
});

client.on('ready', () => {
  console.log(`Logged in as ${client.user.tag}! Bot is ready!`);
  
  checkNitro();

  client.guilds.cache.first().members.fetch();

    fetch(websiteURL + "api.php?action=count&secret=" + secret + "", settings)
        .then(res => res.json())
        .then((json) => {
            client.user.setActivity(json.Total.toString() + " Verified!", { type: 'WATCHING' }).catch(console.error);
    }).catch(err => console.log(err));

  client.setInterval(() => {
    fetch(websiteURL + "api.php?action=count&secret=" + secret + "", settings)
        .then(res => res.json())
        .then((json) => {
            client.user.setActivity(json.Total.toString() + " Verified!", { type: 'WATCHING' }).catch(console.error);
         }).catch(err => console.log(err));
    }, 30000);
	
	client.setInterval(() => {

        client.guilds.cache.first().members.cache.filter(member => member.fetch() && member.premiumSince).each(member => updateNitro(member.user.id, 1));

    }, 15000);

    if (syncNames) {
        client.setInterval(() => {
            client.guilds.cache.first().members.cache.filter(member => member.fetch() && member.roles.cache.has(verifiedRoleID)).each(member => {
                fetch(websiteURL + "api.php?action=getSteam&id=" + member.user.id + "&secret=" + secret + "", settings)
                    .then(res => res.json())
                    .then((json) => {
                        member.setNickname(json);
                    });
            });
        }, 15000);
    }

    if (roleForStatus) {
        role = client.guilds.cache.first().roles.cache.find(r => r.id === roleIDForStatus);

        client.setInterval(() => {
            client.guilds.cache.first().members.cache.filter(m => m.presence.status == "online" && !m.roles.cache.has(roleIDForStatus) && m.presence.activities[0]).each(mem => {
                if (mem.presence.activities[0].type === "PLAYING" && mem.presence.activities[0].name.toLowerCase().includes(roleTextForStatus.toLowerCase())) {
                    
                    if (role) {
                        mem.roles.add(role);
                    }
                } else if (mem.presence.activities[0].type === "CUSTOM_STATUS" && mem.presence.activities[0].state && mem.presence.activities[0].state.toLowerCase().includes(roleTextForStatus.toLowerCase())) {
                    
                    if (role) {
                        mem.roles.add(role);
                    }
                }
            })
        }, 15000);

        client.setInterval(() => {
            client.guilds.cache.first().members.cache.filter(m => m.presence.status == "online" && m.roles.cache.has(roleIDForStatus)).each(mem => {
                if (!mem.presence.activities[0]) {
                    
                    if (role) {
                        mem.roles.remove(role);
                    }
                } else if (mem.presence.activities[0].type === "PLAYING" && !mem.presence.activities[0].name.toLowerCase().includes(roleTextForStatus.toLowerCase())) {
                    
                    if (role) {
                        mem.roles.remove(role);
                    }
                } else if (mem.presence.activities[0].type === "CUSTOM_STATUS" && mem.presence.activities[0].state && !mem.presence.activities[0].state.toLowerCase().includes(roleTextForStatus.toLowerCase())) {
                    
                    if (role) {
                        mem.roles.remove(role);
                    }
                }
            })
        }, 15000);
    }

    client.setInterval(() => {

        checkNitro();

    }, 600000);
});

client.on("guildMemberRemove", (member) => {

  fetch(websiteURL + "api.php?action=remove&secret=" + secret + "&id=" + member.id, settings)
        .then(res => res.json())
        .then((json) => {
            console.log(json);
    }).catch(err => console.log(err));
  
});

client.on("message", async function(message) {

    if(message.author.bot) return;
    
    if(message.content.toLowerCase().includes("auth") || message.content.toLowerCase().includes("verify") || message.content.toLowerCase().includes("link")) {
        if (sendMessages) {
            message.channel.send(`Hey ${message.author}, it seems like you are trying to verify your accounts. Instructions below will delete in 30 seconds.`)
            message.channel.send(verifyEmbed).then(msg => msg.delete({timeout: 30000}));
        }
    }

    if(message.member.hasPermission("ADMINISTRATOR") || message.member.roles.cache.has(staffAccessRoleID)) {
    	if(message.mentions.users.first()) {
    		var mentionUser = message.mentions.users.first();

    		if(mentionUser.username == botName) {
                var args = message.content.split(" ");

                if(!args[1]) {
                    message.channel.send("You have not provided any arguments.\nYou can do things like `@Your Bot Name search (@tag/steam id)`!");
                    return;
                }

                switch(args[1]) {
                    case "test":
                        message.channel.send("Ryz0r says hello!");
                        break;
                        
                    case "count":
                        fetch(websiteURL + "api.php?action=count&secret=" + secret + "", settings)
                            .then(res => res.json())
                            .then((json) => {
                                message.channel.send("There are " + json.Total + " total users verified on " + serverName + ".");
                            }).catch(err => console.log(err));
                        break;
                    
                    case "restart":
                        message.delete();
                        message.channel.send(`Please, ${message.author}... join me in my quest to reset. Initiating...`)
                            .then(msg => msg.delete({timeout: 3500}))
                            .then(q => client.destroy());
                        break;
                        
                    case "link":
                    case "verify":
                    case "auth":
                          
                        message.channel.send(verifyEmbed);
                        break;

                    case "search":
                        if(!args[2]) {
                            message.channel.send("**You have not provided any user information to search.**");
                            return;
                        }

                        if(args[2].startsWith("7656119")) {
                            fetch(websiteURL + "api.php?action=findBySteam&id=" + args[2] + "&secret=" + secret + "", settings)
                                .then(res => res.text())
                                .then((res) => {
                                    var searchEmbedMember = message.guild.members.cache.get(res);

                                    if(res.toString().includes("No users")) {
                                            message.channel.send("No users with a linked account.");
                                            return;
                                    }

                                    if(!searchEmbedMember) {
                                            message.channel.send("No guild member found.");
                                            return;
                                    }

                                        let embedDiscord = new Discord.MessageEmbed()
                                          .setColor('#4286f4')
                                          .setTitle(`Discord Information for ${searchEmbedMember.user.username}`)
                                          .addField("Full Username:", `${searchEmbedMember.user.tag}`)
                                          .addField("User ID:", `${searchEmbedMember.user.id}`)
                                          .addField("Server Join Date:", `${searchEmbedMember.joinedAt}`)
                                          .addField("Online Status:", `${searchEmbedMember.user.presence.status}`)
                                          .addField("Roles:", `${searchEmbedMember.roles.cache.map(r => `${r}`).join(' | ')}`)
                                          .addField("Current Guild:", `${searchEmbedMember.guild.name}`)
                                          .addField("Current Guild Size:", `${searchEmbedMember.guild.memberCount}`)
                                          .setThumbnail(searchEmbedMember.user.avatarURL({ dynamic: true, format: 'png', size: 1024 }));

                                          steam.getUserSummary(args[2]).then(summary => {

                                            let date = new Date(summary.created * 1000);

                                            let steamDiscord = new Discord.MessageEmbed()
                                              .setColor('#4286f4')
                                              .addField("Full Username:", `${summary.nickname}`)
                                              .setTitle(`Steam Information for ${summary.nickname}`)
                                              .addField("Created Date", `${date}`)
                                              .addField("Steam ID:", `${summary.steamID}`)
                                              .addField("Profile Visibility:", `${summary.visibilityState}`)
                                              .setThumbnail(summary.avatar.large);

                                              message.channel.send(steamDiscord);
                                              message.channel.send(summary.steamID);
                                              message.channel.send(searchEmbedMember.user.id);
                                            
                                        });
                                        
                                        message.channel.send(embedDiscord);
                            }).catch(err => console.log(err));
                        } else if(args[2].startsWith("<@")) {
                            fetch(websiteURL + "api.php?action=findByDiscord&id=" + message.mentions.users.last().id +"&secret=" + secret + "", settings)
                                .then(res => res.text())
                                .then((res) => {
                                        var searchEmbedMember = message.guild.members.cache.get(message.mentions.users.last().id);

                                        if(res.toString().includes("No users")) {
                                            message.channel.send("No users with a linked account.");
                                            return;
                                    }

                                    if(!searchEmbedMember) {
                                            message.channel.send("No guild member found.");
                                            return;
                                    }

                                        let embedDiscord = new Discord.MessageEmbed()
                                          .setColor('#4286f4')
                                          .setTitle(`Discord Information for ${searchEmbedMember.user.username}`)
                                          .addField("Full Username:", `${searchEmbedMember.user.tag}`)
                                          .addField("User ID:", `${searchEmbedMember.user.id}`)
                                          .addField("Server Join Date:", `${searchEmbedMember.joinedAt}`)
                                          .addField("Online Status:", `${searchEmbedMember.user.presence.status}`)
                                          .addField("Roles:", `${searchEmbedMember.roles.cache.map(r => `${r}`).join(' | ')}`)
                                          .addField("Current Guild:", `${searchEmbedMember.guild.name}`)
                                          .addField("Current Guild Size:", `${searchEmbedMember.guild.memberCount}`)
                                          .setThumbnail(searchEmbedMember.user.avatarURL({ dynamic: true, format: 'png', size: 1024 }));

                                          steam.getUserSummary(res).then(summary => {

                                            let date = new Date(summary.created * 1000);

                                            let steamDiscord = new Discord.MessageEmbed()
                                              .setColor('#4286f4')
                                              .addField("Full Username:", `${summary.nickname}`)
                                              .setTitle(`Steam Information for ${summary.nickname}`)
                                              .addField("Created Date", `${date}`)
                                              .addField("Steam ID:", `${summary.steamID}`)
                                              .addField("Profile Visibility:", `${summary.visibilityState}`)
                                              .setThumbnail(summary.avatar.large);

                                              message.channel.send(steamDiscord);
                                              message.channel.send(summary.steamID);
                                              message.channel.send(searchEmbedMember.user.id);
                                            
                                        });
                                        
                                        message.channel.send(embedDiscord);
                                    });
                        } else {
                            fetch(websiteURL + "api.php?action=findByDiscord&id=" + args[2] +"&secret=" + secret + "", settings)
                                .then(res => res.text())
                                .then((res) => {
                                        var searchEmbedMember = message.guild.members.cache.get(args[2]);

                                        if(res.toString().includes("No users")) {
                                            message.channel.send("No users with a linked account.");
                                            return;
                                    }

                                    if(!searchEmbedMember) {
                                            message.channel.send("No guild member found.");
                                            return;
                                    }

                                        let embedDiscord = new Discord.MessageEmbed()
                                          .setColor('#4286f4')
                                          .setTitle(`Discord Information for ${searchEmbedMember.user.username}`)
                                          .addField("Full Username:", `${searchEmbedMember.user.tag}`)
                                          .addField("User ID:", `${searchEmbedMember.user.id}`)
                                          .addField("Server Join Date:", `${searchEmbedMember.joinedAt}`)
                                          .addField("Online Status:", `${searchEmbedMember.user.presence.status}`)
                                          .addField("Roles:", `${searchEmbedMember.roles.cache.map(r => `${r}`).join(' | ')}`)
                                          .addField("Current Guild:", `${searchEmbedMember.guild.name}`)
                                          .addField("Current Guild Size:", `${searchEmbedMember.guild.memberCount}`)
                                          .setThumbnail(searchEmbedMember.user.avatarURL({ dynamic: true, format: 'png', size: 1024 }));

                                          steam.getUserSummary(res).then(summary => {

                                            let date = new Date(summary.created * 1000);

                                            let steamDiscord = new Discord.MessageEmbed()
                                              .setColor('#4286f4')
                                              .addField("Full Username:", `${summary.nickname}`)
                                              .setTitle(`Steam Information for ${summary.nickname}`)
                                              .addField("Created Date", `${date}`)
                                              .addField("Steam ID:", `${summary.steamID}`)
                                              .addField("Profile Visibility:", `${summary.visibilityState}`)
                                              .setThumbnail(summary.avatar.large);

                                              message.channel.send(steamDiscord);
                                              message.channel.send(summary.steamID);
                                              message.channel.send(searchEmbedMember.user.id);
                                            
                                        });
                                        
                                        message.channel.send(embedDiscord);
                                    });
                        }
                        break;

                    case "help":
                        message.channel.send("**(All commands run by tagging the bot followed by spaces)**\n\nCurrent Commands\n```search (discord id/tag/steam id) - searches for user information in linking database\nhelp - simple help document\ntest - test command for ryz0r\ncount - lists number of verified\nlink/verify/auth - will display a helpful popup on how to verify\nrestart - restarts the bot. use at your own risk.```");
                        break;
                }
    		}
    	}
    }
});