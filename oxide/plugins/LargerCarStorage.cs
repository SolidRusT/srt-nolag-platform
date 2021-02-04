using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Larger Car Storage", "WhiteThunder", "2.0.2")]
    [Description("Increases the capacity of storage modules on modular cars.")]
    internal class LargerCarStorage : CovalencePlugin
    {
        #region Fields

        private const int ItemsPerRow = 6;

        private const string PermissionSizePrefix = "largercarstorage.size";
        private const int MinPermissionSize = 4;
        private const int MaxPermissionSize = 7;

        private const string MaximumCapacityPanelName = "genericlarge";
        private const int MaximumCapacity = 42;

        private readonly Dictionary<string, int> DisplayCapacityByPanelName = new Dictionary<string, int>
        {
            ["modularcar.storage"] = 18,
            ["largewoodbox"] = 30,
            ["generic"] = 36,
            [MaximumCapacityPanelName] = MaximumCapacity,
        };

        private LargerCarStorageConfig PluginConfig;

        #endregion

        #region Hooks

        private void Init()
        {
            PluginConfig = Config.ReadObject<LargerCarStorageConfig>();

            for (var size = MinPermissionSize; size <= MaxPermissionSize; size++)
                permission.RegisterPermission(GetSizePermission(size), this);
        }

        private void OnServerInitialized(bool initialBoot)
        {
            // The OnEntitySpawned hook already covers server boot so this is just for late loading
            if (!initialBoot)
            {
                foreach (var car in BaseNetworkable.serverEntities.OfType<ModularCar>())
                    RefreshStorageCapacity(car);
            }
        }

        private void OnEntitySpawned(VehicleModuleStorage storageModule)
        {
            NextTick(() => {
                if (storageModule == null) return;
                var car = storageModule.Vehicle as ModularCar;
                var capacity = GetPlayerAllowedCapacity(car?.OwnerID ?? 0);
                var panelName = GetSmallestPanelForCapacity(capacity);
                RefreshStorageCapacity(storageModule, capacity, panelName);
            });
        }

        private void OnGroupPermissionGranted(string group, string perm)
        {
            if (!perm.StartsWith(PermissionSizePrefix)) return;
            OnStoragePermissionChanged();
        }

        private void OnGroupPermissionRevoked(string group, string perm)
        {
            if (!perm.StartsWith(PermissionSizePrefix)) return;
            OnStoragePermissionChanged();
        }

        private void OnUserPermissionGranted(string userId, string perm)
        {
            if (!perm.StartsWith(PermissionSizePrefix)) return;
            OnStoragePermissionChanged(userId);
        }

        private void OnUserPermissionRevoked(string userId, string perm)
        {
            if (!perm.StartsWith(PermissionSizePrefix)) return;
            OnStoragePermissionChanged(userId);
        }

        // Compatibility with plugin: Claim Vehicle Ownership
        private void OnVehicleOwnershipChanged(ModularCar car) => RefreshStorageCapacity(car);

        #endregion

        #region API

        private void API_RefreshStorageCapacity(ModularCar car) => RefreshStorageCapacity(car);

        #endregion

        #region Helper Methods

        private string GetSizePermission(int numRows) => $"{PermissionSizePrefix}.{numRows}";

        private void OnStoragePermissionChanged(string userIdString = "")
        {
            foreach (var car in BaseNetworkable.serverEntities.OfType<ModularCar>())
            {
                if (car.OwnerID == 0 || userIdString != string.Empty && userIdString != car.OwnerID.ToString())
                    continue;

                RefreshStorageCapacity(car);
            }
        }

        private void RefreshStorageCapacity(ModularCar car)
        {
            var capacity = GetPlayerAllowedCapacity(car?.OwnerID ?? 0);
            var panelName = GetSmallestPanelForCapacity(capacity);

            foreach (var module in car.AttachedModuleEntities)
            {
                var storageModule = module as VehicleModuleStorage;
                if (storageModule != null)
                    RefreshStorageCapacity(storageModule, capacity, panelName);
            }
        }

        private void RefreshStorageCapacity(VehicleModuleStorage storageModule, int capacity, string panelName)
        {
            if (storageModule is VehicleModuleEngine) return;

            var container = storageModule.GetContainer() as StorageContainer;
            if (container == null || container is ModularVehicleShopFront) return;

            container.panelName = panelName;
            container.inventory.capacity = capacity;
        }

        private int GetPlayerAllowedCapacity(ulong userId)
        {
            int defaultCapacity = PluginConfig.GetCapacity();

            if (userId == 0)
                return defaultCapacity;

            var userIdString = userId.ToString();

            for (var size = MaxPermissionSize; size >= MinPermissionSize; size--)
            {
                int capacity = size * ItemsPerRow;
                if (capacity > defaultCapacity && permission.UserHasPermission(userIdString, GetSizePermission(size)))
                    return capacity;
            }

            return defaultCapacity;
        }

        private string GetSmallestPanelForCapacity(int capacity)
        {
            string panelName = MaximumCapacityPanelName;
            int displayCapacity = MaximumCapacity;

            foreach (var entry in DisplayCapacityByPanelName)
            {
                if (entry.Value >= capacity && entry.Value < displayCapacity)
                {
                    panelName = entry.Key;
                    displayCapacity = entry.Value;
                }
            }

            return panelName;
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig() => Config.WriteObject(new LargerCarStorageConfig(), true);

        internal class LargerCarStorageConfig
        {
            [JsonProperty("DefaultCapacityRows")]
            public int DefaultCapacityRows = 3;

            // Legacy field for backwards compatibility
            [JsonProperty("GlobalSettings", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public StorageSettings GlobalSettings = null;

            public int GetCapacity()
            {
                int capacity = GlobalSettings != null ? GlobalSettings.StorageCapacity : DefaultCapacityRows * ItemsPerRow;
                return Math.Min(Math.Max(capacity, 0), MaximumCapacity);
            }
        }

        internal class StorageSettings
        {
            [JsonProperty("StorageCapacity")]
            public int StorageCapacity = MaximumCapacity;
        }

        #endregion
    }
}
