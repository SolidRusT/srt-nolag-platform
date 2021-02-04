using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Code Sync", "Wulf", "2.0.1")]
    [Description("Automatically syncs all code locks on a building with the code for the tool cupboard")]
    class CodeSync : CovalencePlugin
    {
        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["CodeSynced"] = "Code will automatically be set as the code for the tool cupboard"
            }, this);
        }

        #endregion Localization

        #region Lock Handling

        private BuildingManager.Building GetBuilding(BaseEntity entity)
        {
            BuildingManager.Building building = (entity.GetParentEntity() as DecayEntity)?.GetBuilding();
            if (building == null || building.buildingPrivileges == null || building.buildingPrivileges.Count == 0)
            {
                // TODO: Does not work for high external gates; they do not store which building they are attached to
                //Log($"Could not find building for {codeLock.GetParentEntity()?.PrefabName}");
                return null;
            }

            return building;
        }

        private CodeLock GetCupboardLock(BuildingManager.Building building)
        {
            return building.buildingPrivileges[0].GetSlot(BaseEntity.Slot.Lock) as CodeLock;
        }

        private void CanChangeCode(BasePlayer basePlayer, CodeLock codeLock)
        {
            // TODO: Support changing code on any code lock, for all building code locks
            Message(basePlayer, "CodeSynced");
        }

        private object CanUseLockedEntity(BasePlayer basePlayer, CodeLock codeLock)
        {
            // Check if entity is already unlocked
            if (!codeLock.IsLocked())
            {
                return null;
            }

            BuildingManager.Building building = GetBuilding(codeLock);
            if (building == null)
            {
                return null;
            }

            CodeLock tcLock = GetCupboardLock(building);
            if (tcLock == null || tcLock.code != codeLock.code && tcLock.guestCode != codeLock.code)
            {
                return null;
            }

            // Check for and allow player if whitelist
            if (tcLock.whitelistPlayers.Contains(basePlayer.userID) || tcLock.guestPlayers.Contains(basePlayer.userID))
            {
                return true;
            }

            return null;
        }

        private void OnCodeEntered(CodeLock codeLock, BasePlayer basePlayer, string code)
        {
            BuildingManager.Building building = GetBuilding(codeLock);
            if (building == null)
            {
                return;
            }

            // Check if code entered matches tool cupboard code
            CodeLock tcLock = GetCupboardLock(building);
            if (tcLock != null)
            {
                if (tcLock.code == code && codeLock.code != code)
                {
                    codeLock.code = code;
                }
                else if (tcLock.guestCode == code && codeLock.code != code)
                {
                    codeLock.guestCode = code;
                }
            }
        }

        #endregion Lock Handling

        #region Helpers

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(BasePlayer basePlayer, string langKey, params object[] args)
        {
            if (basePlayer.IsConnected)
            {
                basePlayer.IPlayer.Message(GetLang(langKey, basePlayer.UserIDString, args));
            }
        }

        #endregion Helpers
    }
}
