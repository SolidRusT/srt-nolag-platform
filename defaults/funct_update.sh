function update_repo() {
  PARAM1=$1
  echo "Downloading repo from s3" | tee -a ${LOGS}
  mkdir -p ${HOME}/solidrust.net/defaults
  case ${PARAM1} in
  game | gameserver)
    echo "Sync repo for game server"
    mkdir -p ${HOME}/solidrust.net/servers ${HOME}/solidrust.net/defaults
    aws s3 sync --delete \
      --exclude "web/*" \
      --exclude "apps/*" \
      --exclude "defaults/bots/*" \
      --exclude "defaults/web/*" \
      --exclude "defaults/radio/*" \
      --exclude "defaults/database/*" \
      --exclude "servers/bots/*" \
      --exclude "servers/web/*" \
      --exclude "servers/data/*" \
      --exclude "servers/radio/*" \
      ${S3_REPO} ${HOME}/solidrust.net | tee -a ${LOGS}
    echo "Setting execution bits" | tee -a ${LOGS}
    chmod +x ${HOME}/solidrust.net/defaults/*.sh
    cp ${HOME}/solidrust.net/build.txt ${GAME_ROOT}/
    ;;
  web | webserver)
    echo "Sync repo for website server"
    mkdir -p ${HOME}/solidrust.net/web ${HOME}/solidrust.net/defaults ${HOME}/solidrust.net/servers
    export GAME_ROOT="${HOME}/solidrust.net/web"
    aws s3 sync --size-only --delete \
      --exclude "web/maps/*" \
      --exclude "apps/*" \
      --exclude "defaults/bots/*" \
      --exclude "defaults/oxide/*" \
      --exclude "defaults/database/*" \
      --exclude "servers/*" \
      --include "servers/web/*" \
      ${S3_REPO} ${HOME}/solidrust.net | tee -a ${LOGS}
    echo "Setting execution bits" | tee -a ${LOGS}
    chmod +x ${HOME}/solidrust.net/defaults/*.sh
    chmod +x ${HOME}/solidrust.net/defaults/web/*.sh
    ;;
  bots)
    echo "Sync repo for bots server"
    mkdir -p ${HOME}/solidrust.net
    aws s3 sync --size-only --delete \
      --exclude "web/maps/*" \
      --exclude "defaults/oxide/*" \
      --exclude "defaults/cfg/*" \
      --exclude "defaults/console/*" \
      --exclude "defaults/radio/*" \
      --exclude "defaults/procedural-maps/*" \
      --exclude "defaults/database/*" \
      --exclude "servers/*" \
      --include "servers/bots/*" \
      ${S3_REPO} ${HOME}/solidrust.net | tee -a ${LOGS}
    echo "Setting execution bits" | tee -a ${LOGS}
    chmod +x ${HOME}/solidrust.net/defaults/*.sh
    chmod +x ${HOME}/solidrust.net/defaults/bots/*.sh
    ;;
  data | database)
    echo "Sync repo for database server"
    mkdir -p ${HOME}/solidrust.net
    aws s3 sync --delete ${S3_REPO} ${HOME}/solidrust.net \
    --exclude "apps/*" \
    --exclude 'web/*' \
    --exclude 'defaults/oxide/*' \
    --exclude 'defaults/web/*' \
    --exclude 'servers/*' \
    --include 'servers/data/*' | grep -v ".git" | tee -a ${LOGS}
    echo "Setting execution bits" | tee -a ${LOGS}
    chmod +x ${HOME}/solidrust.net/defaults/database/*.sh
    ;;
  *)
    echo "Performing full repository sync"
    mkdir -p ${HOME}/solidrust.net
    aws s3 sync --delete ${S3_REPO} ${HOME}/solidrust.net | grep -v ".git" | tee -a ${LOGS}
    echo "Setting execution bits" | tee -a ${LOGS}
    chmod +x ${HOME}/solidrust.net/defaults/database/*.sh
    chmod +x ${HOME}/solidrust.net/defaults/web/*.sh
    chmod +x ${HOME}/solidrust.net/defaults/*.sh
    ;;
  esac
  echo "Current build: $(cat ${HOME}/solidrust.net/build.txt | head -n 2)"
}

function update_mods() {
  # Sync global Oxide data defaults 
  OXIDE=(
    oxide/data/BetterLoot/LootTables.json
    oxide/data/Kits/Kits.json
    oxide/data/FancyDrop.json
    oxide/data/CompoundOptions.json
    oxide/data/death.png
    oxide/data/BetterChat.json
    oxide/data/HeliControlWeapons.json
    oxide/data/HeliControlData.json
    oxide/data/hit.png
    oxide/data/GuardedCrate.json
    oxide/data/CustomChatCommands.json
  )
  # oxide/data/BetterChat.json
  echo "=> Updating plugin data" | tee -a ${LOGS}
  mkdir -p "${GAME_ROOT}/oxide/data/BetterLoot" "${GAME_ROOT}/oxide/data/Kits"
  for data in ${OXIDE[@]}; do
    echo " - $data" | tee -a ${LOGS}
    rsync "${SERVER_GLOBAL}/$data" "${GAME_ROOT}/$data" | tee -a ${LOGS}
    if [[ -f "${SERVER_CUSTOM}/$data" ]]; then
      rsync "${SERVER_CUSTOM}/$data" "${GAME_ROOT}/$data" | tee -a ${LOGS}
    fi
  done
  echo " - oxide/data/copypaste" | tee -a ${LOGS}
  mkdir -p "${GAME_ROOT}/oxide/data/copypaste"
  rsync -r "${SERVER_GLOBAL}/oxide/data/copypaste/" "${GAME_ROOT}/oxide/data/copypaste" | tee -a ${LOGS}
  if [[ -d "${SERVER_CUSTOM}/oxide/data/copypaste" ]]; then
    rsync -r "${SERVER_CUSTOM}/oxide/data/copypaste/" "${GAME_ROOT}/oxide/data/copypaste" | tee -a ${LOGS}
  fi
  echo " - oxide/data/RaidableBases" | tee -a ${LOGS}
  mkdir -p "${GAME_ROOT}/oxide/data/RaidableBases"
  rsync -ra --delete "${SERVER_GLOBAL}/oxide/data/RaidableBases/" "${GAME_ROOT}/oxide/data/RaidableBases" | tee -a ${LOGS}
  if [[ -d "${SERVER_CUSTOM}/oxide/data/RaidableBases" ]]; then
    rsync -r "${SERVER_CUSTOM}/oxide/data/RaidableBases/" "${GAME_ROOT}/oxide/data/RaidableBases" | tee -a ${LOGS}
  fi
  # Sync global Oxide config defaults
  echo "=> Updating plugin configurations" | tee -a ${LOGS}
  mkdir -p "${GAME_ROOT}/oxide/config" | tee -a ${LOGS}
  echo " - sync ${SERVER_GLOBAL}/oxide/config/ to ${GAME_ROOT}/oxide/config" | tee -a ${LOGS}
  rsync -r "${SERVER_GLOBAL}/oxide/config/" "${GAME_ROOT}/oxide/config" | tee -a ${LOGS}
  # Sync server-specific Oxide config overrides
  echo " - sync ${SERVER_CUSTOM}/oxide/config/ to ${GAME_ROOT}/oxide/config" | tee -a ${LOGS}
  rsync -r "${SERVER_CUSTOM}/oxide/config/" "${GAME_ROOT}/oxide/config" | tee -a ${LOGS}
  # Merge global default, SRT Custom and other server-specific plugins into a single build
  rm -rf ${BUILD_ROOT}
  mkdir -p "${BUILD_ROOT}/oxide/plugins"
  echo "=> Updating Oxide plugins" | tee -a ${LOGS}
  rsync -ra --delete "${SERVER_GLOBAL}/oxide/plugins/" "${BUILD_ROOT}/oxide/plugins" | tee -a ${LOGS}
  rsync -ra "${SERVER_GLOBAL}/oxide/custom/" "${BUILD_ROOT}/oxide/plugins" | tee -a ${LOGS}
  rsync -ra "${SERVER_CUSTOM}/oxide/plugins/" "${BUILD_ROOT}/oxide/plugins" | tee -a ${LOGS}
  # Push customized plugins into the game root
  rsync -ra --delete "${BUILD_ROOT}/oxide/plugins/" "${GAME_ROOT}/oxide/plugins" | tee -a ${LOGS}
  rm -rf ${BUILD_ROOT}
  # Update plugin language and wording overrides
  LANG=(
    oxide/lang/en/Kits.json
    oxide/lang/en/Welcomer.json
    oxide/lang/en/Dance.json
    oxide/lang/ru/Dance.json
  )
  echo "=> Updating plugin language data" | tee -a ${LOGS}
  mkdir -p "${GAME_ROOT}/oxide/lang/en" "${GAME_ROOT}/oxide/lang/ru"
  for data in ${LANG[@]}; do
    echo " - $data" | tee -a ${LOGS}
    rsync "${SERVER_GLOBAL}/$data" "${GAME_ROOT}/$data" | tee -a ${LOGS}
    if [[ -f "${SERVER_CUSTOM}/$data" ]]; then
      rsync "${SERVER_CUSTOM}/$data" "${GAME_ROOT}/$data" | tee -a ${LOGS}
    fi
  done
  echo "=> loading dormant plugins" | tee -a ${LOGS}
  ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.load *" | tee -a ${LOGS}
  tail -n 24 "${GAME_ROOT}/RustDedicated.log"
}

function update_configs() {
  echo "=> Update Rust Server configs" | tee -a ${LOGS}
  mkdir -p ${GAME_ROOT}/server/solidrust/cfg | tee -a ${LOGS}
  rm ${GAME_ROOT}/server/solidrust/cfg/serverauto.cfg ## TODO, conditional, only if file exists
  rsync -a ${SERVER_CUSTOM}/server/solidrust/cfg/server.cfg ${GAME_ROOT}/server/solidrust/cfg/server.cfg | tee -a ${LOGS}
  rsync -a ${SERVER_GLOBAL}/cfg/users.cfg ${GAME_ROOT}/server/solidrust/cfg/users.cfg | tee -a ${LOGS}
  rsync -a ${SERVER_CUSTOM}/server/solidrust/cfg/users.cfg ${GAME_ROOT}/server/solidrust/cfg/users.cfg | tee -a ${LOGS}
  rsync -a ${SERVER_GLOBAL}/cfg/bans.cfg ${GAME_ROOT}/server/solidrust/cfg/bans.cfg | tee -a ${LOGS}
}

function update_maps() {
  echo "=> Update Rust custom maps configs" | tee -a ${LOGS}
  aws s3 sync --size-only --delete ${S3_WEB}/maps ${HOME}/solidrust.net/web/maps | tee -a ${LOGS}
}

function update_radio() {
  echo "=> Update Rust custom radio station" | tee -a ${LOGS}
  aws s3 sync --size-only --delete ${S3_RADIO} /var/www/radio | tee -a ${LOGS}
}

function update_server() {
  echo "=> Updating server: ${GAME_ROOT}" | tee -a ${LOGS}
  echo " - Buffing-up Debian Distribution..." | tee -a ${LOGS}
  sudo apt update | tee -a ${LOGS}
  sudo apt -y dist-upgrade | tee -a ${LOGS}
  # TODO: output a message to reboot if kernel or initrd was updated
  echo "=> Validating installed Steam components..." | tee -a ${LOGS}
  /usr/games/steamcmd +force_install_dir ${GAME_ROOT}/ +login anonymous +app_update 258550 validate +quit | tee -a ${LOGS}
  # Update RCON CLI tool
  echo " - No rcon found here, downloading it..." | tee -a ${LOGS}
  LATEST_RCON=$(curl -q https://github.com/gorcon/rcon-cli/releases | grep "/releases/tag" | head -n 1 | awk -F "v" '{ print $3 }' | awk -F "\"" {' print $1 '})
  wget https://github.com/gorcon/rcon-cli/releases/download/v${LATEST_RCON}/rcon-${LATEST_RCON}-amd64_linux.tar.gz
  tar xzvf rcon-${LATEST_RCON}-amd64_linux.tar.gz
  mv rcon-${LATEST_RCON}-amd64_linux/rcon ${GAME_ROOT}/rcon
  rm -rf rcon-${LATEST_RCON}-amd64_linux*
  # Update uMod (Oxide) libraries
  echo "=> Updating uMod..." | tee -a ${LOGS}
  # https://github.com/OxideMod/Oxide.Rust/releases
  # https://umod.org/games/rust/
  aws s3 cp ${S3_REPO}/Oxide.Rust-linux.zip ${GAME_ROOT}
  cd ${GAME_ROOT} && unzip -o Oxide.Rust-linux.zip | tee -a ${LOGS}
  rm Oxide.Rust-linux.zip | tee -a ${LOGS}
  # Update Discord integrations
  echo "=> Downloading discord binary..." | tee -a ${LOGS}
  # https://umod.org/extensions/discord/download
  aws s3 cp ${S3_REPO}/Oxide.Ext.Discord.dll ${GAME_ROOT}/RustDedicated_Data/Managed/
  # Update RustEdit libraries
  echo "=> Downloading RustEdit.io binary..." | tee -a ${LOGS}
  wget https://github.com/k1lly0u/Oxide.Ext.RustEdit/raw/master/Oxide.Ext.RustEdit.dll -O \
    ${GAME_ROOT}/RustDedicated_Data/Managed/Oxide.Ext.RustEdit.dll | tee -a ${LOGS}
}

function update_permissions() {
  echo "=> Updating plugin permissions" | tee -a ${LOGS}
  echo " - \"o.load *\"" | tee -a ${LOGS}
  ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.load *" | tee -a ${LOGS}
  sleep 5
  for perm in ${DEFAULT_PERMS[@]}; do
    echo " - \"o.grant default $perm\""
    ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.grant group default $perm" | tee -a ${LOGS}
    sleep 5
  done
  echo "=> Reload permissions sync" | tee -a ${LOGS}

  ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.reload PermissionGroupSync" | tee -a ${LOGS}
}

function update_map_api() {
  echo "Updating Map API data" | tee -a ${LOGS}
  ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "rma_regenerate" | tee -a ${LOGS}
  sleep 10
  echo "Uploading Map to Imgur" | tee -a ${LOGS}
  ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "rma_upload default 1800 1 1" | tee -a ${LOGS}
  sleep 10
  IMGUR_URL=$(tail -n 1000 ${GAME_ROOT}/RustDedicated.log | grep "imgur.com" | tail -n 1 | awk '{print $4}')
  echo "Successfully uploaded: ${IMGUR_URL}" | tee -a ${LOGS}
  wget ${IMGUR_URL} -O ${GAME_ROOT}/oxide/data/LustyMap/current.jpg
  echo "Installed new map graphic: ${GAME_ROOT}/oxide/data/LustyMap/current.jpg" | tee -a ${LOGS}
  echo "Uploading to S3" | tee -a ${LOGS}
  aws s3 cp ${GAME_ROOT}/oxide/data/LustyMap/current.jpg ${S3_WEB}/maps/${HOSTNAME}.jpg
  ${GAME_ROOT}/rcon --log ${LOGS} --config ${RCON_CFG} "o.reload LustyMap" | tee -a ${LOGS}
}

# Default Game permissions
DEFAULT_PERMS=(
  Kits.default
  autobaseupgrade.use
  autocode.try
  autocode.use
  autodoors.use
  autolock.use
  automaticauthorization.use
  backpacks.gui
  backpacks.use
  baserepair.use
  betterrootcombiners.use
  bgrade.all
  bloodtrail.allow
  blueprintmanager.all
  blueprintshare.share
  blueprintshare.toggle
  blueprintshare.use
  buildinggrades.down.all
  buildinggrades.up.all
  buildinggrades.use
  buildingworkbench.use
  carcommander.canbuild
  carcommander.canspawn
  carcommander.use
  carlockui.use.codelock
  carturrets.allmodules
  carturrets.deploy.command
  carturrets.deploy.inventory
  carturrets.limit.2
  chute.allowed
  clearrepair.use
  craftchassis.2
  crafts.use
  craftsman.leveling.clothing
  craftsman.leveling.melee
  craftsman.leveling.ranged
  customgenetics.use
  dance.use
  discordmessages.report
  discordmessages.message
  farmtools.clone
  farmtools.clone.all
  farmtools.harvest.all
  fishing.allowed
  fishing.makepole
  fuelgauge.allow
  furnacesplitter.use
  globalstorage.access
  heal.player
  heal.self
  instantcraft.use
  instantmixingtable.use
  iteminspector.use
  itemskinrandomizer.reskin
  itemskinrandomizer.use
  localize.use
  nteleportation.deletehome
  nteleportation.home
  nteleportation.homehomes
  nteleportation.importhomes
  nteleportation.radiushome
  nteleportation.tpb
  nteleportation.tpbandit
  nteleportation.tphome
  nteleportation.tpoutpost
  nteleportation.tpr
  nteleportation.tptown
  nteleportation.wipehomes
  patrolboat.builder
  pets.bear
  pets.boar
  pets.chicken
  pets.horse
  pets.stag
  pets.wolf
  phonesplus.use
  privatemessages.allow
  quicksmelt.use
  quicksort.autolootall
  quicksort.lootall
  quicksort.use
  raidalarm.use
  realistictorch.use
  recyclerspeed.use
  removertool.normal
  securitycameras.use
  securitylights.use
  signartist.raw
  signartist.restore
  signartist.restoreall
  signartist.text
  signartist.url
  simpletime.use
  skins.use
  sleep.allow
  statistics.use
  trade.accept
  trade.use
  turretloadouts.autoauth
  turretloadouts.autotoggle
  turretloadouts.manage
  turretloadouts.manage.custom
  unwound.canuse
  vehicledeployedlocks.codelock.allvehicles
  vehicledeployedlocks.codelock.duosub
  vehicledeployedlocks.codelock.solosub
  vehicledeployedlocks.keylock.allvehicles
  vehicledeployedlocks.keylock.duosub
  vehicledeployedlocks.keylock.solosub
  vehiclevendoroptions.ownership.allvehicles
  carradio.attachcarradio
  carradio.detachcarradio
)

echo "SRT Update Functions initialized" | tee -a ${LOGS}
