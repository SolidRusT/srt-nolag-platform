namespace Oxide.Plugins
{
    [Info("Better Root Combiners", "WhiteThunder", "1.0.0")]
    [Description("Allows root combiners to accept input from any electrical source.")]
    internal class BetterRootCombiners : CovalencePlugin
    {
        private const string PermissionUse = "betterrootcombiners.use";

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var combiner = entity as ElectricalCombiner;
                if (combiner != null)
                    OnEntitySpawned(combiner);
            }

            Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(ElectricalCombiner combiner)
        {
            var rootConnectionsOnly = combiner.OwnerID == 0
                || !permission.UserHasPermission(combiner.OwnerID.ToString(), PermissionUse);

            foreach (var input in combiner.inputs)
                input.rootConnectionsOnly = rootConnectionsOnly;
        }
    }
}
