using System;
using System.Collections;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Real Time", "haggbart", "1.0.0")]
    [Description("Syncs ingame time with real time")]
    class RealTime : RustPlugin
    {
        private bool enabled = true;
        private TOD_Time todTime;
        private WaitForSeconds waitForSeconds;
        private float offsetHours;

        private const string OFFSET_HOURS = "Time offset in hours";
        private const float SECONDS_BETWEEN = 59f;

        protected override void LoadDefaultConfig()
        {
            Config[OFFSET_HOURS] = 0f;
        }
        
        private void OnServerInitialized()
        {
            waitForSeconds = new WaitForSeconds(SECONDS_BETWEEN);
            offsetHours = Convert.ToSingle(Config[OFFSET_HOURS]);
            todTime = TOD_Sky.Instance.Components.Time;
            todTime.ProgressTime = false;
            todTime.UseTimeCurve = false;
            SyncRealTime();
            todTime.RefreshTimeCurve();
            ServerMgr.Instance.StartCoroutine(AddTimeAndSync());
        }

        private void Unload()
        {
            todTime.ProgressTime = true;
            todTime.UseTimeCurve = true;
            enabled = false;
        }

        private IEnumerator AddTimeAndSync()
        {
            while (enabled)
            {
                yield return waitForSeconds;
                todTime.AddHours(SECONDS_BETWEEN / 3600f, false);
                SyncRealTime();
            }
        }
        
        private void SyncRealTime()
        {
            TOD_Sky.Instance.Cycle.Hour = (float)DateTime.Now.TimeOfDay.TotalHours + offsetHours;
        }
    }
}