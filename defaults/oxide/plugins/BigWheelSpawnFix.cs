namespace Oxide.Plugins
{
    // Creation date: 11-04-2021
    // Last update date: UPDATE_DATE
    [Info("Big Wheel Spawn Fix", "Orange", "1.0.0")]
    [Description("Fixes big wheels spawned faced down")]
    public class BigWheelSpawnFix : RustPlugin
    {
        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(BigWheelGame entity)
        {
            var transform = entity.transform;
            var old = transform.eulerAngles;
            old.x = 90;
            transform.eulerAngles = old;
        }
    }
}