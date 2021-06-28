using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Facepunch;

#region Changelogs and ToDo
/**********************************************************************
 * 
 * v1.0.1   :   Changed cfg for language message
 * v1.0.2   :   Excempt for softcore bags in bandit and outpost
 * 
 **********************************************************************/
#endregion

namespace Oxide.Plugins
{
    [Info("Bed Welcomer", "Krungh Crow", "1.0.3")]
    [Description("Changes the default text on bags towels and beds")]

    class BedWelcomer : RustPlugin
    {
        #region LanguageAPI

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BagText"] = "Welcome to our server",
            }, this);
        }

        #endregion

        #region Hooks

        private void OnEntitySpawned(SleepingBag bag)
        {
            if (bag.niceName == "Unnamed Bag" || bag.niceName == "Unnamed Towel" || bag.niceName == "Bed")
            {
                bag.niceName = lang.GetMessage("BagText", this);
            }
            return;
        }

        #endregion
    }
}