# SolidRusT Installation
## SolidRusT Game Server
 * [Installing SRT Game server](INSTALL.md)
 * [Useful Admin commands](docs/)

# SolidRusT Game Server Releases
This document describes the workflow for creating, managing and deploying releases to SolidRusT servers.

## Getting Started
Create a local copy of this code repository
* [How to use GitHub](https://docs.github.com/en/get-started/quickstart/set-up-git) Reference for setting up your local github command line tools
*  [Clone GitHub Repo](#clone-remote-github-pepository) Clone this remote github repository to your local machine
*  [Update GitHub Repo](#update-local-github-repository) Update your local clone with the remote repository

## Updating SolidRusT's next release
SolidRusT releases get auto-deployed every server wipe. Here are the steps involved for making changes to an upcomming SolidRusT release.
*  [Commit Changes](#commit-local-changes) commit your changes to your local copy of this github repository
*  [Update GitHub](#push-local-repository-changes) push and merge your local changes into the remote github repository
*  [Update SRT Distribution](#update-srt-distribution) update SolidRusT distribution server from the remote github repository

## Deploying the release before a scheduled wipe
These steps are to manually deploy the current SolidRusT without waiting for the scheduled wipe and auto-deployment jobs.
*  [Update SRT Distribution](#update-srt-distribution) update SolidRusT distribution server from the remote github repository
*  [Deploy SRT Release](#deploy-srt-release) deploy game server release from the SolidRusT distribution server
*  [Reload Plugins](#load-and-reload-plugins) load and reload any new or updated plugins

## Save running SolidRusT game server configurations into the Release
When making changes to plugins like `Kits`, using the in-game user interface, these changes will need to be downloaded into the release.
*  [Update Repo](#update-local-github-repository) update local repository with remote repository
*  [Pull running configs](#pull-running-configs) pull configs from a running server
*  [Commit Changes](#commit-local-changes) commit your changes to your local copy of this github repository
*  [Update GitHub](#push-local-repository-changes) push and merge your local changes into the remote github repository
*  [Update SRT Distribution](#update-srt-distribution) update SolidRusT distribution server from the remote github repository

# SRT Playbooks (how-to)
#### Clone remote GitHub Repository

To create a local copy of this repository, choose from the following methods:
* Clone Using website username and password
```bash
git clone https://github.com/suparious/solidrust.net.git
cd solidrust.net
```
* Clone Using SSH keys
See: [Adding SSH keys to your GitHub account](https://docs.github.com/en/authentication/connecting-to-github-with-ssh/adding-a-new-ssh-key-to-your-github-account) for more information on this method.
```bash
git clone git@github.com:suparious/solidrust.net.git
cd solidrust.net
```

#### Update local GitHub Repository
Syncronize your local files with the remote Github repository.
```bash
cd solidrust.net # Optional, if you are already in this folder
git checkout master && git pull
```
If you get some error messages, then you need to read and follow their suggestions OR just trash the `solidrust.net` folder and clone it from the remote repository to start over.

#### Commit local changes
Once you are happy with your edits, commit them with a comment to indicate what you have changed.
```bash
cd solidrust.net                        # Optional, if you are already in this folder
git config --global core.autocrlf false # need this if you use a Windows PC
git add --renormalize .                 # need this if you use a Windows PC
git add .       # Add any file that was changed into the release
git commit -m "Type a breif description of what you changed here"
```

#### Push local repository changes
Update GitHub with your local commit(s), by pushing and merging your changes with the remote GitHub repository.
```bash
cd solidrust.net # Optional, if you are already in this folder
git push
```

#### Update SRT Distribution
```bash
sync_repo
```

#### Login to the Game server
```bash
<server_name_ssh>
sudo su - <game_user>
```

#### Logout of the Game server
```bash
exit  # exit from game service user
exit  # exit from game server SSH session
```

#### Deploy SRT Release
This happens automatically every 5-15mins depending on the server's `crontab` configuration. To make this happen immediately, use the following steps:
*  [Login to the Game server](#login-to-the-game-server) Starting from your admin console, login to the game server using SSH.
* Update the game server from the SolidRusT distribution's current release.
```bash
update_repo game && update_mods
```
*  [Logout of the Game server](#logout-of-the-game-server)

#### Load and Reload Plugins
*  [Login to the Game server](#login-to-the-game-server) Starting from your admin console, login to the game server using SSH.
```bash
rcon "o.load *"
rcon "o.reload <plugin_name>"
```
*  [Logout of the Game server](#logout-of-the-game-server)

#### Pull running configs
```
pull_oxide_config <server_name> <plugin_name>
```

#### Pull plugin data
```
pull_oxide_data <server_name> <plugin_name>
```


[StackEdit](https://stackedit.io) - _StackEdit’s Markdown syntax highlighting is unique. The refined text formatting of the editor helps you visualize the final rendering of your files._