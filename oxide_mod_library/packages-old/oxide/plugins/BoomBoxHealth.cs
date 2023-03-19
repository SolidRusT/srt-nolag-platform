using Rust;

namespace Oxide.Plugins
{
    [Info("Boom Box Health", "Waldobert", "1.0.1")]
    [Description("Disables the playing-decay of the boombox")]


    class BoomBoxHealth : RustPlugin
    {

        void OnEntityTakeDamage(DeployableBoomBox entity, HitInfo info)
        {
            
            if (info.damageTypes.GetMajorityDamageType() == DamageType.Decay && entity.IsOn())
            {

                info.damageTypes.Scale(DamageType.Decay, 0.0f);

            }
            
        }

    }

}
