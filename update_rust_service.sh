## Offline server update
GAME_DIR="/game"
cd ${GAME_DIR}
LOG_DATE=$(date +"%Y_%m_%d_%I_%M_%p")

# refresh OS packages
echo "===> Buffing-up Debian Distribution..."
sudo apt update
sudo apt -y dist-upgrade
# TODO: output a message to reboot if kernel or initrd was updated

# Refresh Steam installation
echo "===> Validating installed Steam components..."
steamcmd +login anonymous +force_install_dir ${GAME_DIR} +app_update 258550 validate +quit

# Update uMod platform
echo "===> Updating uMod..."
wget https://umod.org/games/rust/download/develop -O \
    Oxide.Rust.zip && \
    unzip -o Oxide.Rust.zip && \
    rm Oxide.Rust.zip

# Integrate discord binary
echo "===> Downloading discord binary..."
wget https://umod.org/extensions/discord/download -O \
    ${GAME_DIR}/RustDedicated_Data/Managed/Oxide.Ext.Discord.dll

# Integrate RustEdit binary
echo "===> Downloading RustEdit.io binary..."
wget https://github.com/k1lly0u/Oxide.Ext.RustEdit/raw/master/Oxide.Ext.RustEdit.dll -O \
    ${GAME_DIR}/RustDedicated_Data/Managed/Oxide.Ext.RustEdit.dll

# Integrate Rust:IO binary
wget http://playrust.io/latest -O \
    ${GAME_DIR}/RustDedicated_Data/Managed/Oxide.Ext.RustIO.dll

# Update custom maps
aws s3 sync s3://solidrust.net/maps ${GAME_DIR}/server/solidrust
