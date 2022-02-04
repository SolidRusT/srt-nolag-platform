const Discord = require('discord.js');
const client = new Discord.Client({ partials: ['MESSAGE', 'CHANNEL', 'REACTION'] });
const fetch = require('node-fetch');
let settings = { method: "Get" };


//YOU MODIFY THIS//
const prefixes = ['.', '-'];
const websiteURL = 'https://solidrust.net/';
const logoURL = 'https://solidrust.net/images/SolidRust_Logo.png';
const botToken = 'ODU0NDQ4NDQxMzY2MTUxMjA4.YMkFHw.4ebLZ6jv67jxmFulFjTb959WGPU';
const secret = "2f7ea85f8b8";
const serverName = "SolidRusT NoLAG Networks";
const staffAccessRoleID = "854449789214523403";
const verifiedRoleID = "847515430737674310";
const syncNames = false;
const sendMessages = true;
const roleForStatus = false;
const roleTextForStatus = "Text To Check Here";
//DO NOT MODIFY BELOW//

let verifyEmbed = new Discord.MessageEmbed()
    .setColor('#FF0000')
    .setTitle(`Verification Instructions`)
    .setURL(websiteURL)
    .setDescription('The instructions to link your accounts to recieve your SolidRusT role!')
    .setAuthor('Suparious', 'https://cdn.discordapp.com/avatars/140496445798219776/63c56af056e2a07b4d7c0d438f114ab8.png', 'https://ryz.sh/')
    .setFooter(websiteURL + ' Simple Link Bot by Suparious - v1.0.1')
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
            } catch (err) {
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
                    if (!member.premiumSince) {
                        console.log(`Member has nitro ${member.user.id}, but is not premium.`)
                        updateNitro(member.user.id, 0)
                    }
                }
            } catch (err) {
                console.log(err);
            }
        }).catch(err => console.log(err));
}

client.on("guildMemberAdd", member => {
    fetch(websiteURL + "api.php?action=findByDiscord&id=" + member.user.id + "&secret=" + secret + "", settings).then(res => res.text()).then((res) => {
        if (res.toString().includes("No users")) return;

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

    }, 30000);

    if (syncNames) {
        client.setInterval(() => {
            client.guilds.cache.first().members.cache.filter(member => member.fetch() && member.roles.cache.has(verifiedRoleID)).each(member => {
                fetch(websiteURL + "api.php?action=getSteam&id=" + member.user.id + "&secret=" + secret + "", settings)
                    .then(res => res.json())
                    .then((json) => {
                        member.setNickname(json);
                    });
            });
        }, 34000);
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
        }, 38000);

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
        }, 35000);
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

    if (message.author.bot) return;
    if (message.content.toLowerCase().includes("auth") || message.content.toLowerCase().includes("verify") || message.content.toLowerCase().includes("link")) {
        if (sendMessages) {
            message.channel.send(`Hey ${message.author}, it seems like you are trying to verify your accounts. Instructions below will delete in 30 seconds.`)
            message.channel.send(verifyEmbed).then(msg => msg.delete({ timeout: 30000 }));
        }
    }

    if (message.member.hasPermission("ADMINISTRATOR") || message.member.roles.cache.has(staffAccessRoleID)) {

        const prefixRegex = new RegExp(`^(${prefixes.join('|')})`);
        const prefix = message.content.match(prefixRegex);

        if (prefix) {
            message.content = message.content.replace(prefix[0], '');
            args = message.content.toString().split(" ");

            if (args[0] == '') {
                args.splice(0, 1);
            }

            switch (args[0]) {
                case "search":
                    if (!args[1]) {
                        message.channel.send("**You have not provided any user information to search.**");
                        return;
                    }

                    searchID = "";
                    searchType = "";
                    if (message.mentions.users.first()) {
                        searchID = message.mentions.users.first().id;
                        searchType = "Discord";
                    } else if (args[1].length == 17 && args[1].startsWith("7656119")) {
                        searchID = args[1];
                        searchType = "Steam";
                    } else if (args[1].length > 15 && !args[1].startsWith("7656119")) {
                        searchID = args[1];
                        searchType = "Discord";
                    } else {
                        message.reply("not a valid search ID.");
                        return;
                    }

                    if (searchType === "Discord") {
                        fetch(websiteURL + "api.php?action=findByDiscord&id=" + searchID + "&secret=" + secret + "", settings)
                            .then(res => res.text())
                            .then((res) => {
                                if (res.toString().includes("No users")) {
                                    message.channel.send("No users with a linked account.");
                                    return;
                                }

                                let theEmbed = new Discord.MessageEmbed()
                                    .setColor('#4286f4')
                                    .setTitle(`User Lookup Results`)
                                    .setThumbnail(logoURL)
                                    .addField("Discord ID", searchID, true)
                                    .addField("Steam ID", res, true);

                                message.reply(theEmbed);
                            });
                    } else if (searchType === "Steam") {
                        fetch(websiteURL + "api.php?action=findBySteam&id=" + searchID + "&secret=" + secret + "", settings)
                            .then(res => res.text())
                            .then((res) => {
                                if (res.toString().includes("No users")) {
                                    message.channel.send("No users with a linked account.");
                                    return;
                                }

                                let theEmbed = new Discord.MessageEmbed()
                                    .setColor('#4286f4')
                                    .setTitle(`User Lookup Results`)
                                    .setThumbnail(logoURL)
                                    .addField("Discord ID", `${res}`, true)
                                    .addField("Steam ID", searchID, true);

                                message.reply(theEmbed);
                            });
                    }
                    break;

                case "count":
                    fetch(websiteURL + "api.php?action=count&secret=" + secret + "", settings)
                        .then(res => res.json())
                        .then((json) => {
                            message.channel.send("There are " + json.Total + " total users verified on " + serverName + ".");
                        }).catch(err => console.log(err));
                    break;

                case "auth":
                case "link":
                case "verify":
                    message.channel.send("Please use the website to auth, link and verify your Steam and Discord accounts\n```https://solidrust.net```");
                    break;
            }
        }
    }
});