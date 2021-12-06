SHIT=(NPCDropGun
ZombieHorde
RaidingZombies
RaidableBases)

for shit in ${SHIT[@]}; do
    rcon "o.reload $shit"
done