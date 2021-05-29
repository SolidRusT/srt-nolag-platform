using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Unlimited Drone Range", "WhiteThunder", "1.0.1")]
    [Description("Removes the range limit when remote-controlling drones from a computer station.")]
    internal class UnlimitedDroneRange : CovalencePlugin
    {
        private void OnServerInitialized()
        {
            LogWarning("This plugin is no longer necessary. Facepunch fixed the RC drone range issue in the March 2021 update. You may uninstall this plugin.");
        }

        private void Unload()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone != null && !(drone is DeliveryDrone))
                {
                    var component = drone.GetComponent<DroneNetworkGroupUpdater>();
                    if (component != null)
                        UnityEngine.Object.Destroy(component);
                }
            }
        }

        private void OnBookmarkControl(ComputerStation station, BasePlayer player, string bookmarkName, Drone drone)
        {
            // Without a delay, we can't know whether another plugin blocked the entity from being controlled.
            NextTick(() =>
            {
                if (station == null || station.currentlyControllingEnt.uid != drone.net.ID || station._mounted != player)
                    return;

                // Check if there's already a component for some unknown reason, just in case.
                var component = drone.gameObject.GetComponent<DroneNetworkGroupUpdater>();
                if (component == null)
                    component = drone.gameObject.AddComponent<DroneNetworkGroupUpdater>();

                component.Controller = player;
            });
        }

        private void OnBookmarkControlEnd(ComputerStation station, BasePlayer player, Drone drone)
        {
            var component = drone.gameObject.GetComponent<DroneNetworkGroupUpdater>();
            if (component != null)
                UnityEngine.Object.Destroy(component);
        }

        private class DroneNetworkGroupUpdater : MonoBehaviour
        {
            private Drone _drone;
            private Network.Visibility.Group _networkGroup;

            public BasePlayer Controller;

            private void Awake()
            {
                _drone = GetComponent<Drone>();
                _networkGroup = _drone.net.group;
            }

            private void Update()
            {
                if (Controller == null)
                    return;

                var currentGroup = _drone.net.group;
                if (currentGroup != _networkGroup)
                {
                    Controller.net.SwitchSecondaryGroup(currentGroup);
                    _networkGroup = currentGroup;
                }
            }
        }
    }
}
