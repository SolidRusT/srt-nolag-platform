using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Custom Event Announcements", "klauz24", "1.0.1"), Description("Allows managing event announcements.")]
    internal class CustomEventAnnouncements : HurtworldPlugin
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Events/Iron Meteor Shower", "<color=gray>Iron Meteor</color> is on the way!"},
                {"Events/Titranium Meteor Shower", "<color=red>Titranium Meteor</color> is on the way!"},
                {"Events/Mondinium Meteor Shower", "<color=green>Mondinium Meteor</color> is on the way!"},
                {"Events/Ultranium Meteor Shower", "<color=blue>Ultranium Meteor</color> is on the way!"},
                {"Events/Amber Meteor Shower", "<color=yellow>Amber Meteor</color> is on the way!"},
                {"Events/Control Town Event", "<color=orange>Town Control Event</color> has started!"},
                {"Events/Loot Frenzy Town Event", "<color=cyan>Loot Frenzy Event</color> has started!"},
                {"Airdrop", "<color=black>Cargo Plane</color> is on the way!"}
            }, this);
        }

        private object OnMeteorShowerBroadcast(MeteorShowerEvent shower, string title)
        {
            Broadcast(shower.NameKey);
            return true;
        }

        private object OnTownEventBroadcast(BaseTownEvent townEvent, string title)
        {
            Broadcast(townEvent.NameKey);
            return true;
        }

        private void OnAirdrop(GameObject obj, AirDropEvent airdrop, List<ItemObject> items) => Broadcast("Airdrop");

        private void Broadcast(string nameKey)
        {
            foreach (var str in lang.GetMessages(lang.GetServerLanguage(), this))
            {
                if (nameKey == str.Key)
                {
                    foreach (var session in GameManager.Instance.GetSessions().Values)
                    {
                        Player.Message(session, lang.GetMessage(str.Value, this, session.SteamId.ToString()));
                    }
                    break;
                }
            }
        }
    }
}