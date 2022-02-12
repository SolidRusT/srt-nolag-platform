using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Diseases", "mr01sam", "2.1.0")]
	[Description("Players can be inflicted with disease that can be spread to others")]
	public class Diseases : CovalencePlugin
	{
		public static Diseases PLUGIN;

		/* Permissions */
		private const string PermissionUse = "diseases.use";
		private const string PermissionAdmin = "diseases.admin";

		/* Dependencies */
		[PluginReference]
		private Plugin ImageLibrary;

		/* Constants */
		private const float PLAYER_TICK_RATE = 1f;
		private static bool debug = false;

		/* Global data */
		private DiseaseManager diseaseManager;
		private EffectManager effectManager;
		private Dictionary<string, DiseaseStats> diseaseStats;

		#region Oxide Hooks
		void Init()
		{
			PLUGIN = this;
			/* Unsubscribe */
			Unsubscribe(nameof(OnEntityDeath));
			Unsubscribe(nameof(OnPlayerSleep));
			Unsubscribe(nameof(OnPlayerSleepEnded));
			Unsubscribe(nameof(OnItemUse));
			Unsubscribe(nameof(OnEntityTakeDamage));

			/* Init */
			InitCommands();
		}

		void Unload()
		{
			/* Clear effects and UI */
			foreach (BasePlayer player in BasePlayer.activePlayerList)
			{
				effectManager.ClearAllEffects(player);
				HideAllIndicators(player);
			}
		}

		void OnServerInitialized(bool initial)
		{
			/* Load images (if ImageLibrary is installed) */
			LoadImages();

			/* Register perms */
			permission.RegisterPermission(PermissionUse, this);

			/* Init */
			diseaseStats = new Dictionary<string, DiseaseStats>();
			diseaseManager = new DiseaseManager();
			effectManager = new EffectManager();

			/* Load disease files */
			diseaseManager.LoadAllDiseases(() =>
			{
				foreach (BasePlayer player in BasePlayer.activePlayerList)
					RefreshIndicator(player);
			});

			/* Unsubscribe */
			Subscribe(nameof(OnEntityDeath));
			Subscribe(nameof(OnPlayerSleep));
			Subscribe(nameof(OnPlayerSleepEnded));
			Subscribe(nameof(OnItemUse));
			Subscribe(nameof(OnEntityTakeDamage));
		}

		void OnEntityDeath(BasePlayer player, HitInfo info)
		{
			if (player.IsValid())
			{
				foreach (string diseasename in diseaseManager.GetPlayerInfections(player))
				{
					Disease disease = diseaseManager.GetDisease(diseasename);
					if (disease != null)
					{
						bool killedByDisease = WasCauseOfDeath(player, info, disease);
						if (killedByDisease)
						{
							diseaseStats[disease.name].playersKilled.count += 1;
							if (config.BroadcastDeathMessages)
								BasePlayer.activePlayerList.ToList().ForEach(x => x.ChatMessage(Lang("Death", player.UserIDString, player.displayName, Color(disease.name, config.DiseaseNameColor))));
						}
						if (debug)
							PLUGIN.Puts("Died: " + player.displayName + " removing of " + disease.name);

						diseaseStats[disease.name].activeInfected.count -= 1;
						diseaseManager.RemoveInfected(player, disease);
						Interface.CallHook("OnInfectedDeath", player, disease, killedByDisease);
					}
				}
				RefreshIndicator(player);
			}
		}

		object OnPlayerSleep(BasePlayer player)
		{
			if (player.IsValid())
			{
				foreach (string diseasename in diseaseManager.GetPlayerInfections(player))
				{
					diseaseStats[diseasename].sleepersInfected.count += 1;
					diseaseStats[diseasename].activeInfected.count -= 1;
				}
			}
			return null;
		}

		void OnPlayerSleepEnded(BasePlayer player)
		{
			if (debug)
				PLUGIN.Puts("Sleep ended for " + player.displayName);
			if (player.IsValid())
			{
				if (debug)
					PLUGIN.Puts("Is valid: " + player.displayName);
				foreach (string diseasename in diseaseManager.GetPlayerInfections(player))
				{
					Disease disease = diseaseManager.GetDisease(diseasename);
					if (debug)
						PLUGIN.Puts("Has active tick: " + diseaseManager.HasActiveTick(player, disease));
					if (disease != null && diseaseManager.HasActiveTick(player, disease))
					{
						if (diseaseManager.HasImmunity(player, disease))
						{
							if (debug)
								PLUGIN.Puts("Resuming recovery for " + player.displayName + " with " + disease.name);
							DoRecoverTick(player, disease);
						}
						else
						{
							if (debug)
								PLUGIN.Puts("Resuming infection for " + player.displayName + " with " + disease.name);
							DoInfectedTick(player, disease);
						}
						diseaseStats[disease.name].sleepersInfected.count -= 1;
						diseaseStats[disease.name].activeInfected.count += 1;
					}
				}
				RefreshIndicator(player);
			}
		}

		void OnItemUse(Item item, int amountToUse)
		{
			if (item == null) return;

			BasePlayer player = item.GetOwnerPlayer();

			if (!player.IsValid()) return;

			foreach (Disease disease in diseaseManager.LoadedDiseases.Values)
			{
				CureItem ci = diseaseManager.ItemsThatCure.Get(item.info.shortname, disease.name);
				if (ci != null && ci.cureChanceOnConsumption >= UnityEngine.Random.Range(0, 100))
				{
					RecoverPlayer(player, disease, () => {
						diseaseStats[disease.name].timesCuredFromItem.count += 1;
					});
				}

				InfectionItem ii = diseaseManager.ItemsThatInfect.Get(item.info.shortname, disease.name);
				if (ii != null && ii.infectionChanceOnConsumption >= UnityEngine.Random.Range(0, 100))
				{
					InfectPlayer(player, disease, () =>
					{
						diseaseStats[disease.name].infectedFromItem.count += 1;
					});
				}

				TreatmentItem ti = diseaseManager.ItemsThatTreat.Get(item.info.shortname, disease.name);
				if (ti != null)
				{
					TreatPlayer(player, disease, ti.Value(), ti.treatmentDelay, () =>
					{
						diseaseStats[disease.name].timesTreatedFromItem.count += 1;
					});
				}
			}
		}

		object OnEntityTakeDamage(BasePlayer player, HitInfo info)
		{
			if (!player.IsValid() || info == null || info.Initiator == null) return null;

			foreach (Disease disease in diseaseManager.LoadedDiseases.Values)
			{
				if (debug)
					Puts(disease.name);
				SpreaderEntity se = diseaseManager.EntitiesThatInfect.Get(info.Initiator.ShortPrefabName, disease.name);
				if (se != null && (se.shortPrefabName != "player" || !info.IsProjectile()) && se.infectionChanceOnHit >= UnityEngine.Random.Range(0, 100))
				{
					if (debug)
						Puts("Should infect: " + player.displayName + " with " + disease.name);
					InfectPlayer(player, disease, () =>
					{
						diseaseStats[disease.name].infectedFromEntity.count += 1;
					});
				}
			}
			return null;
		}
		#endregion

		#region Helper Functions
		private void LoadImages()
		{
			if (ImageLibrary != null && ImageLibrary.IsLoaded)
			{
				ImageLibrary.Call<bool>("AddImage", "https://i.imgur.com/BODiaSy.png", "DiseaseIndicatorIcon", 0UL);
			}
		}

		private void DoDiseaseTick(Disease disease)
		{
			if (disease.outbreakConditions.enabled)
			{
				timer.In(disease.outbreakConditions.timeInterval, () =>
				{
					/* Attempt outbreak */
					Outbreak(disease);

					/* Continue */
					DoDiseaseTick(disease);
				});
			}
		}

		private void DoInfectedTick(BasePlayer player, Disease disease)
		{
			if (player.IsConnected && player.IsAlive() && player.IsValid() && !player.IsSleeping() && disease != null && diseaseManager.HasDisease(player, disease) && !diseaseManager.HasImmunity(player, disease))
			{
				/* Show symptoms */
				if (debug)
					Puts("Showing symptom for: " + player.displayName);
				DoSymptomDamage(player, disease);
				DoSymptomEffects(player, disease);
				DoSymptomScreenEffects(player, disease);

				/* Spread to other players */
				foreach (BasePlayer entity in NearbyValidPlayers(player.transform.position, disease))
				{
					if (GetSpreadChance(entity, disease) >= UnityEngine.Random.Range(1, 100))
					{
						if (debug)
							Puts("Spread " + disease.name + " from " + player.name + " to " + entity.displayName);
						InfectPlayer(entity, disease, () =>
						{
							diseaseStats[disease.name].infectedFromSpreading.count += 1;
						});
					}
				}

                /* Continue timer */
				int spreadTime = disease.SpreadTime();
				timer.In(spreadTime, () =>
				{
					if (diseaseManager.UpdateInfectedTime(player, disease, spreadTime))
					{
						RecoverPlayer(player, disease);
					}
					else
					{
						DoInfectedTick(player, disease);
					}
				});
			}
			else
			{
				if (debug)
					Puts("Stopped infection tick for: " + player.displayName);
			}
		}

		private void DoRecoverTick(BasePlayer player, Disease disease)
		{
			if (player.IsConnected && player.IsAlive() && player.IsValid() && !player.IsSleeping())
			{
				diseaseManager.SetActiveTick(player, disease, true);
				RefreshIndicator(player);
				timer.In(disease.ImmunityTime(), () =>
				{
					if (player.IsConnected && player.IsAlive() && player.IsValid() && !player.IsSleeping())
					{
						CurePlayer(player, disease);
					}
				});
			}
			else
			{
				if (debug)
					Puts("Stopped recovery tick for: " + player.displayName);
			}
		}

		private List<BaseCombatEntity> NearbyValidPlayers(Vector3 position, Disease disease)
		{
			List<BaseCombatEntity> valid = new List<BaseCombatEntity>();
			List<BasePlayer> nearby = new List<BasePlayer>();
			Vis.Entities(position, disease.spreadDistance, nearby); foreach (BasePlayer entity in nearby)
			{
				if (diseaseManager.CanInfect(entity, disease))
				{
					valid.Add(entity);
				}
			}
			return valid;
		}

		private void InfectPlayer(BasePlayer player, Disease disease, Action callback = null)
		{
			if (Interface.CallHook("OnPlayerInfected", player, disease) != null) return;
			if (diseaseManager.AddInfected(player, disease))
			{
				if (debug)
					Puts("Started tick for: " + player.displayName);
				diseaseManager.SetActiveTick(player, disease, true);
				DoInfectedTick(player, disease);
				RefreshIndicator(player);
				StatusMessage(player, disease, Lang("StatusInfected", player.UserIDString, Color(disease.name, config.DiseaseNameColor)));
				diseaseStats[disease.name].activeInfected.count += 1;
				diseaseStats[disease.name].totalInfected.count += 1; ;
				callback?.Invoke();
			}
		}

		private void RecoverPlayer(BasePlayer player, Disease disease, Action callback = null)
		{
			if (diseaseManager.HasDisease(player, disease) && diseaseManager.SetRecovery(player, disease))
			{
				if (debug)
					Puts("Started recovery for: " + player.displayName);
				diseaseManager.SetActiveTick(player, disease, true);
				DoRecoverTick(player, disease);
				RefreshIndicator(player);
				StatusMessage(player, disease, Lang("StatusRecovered", player.UserIDString, Color(disease.name, config.DiseaseNameColor)));
				callback?.Invoke();
			}
		}

		private int Outbreak(Disease disease, int tries = 1)
		{
			if (Interface.CallHook("OnOutbreak", disease) != null) return 0;

			int infected = 0;
			if (debug)
				Puts("Attempting outbreak for " + disease.name);

			List<BasePlayer> activePlayerList = BasePlayer.activePlayerList.ToList().Where(p => diseaseManager.CanInfect(p, disease)).ToList();
			if (debug)
				Puts("Valid players: " + activePlayerList.Count);
			if (activePlayerList.Count > 0)
			{
				List<BasePlayer> filteredList = activePlayerList.ToArray().ToList(); // Make a copy
				foreach (StatFilter filter in disease.outbreakConditions.statFilters)
				{
					filteredList = filteredList.Where(p => CompareStat(p, filter.stat, filter.minValue, (x, y) => { return x >= y; }) && CompareStat(p, filter.stat, filter.maxValue, (x, y) => { return x <= y; })).ToList();
				}
				if (debug)
					Puts("Filters players: " + filteredList.Count);

				for (int i = 0; i < tries; i++)
				{
					if (filteredList.Count > 0)
					{
						BasePlayer randomPlayer = filteredList[UnityEngine.Random.Range(0, filteredList.Count - 1)];
						if (disease.outbreakConditions.outbreakChance >= UnityEngine.Random.Range(0, 100))
						{
							InfectPlayer(randomPlayer, disease);
							infected++;
							filteredList.Remove(randomPlayer);
						}
					}
				}
				diseaseStats[disease.name].infectedFromOutbreak.count += infected;
			}
			diseaseStats[disease.name].numOutbreaks.count += 1; ;
			if (infected > 0)
			{
				if (config.BroadcastOutbreaks)
					BasePlayer.activePlayerList.ToList().ForEach(x => x.ChatMessage(Lang("StatusOutbreak", x.UserIDString, Color(disease.name, config.DiseaseNameColor))));
			}
			return infected;
		}

		private void CurePlayer(BasePlayer player, Disease disease, Action callback = null)
		{
			if (Interface.CallHook("OnPlayerCured", player, disease) != null) return;
			if (diseaseManager.RemoveInfected(player, disease))
			{
				if (debug)
					Puts("Cured: " + player.displayName + " of " + disease.name);
				RefreshIndicator(player);
				diseaseManager.SetActiveTick(player, disease, false);
				StatusMessage(player, disease, Lang("StatusCured", player.UserIDString, Color(disease.name, config.DiseaseNameColor)));
				diseaseStats[disease.name].activeInfected.count -= 1;
				callback?.Invoke();
			}
		}

		private void TreatPlayer(BasePlayer player, Disease disease, int amount, int delay, Action callback = null)
		{
			if (Interface.CallHook("OnPlayerTreated", player, disease) != null) return;
			if (diseaseManager.HasDisease(player, disease) && !diseaseManager.HasTreatment(player, disease) && !diseaseManager.HasImmunity(player, disease))
			{
				if (debug)
					Puts("Treated: " + player.displayName + " of " + disease.name + " for " + amount);
				diseaseManager.SetTreatment(player, disease, true);
				RefreshIndicator(player);
				timer.In(delay, () =>
				{
					diseaseManager.SetTreatment(player, disease, false);
					RefreshIndicator(player);
				});
				if (diseaseManager.UpdateInfectedTime(player, disease, amount))
				{
					diseaseManager.SetActiveTick(player, disease, true);
					RecoverPlayer(player, disease);
				}
				StatusMessage(player, disease, Lang("StatusTreated", player.UserIDString, Color(disease.name, config.DiseaseNameColor)));
				callback?.Invoke();
			}
		}

		private void StatusMessage(BasePlayer player, Disease disease, string message)
		{
			if (config.ShowStatusMessages)
				player.ChatMessage(message);
		}

		private void DoSymptomEffects(BasePlayer player, Disease disease)
		{
			foreach (SymptomEffect effect in disease.symptomEffects)
			{
				effectManager.PlayEffect(player, effect.shortPrefabName, effect.LocalOnly);
			}
		}

		private void DoSymptomScreenEffects(BasePlayer player, Disease disease)
		{
			foreach (SymptomScreenEffect effect in disease.symptomScreenEffects)
			{
				effectManager.ShowScreenEffect(player, effect.shortPrefabName, effect.materialName, effect.opacity, effect.duration, effect.fadeOut);
			}
		}

		private void DoSymptomDamage(BasePlayer player, Disease disease)
		{
			foreach (SymptomDamage damage in disease.damageValues)
			{
				float value = damage.Value();
				switch (damage.stat)
				{
					case RustStat.health:
						player.Hurt(value, Rust.DamageType.Poison);
						break;
					case RustStat.bleeding:
						player.metabolism.ApplyChange(MetabolismAttribute.Type.Bleeding, value, 1);
						break;
					case RustStat.calories:
						player.metabolism.ApplyChange(MetabolismAttribute.Type.Calories, -value, 1);
						break;
					case RustStat.hydration:
						player.metabolism.ApplyChange(MetabolismAttribute.Type.Hydration, -value, 1);
						break;
					case RustStat.oxygen:
						player.metabolism.oxygen.value -= value;
						break;
					case RustStat.radiation:
						player.metabolism.ApplyChange(MetabolismAttribute.Type.Radiation, value, 1);
						break;
					case RustStat.temperature:
						player.metabolism.temperature.value += value;
						break;
					case RustStat.poison:
						player.metabolism.ApplyChange(MetabolismAttribute.Type.Poison, value, 1);
						break;
				}
			}
		}

		private bool WasCauseOfDeath(BasePlayer player, HitInfo info, Disease disease)
		{
			if (!diseaseManager.HasDisease(player, disease))
				return false;
			Rust.DamageType cause = info.damageTypes.GetMajorityDamageType();
			foreach (SymptomDamage damage in disease.damageValues)
				if (cause == damage.GetDamageType())
					return true;
			return false;
		}

		private float GetSpreadChance(BasePlayer player, Disease disease)
		{
			foreach (Item item in player.inventory.containerWear.itemList)
			{
				if (config.FaceCoveringItems.Contains(item.info.shortname))
					return disease.spreadChanceCovered;
			}
			return disease.spreadChanceUncovered;
		}

		private bool CompareStat(BasePlayer player, string statname, float value, Func<float, float, bool> comparison)
		{
			switch (statname)
			{
				case RustStat.health:
					return comparison(player.health, value);
				case RustStat.bleeding:
					return comparison(player.metabolism.bleeding.value, value);
				case RustStat.calories:
					return comparison(player.metabolism.calories.value, value);
				case RustStat.hydration:
					return comparison(player.metabolism.hydration.value, value);
				case RustStat.oxygen:
					return comparison(player.metabolism.oxygen.value, value);
				case RustStat.radiation:
					return comparison(player.metabolism.radiation_level.value, value);
				case RustStat.temperature:
					return comparison(player.metabolism.temperature.value, value);
				case RustStat.poison:
					return comparison(player.metabolism.poison.value, value);
			}
			return false;
		}

		private string AsTitle(string str) => str.ToLower().TitleCase();

		#endregion

		#region Data Structures
		class DoubleDictionary<TkeyA, TkeyB, Tvalue>
		{
			private Dictionary<TkeyA, Dictionary<TkeyB, Tvalue>> a2b;
			private Dictionary<TkeyB, Dictionary<TkeyA, Tvalue>> b2a;

			public DoubleDictionary()
			{
				a2b = new Dictionary<TkeyA, Dictionary<TkeyB, Tvalue>>();
				b2a = new Dictionary<TkeyB, Dictionary<TkeyA, Tvalue>>();
			}

			public void Set(TkeyA keyA, TkeyB keyB, Tvalue value)
			{
				if (!a2b.ContainsKey(keyA))
					a2b.Add(keyA, new Dictionary<TkeyB, Tvalue>());
				if (!b2a.ContainsKey(keyB))
					b2a.Add(keyB, new Dictionary<TkeyA, Tvalue>());
				if (!a2b[keyA].ContainsKey(keyB))
					a2b[keyA].Add(keyB, value);
				else
					a2b[keyA][keyB] = value;
				if (!b2a[keyB].ContainsKey(keyA))
					b2a[keyB].Add(keyA, value);
				else
					b2a[keyB][keyA] = value;
			}

			public Tvalue Get(TkeyA keyA, TkeyB keyB)
			{
				if (a2b.ContainsKey(keyA) && a2b[keyA].ContainsKey(keyB))
					return a2b[keyA][keyB];
				return default(Tvalue);
			}

			public Dictionary<TkeyB, Tvalue> Get(TkeyA keyA)
			{
				if (a2b.ContainsKey(keyA))
					return a2b[keyA];
				return new Dictionary<TkeyB, Tvalue>();
			}

			public Dictionary<TkeyA, Tvalue> Get(TkeyB keyB)
			{
				if (b2a.ContainsKey(keyB))
					return b2a[keyB];
				return new Dictionary<TkeyA, Tvalue>();
			}

			public bool ContainsKey(TkeyA keyA, TkeyB keyB)
			{
				return a2b.ContainsKey(keyA) && a2b[keyA].ContainsKey(keyB);
			}

			public void Delete(TkeyA keyA, TkeyB keyB)
			{
				if (a2b.ContainsKey(keyA) && a2b[keyA].ContainsKey(keyB))
					a2b[keyA].Remove(keyB);
				if (b2a.ContainsKey(keyB) && b2a[keyB].ContainsKey(keyA))
					b2a[keyB].Remove(keyA);
			}

			public void Delete(TkeyA keyA)
			{
				foreach (TkeyB keyB in b2a.Keys)
					if (b2a[keyB].ContainsKey(keyA))
						b2a[keyB].Remove(keyA);
				if (a2b.ContainsKey(keyA))
					a2b.Remove(keyA);
			}

			public void Delete(TkeyB keyB)
			{
				foreach (TkeyA keyA in a2b.Keys)
					if (a2b[keyA].ContainsKey(keyB))
						a2b[keyA].Remove(keyB);
				if (b2a.ContainsKey(keyB))
					b2a.Remove(keyB);
			}
		}

		class LookupList<TOne, TTwo>
		{
			private Dictionary<TOne, List<TTwo>> a2b;
			private Dictionary<TTwo, List<TOne>> b2a;

			public LookupList()
			{
				a2b = new Dictionary<TOne, List<TTwo>>();
				b2a = new Dictionary<TTwo, List<TOne>>();
			}

			public int Count()
			{
				return a2b.Count();
			}

			public void Add(TOne a, TTwo b)
			{
				if (!a2b.ContainsKey(a))
					a2b.Add(a, new List<TTwo>());
				if (!b2a.ContainsKey(b))
					b2a.Add(b, new List<TOne>());
				if (!a2b[a].Contains(b))
					a2b[a].Add(b);
				if (!b2a[b].Contains(a))
					b2a[b].Add(a);
			}

			public void AddAll(TOne[] listA, TTwo b)
			{
				foreach (TOne a in listA)
					Add(a, b);
			}

			public void AddAll(TTwo[] listB, TOne a)
			{
				foreach (TTwo b in listB)
					Add(a, b);
			}

			public void Remove(TOne a)
			{
				TTwo[] depends = Get(a).ToArray();
				foreach (TTwo b in depends)
					Remove(a, b);
			}

			public void Remove(TTwo b)
			{
				TOne[] depends = Get(b).ToArray();
				foreach (TOne a in depends)
					Remove(a, b);
			}

			public void Remove(TOne a, TTwo b)
			{
				if (a2b.ContainsKey(a) && a2b[a].Contains(b))
				{
					a2b[a].Remove(b);
					if (a2b[a].Count() <= 0)
						a2b.Remove(a);
				}

				if (b2a.ContainsKey(b) && b2a[b].Contains(a))
				{
					b2a[b].Remove(a);
					if (b2a[b].Count() <= 0)
						b2a.Remove(b);
				}
			}

			public List<TTwo> Get(TOne a)
			{
				if (a2b.ContainsKey(a))
					return a2b[a];
				return new List<TTwo>();
			}

			public List<TOne> Get(TTwo b)
			{
				if (b2a.ContainsKey(b))
					return b2a[b];
				return new List<TOne>();
			}

			public bool ContainsKey(TOne a)
			{
				return a2b.ContainsKey(a);
			}

			public bool ContainsKey(TTwo b)
			{
				return b2a.ContainsKey(b);
			}

			public bool Contains(TOne a, TTwo b)
			{
				return a2b.ContainsKey(a) && a2b[a].Contains(b);
			}

			public bool Contains(TTwo b, TOne a)
			{
				return b2a.ContainsKey(b) && b2a[b].Contains(a);
			}
		}
		#endregion

		#region Models
		class RustStat
		{
			public const string health = "health";
			public const string bleeding = "bleeding";
			public const string calories = "calories";
			public const string hydration = "hydration";
			public const string oxygen = "oxygen";
			public const string radiation = "radiation";
			public const string temperature = "temperature";
			public const string poison = "poison";
		}

		class SymptomEffect
		{
			[JsonProperty(PropertyName = "Asset path")]
			public string shortPrefabName;

			[JsonProperty(PropertyName = "Local only")]
			public bool LocalOnly = false;
		}
		class SymptomScreenEffect
		{
			[JsonProperty(PropertyName = "Sprite asset path")]
			public string shortPrefabName = "assets/content/textures/generic/fulltransparent.tga";
			[JsonProperty(PropertyName = "Opacity (0-1.0)")]
			public float opacity;
			[JsonProperty(PropertyName = "Duration (seconds)")]
			public float duration;
			[JsonProperty(PropertyName = "Fade out (seconds)")]
			public float fadeOut;
			[JsonProperty(PropertyName = "Material asset path (optional)")]
			public string materialName = "";
		}

		class SpreaderEntity
		{
			[JsonProperty(PropertyName = "Short prefab name")]
			public string shortPrefabName;
			[JsonProperty(PropertyName = "Infection chance on hit (0-100)")]
			public float infectionChanceOnHit;
		}

		class InfectionItem : ItemPrefab
		{
			[JsonProperty(PropertyName = "Infection chance on consumption (0-100)")]
			public float infectionChanceOnConsumption;
		}

		class TreatmentItem : ItemPrefab
		{
			[JsonProperty(PropertyName = "Min infection time decreased (seconds)")]
			public int minTimeDecrease;
			[JsonProperty(PropertyName = "Max infection time decreased (seconds)")]
			public int maxTimeDecrease;
			[JsonProperty(PropertyName = "Delay between reusing treament (seconds)")]
			public int treatmentDelay;

			public int Value() => UnityEngine.Random.Range(minTimeDecrease, maxTimeDecrease);
		}

		class CureItem : ItemPrefab
		{
			[JsonProperty(PropertyName = "Cure chance on consumption (0-100)")]
			public float cureChanceOnConsumption;
		}

		abstract class ItemPrefab
		{
			[JsonProperty(PropertyName = "Short prefab name")]
			public string shortPrefabName;
		}

		class SymptomDamage
		{
			[JsonProperty(PropertyName = "Stat name")]
			public string stat;
			[JsonProperty(PropertyName = "Min damage")]
			public float minValue;
			[JsonProperty(PropertyName = "Max damage")]
			public float maxValue;
			public float Value() => UnityEngine.Random.Range(minValue, maxValue);
			public Rust.DamageType GetDamageType()
			{
				switch (stat)
				{
					case "health":
						return Rust.DamageType.Poison;
					case "bleeding":
						return Rust.DamageType.Bleeding;
					case "calories":
						return Rust.DamageType.Hunger;
					case "hydration":
						return Rust.DamageType.Thirst;
					case "oxygen":
						return Rust.DamageType.Drowned;
					case "radiation":
						return Rust.DamageType.Radiation;
					case "temperature":
						return Rust.DamageType.Cold;
					case "poison":
						return Rust.DamageType.Poison;
					default:
						return Rust.DamageType.LAST;
				}
			}
		}

		class StatFilter
		{
			[JsonProperty(PropertyName = "Stat name")]
			public string stat;
			[JsonProperty(PropertyName = "Min value")]
			public float minValue;
			[JsonProperty(PropertyName = "Max value")]
			public float maxValue;
		}

		class OutbreakCondition
		{
			[JsonProperty(PropertyName = "Outbreaks enabled")]
			public bool enabled;
			[JsonProperty(PropertyName = "Outbreak chance (0-100)")]
			public float outbreakChance;
			[JsonProperty(PropertyName = "Time interval (seconds)")]
			public int timeInterval;
			[JsonProperty(PropertyName = "Only outbreak on players with the following stats")]
			public StatFilter[] statFilters;
		}

		class DiseaseStats
		{
			public class DiseaseStat
			{
				public string name;
				public int count;
			}

			public DiseaseStat activeInfected = new DiseaseStat { name = "Active players infected", count = 0 };
			public DiseaseStat sleepersInfected = new DiseaseStat { name = "Sleeping players infected", count = 0 };
			public DiseaseStat totalInfected = new DiseaseStat { name = "Total players infected", count = 0 };
			public DiseaseStat playersKilled = new DiseaseStat { name = "Total players killed", count = 0 };
			public DiseaseStat numOutbreaks = new DiseaseStat { name = "Number of outbreaks", count = 0 };
			public DiseaseStat infectedFromSpreading = new DiseaseStat { name = "Players infected from spreading", count = 0 };
			public DiseaseStat infectedFromEntity = new DiseaseStat { name = "Players infected from entities", count = 0 };
			public DiseaseStat infectedFromItem = new DiseaseStat { name = "Players infected from items", count = 0 };
			public DiseaseStat infectedFromOutbreak = new DiseaseStat { name = "Players infected from outbreaks", count = 0 };
			public DiseaseStat infectedFromCommand = new DiseaseStat { name = "Players infected from commands", count = 0 };
			public DiseaseStat timesTreatedFromItem = new DiseaseStat { name = "Times treated by items", count = 0 };
			public DiseaseStat timesCuredFromItem = new DiseaseStat { name = "Times cured by items", count = 0 };
		}

		#endregion

		#region Classes
		class Disease
		{
			[JsonProperty(PropertyName = "Disease name")]
			public string name { get; set; } = "";

			[JsonProperty(PropertyName = "Description")]
			public string info { get; set; } = "No information on this disease.";

			[JsonProperty(PropertyName = "Min time immune after recovery (seconds)")]
			public int minImmunityTime { get; set; } = 0;

			[JsonProperty(PropertyName = "Max time immune after recovery (seconds)")]
			public int maxImmunityTime { get; set; } = 0;

			[JsonProperty(PropertyName = "Min time infected (seconds)")]
			public int minInfectionTime { get; set; } = 0;

			[JsonProperty(PropertyName = "Max time infected (seconds)")]
			public int maxInfectionTime { get; set; } = 0;

			[JsonProperty(PropertyName = "Min time between symptoms (seconds)")]
			public int minSpreadTime { get; set; } = 0;

			[JsonProperty(PropertyName = "Max time between symptoms (seconds)")]
			public int maxSpreadTime { get; set; } = 0;

			[JsonProperty(PropertyName = "Spread distance (4.0 = 1 foundation)")]
			public float spreadDistance { get; set; } = 0;

			[JsonProperty(PropertyName = "Spread chance with mask (0-100)")]
			public float spreadChanceCovered { get; set; } = 0;

			[JsonProperty(PropertyName = "Spread chance without mask (0-100)")]
			public float spreadChanceUncovered { get; set; } = 0;

			[JsonProperty(PropertyName = "Symptom damage effects")]
			public SymptomDamage[] damageValues { get; set; } = new SymptomDamage[] { };

			[JsonProperty(PropertyName = "Items that cause infection on consumption")]
			public InfectionItem[] infectionItems { get; set; } = new InfectionItem[] { };

			[JsonProperty(PropertyName = "Items that cure on consumption")]
			public CureItem[] cureItems { get; set; } = new CureItem[] { };

			[JsonProperty(PropertyName = "Items that reduce infection time on consumption")]
			public TreatmentItem[] treatmentItems { get; set; } = new TreatmentItem[] { };

			[JsonProperty(PropertyName = "Entities that cause infection on hit")]
			public SpreaderEntity[] spreaderEntities { get; set; } = new SpreaderEntity[] { };

			[JsonProperty(PropertyName = "Random outbreak settings")]
			public OutbreakCondition outbreakConditions { get; set; } = new OutbreakCondition
			{
				enabled = false,
				outbreakChance = 0,
				timeInterval = 300,
				statFilters = new StatFilter[] { }
			};

			[JsonProperty(PropertyName = "Symptom effects")]
			public SymptomEffect[] symptomEffects { get; set; } = new SymptomEffect[] { };

			[JsonProperty(PropertyName = "Symptom screen effects")]
			public SymptomScreenEffect[] symptomScreenEffects { get; set; } = new SymptomScreenEffect[] { };

			[JsonProperty(PropertyName = "Version")]
			public VersionNumber version { get; set; } = new VersionNumber(0, 0, 0);

			public int SpreadTime() => UnityEngine.Random.Range(minSpreadTime, maxSpreadTime);

			public int InfectedTime() => UnityEngine.Random.Range(minInfectionTime, maxInfectionTime);

			public int ImmunityTime() => UnityEngine.Random.Range(minImmunityTime, maxImmunityTime);

			#region File IO
			public static Disease GenerateDefault()
			{
				Disease disease = new Disease
				{
					name = "Norovirus",
					info = "Highly contagious disease that causes vomitting and is caused by eating rotten food. Can be treated with tea and cured with pills.",
					minImmunityTime = 180,
					maxImmunityTime = 600,
					minInfectionTime = 180,
					maxInfectionTime = 600,
					minSpreadTime = 5,
					maxSpreadTime = 45,
					spreadDistance = 2f,
					spreadChanceCovered = 0,
					spreadChanceUncovered = 85,
					damageValues = new SymptomDamage[]
					{
						new SymptomDamage { stat=RustStat.health , minValue=1f, maxValue=5f},
						new SymptomDamage { stat=RustStat.calories , minValue=100f, maxValue=125f},
						new SymptomDamage { stat=RustStat.hydration , minValue=50f, maxValue=85f}
					},
					infectionItems = new InfectionItem[]
					{
						new InfectionItem { shortPrefabName="chicken.raw", infectionChanceOnConsumption=10f },
						new InfectionItem { shortPrefabName="chicken.spoiled", infectionChanceOnConsumption=85f },
						new InfectionItem { shortPrefabName="humanmeat.raw", infectionChanceOnConsumption=10f },
						new InfectionItem { shortPrefabName="humanmeat.spoiled", infectionChanceOnConsumption=85f }
					},
					cureItems = new CureItem[]
					{
						new CureItem { shortPrefabName="antiradpills", cureChanceOnConsumption=50f }
					},
					treatmentItems = new TreatmentItem[]
					{
						new TreatmentItem { shortPrefabName="healingtea", minTimeDecrease=5, maxTimeDecrease=10, treatmentDelay=10 },
						new TreatmentItem { shortPrefabName="healingtea.advanced", minTimeDecrease=10, maxTimeDecrease=40, treatmentDelay=40 },
						new TreatmentItem { shortPrefabName="healingtea.pure", minTimeDecrease=40, maxTimeDecrease=160, treatmentDelay=160 }
					},
					spreaderEntities = new SpreaderEntity[]
					{
						new SpreaderEntity { shortPrefabName="boar", infectionChanceOnHit=5f }
					},
					outbreakConditions = new OutbreakCondition
					{
						enabled = true,
						outbreakChance = 25f,
						timeInterval = 300,
						statFilters = new StatFilter[] {
							new StatFilter { stat=RustStat.health, minValue=0, maxValue=50 }
						}
					},
					symptomEffects = new SymptomEffect[]
					{
						new SymptomEffect { shortPrefabName="assets/bundled/prefabs/fx/gestures/drink_vomit.prefab" },
						new SymptomEffect { shortPrefabName="assets/bundled/prefabs/fx/water/midair_splash.prefab" },
						new SymptomEffect { shortPrefabName="assets/prefabs/weapons/cake/effects/strike_screenshake.prefab", LocalOnly=true }
					},
					symptomScreenEffects = new SymptomScreenEffect[]
					{
						new SymptomScreenEffect { shortPrefabName="assets/content/ui/overlay_poisoned.png", duration=1f, opacity=1f, fadeOut=1f }
					},
					version = PLUGIN.Version
				};
				return disease;
			}

			public static void SaveToFile(Disease disease)
			{
				Interface.Oxide.DataFileSystem.WriteObject("Diseases/" + disease.name, disease);
			}

			public static Disease ReadFromFile(string diseaseName)
			{
				try
				{
					Disease disease = Interface.Oxide.DataFileSystem.ReadObject<Disease>("Diseases/" + diseaseName);
					if (disease.name == null)
						disease.name = "null";
					if (PLUGIN.AsTitle(diseaseName) != PLUGIN.AsTitle(disease.name))
						PLUGIN.PrintWarning($"Disease file '{diseaseName}.json' does not match with disease name of '{disease.name}', the name '{diseaseName}' will be used instead.");
					disease.name = PLUGIN.AsTitle(diseaseName);
					if (disease.version.Major != PLUGIN.Version.Major || disease.version.Minor != PLUGIN.Version.Minor)
					{
						PLUGIN.PrintError($"The disease file '{disease.name}.json' is version {disease.version} but the plugin version is {PLUGIN.Version}. This disease cannot be loaded until it is updated.");
						return null;
					}
					else if (disease.version.Patch != PLUGIN.Version.Patch)
					{
						PLUGIN.PrintWarning($"Consider updating the 'Version' property in '{diseaseName}.json' from {disease.version} to {PLUGIN.Version}.");
					}
					return disease;
				}
				catch (Newtonsoft.Json.JsonSerializationException)
				{
					PLUGIN.PrintError($"The disease file '{diseaseName}.json' contains a syntax error. Please check to make sure it matches the syntax in 'Norovirus.json'.");
				}
				catch (Exception)
				{
					PLUGIN.PrintError($"The disease file '{diseaseName}.json' failed to load properly.");
				}
				return null;
			}
			#endregion
		}

		#endregion

		#region Manager
		class EffectManager
		{
			public readonly Dictionary<ulong, List<string>> ActiveEffects;

			private ulong idCount;

			public EffectManager()
			{
				ActiveEffects = new Dictionary<ulong, List<string>>();
				idCount = 0;
			}

			private void InitPlayer(ulong userid)
			{
				if (!ActiveEffects.ContainsKey(userid))
					ActiveEffects.Add(userid, new List<string>());
			}

			private void AddEffect(ulong userid, string effectString)
			{
				InitPlayer(userid);
				ActiveEffects[userid].Add(effectString + idCount);
				idCount++;
			}

			private void RemoveEffect(ulong userid, string effectString)
			{
				InitPlayer(userid);
				if (ActiveEffects[userid].Contains(effectString))
					ActiveEffects[userid].Remove(effectString);
			}

			public void PlayEffect(BasePlayer player, string effectString, bool local)
			{
				var effect = new Effect(effectString, player, 0, Vector3.zero, Vector3.forward);
				if (local)
					EffectNetwork.Send(effect, player.net.connection);
				else
					EffectNetwork.Send(effect);
			}

			public void ClearAllEffects(BasePlayer player)
			{
				InitPlayer(player.userID);
				foreach (string activeEffect in ActiveEffects[player.userID])
				{
					CuiHelper.DestroyUi(player, activeEffect);
					RemoveEffect(player.userID, activeEffect);
				}
			}

			public void ShowScreenEffect(BasePlayer player, string assetStringPath, string materialStringPath, float opacity = 1, float duration = 1, float fadeOut = 1)
			{
				CuiElementContainer container = new CuiElementContainer();
				ulong thisId = idCount;
				container.Add(new CuiElement
				{
					Name = assetStringPath + thisId,
					Parent = "Hud",
					FadeOut = fadeOut,
					Components =
					{
						new CuiRawImageComponent {
							Sprite = assetStringPath,
							Material = materialStringPath,
							Color = "1 1 1 " + opacity
						},
						new CuiRectTransformComponent
						{
							AnchorMin = "0 0",
							AnchorMax = "1 1"
						}
					},
				});

				if (debug)
					PLUGIN.Puts("Started screen effect for " + assetStringPath + thisId);
				CuiHelper.AddUi(player, container);
				AddEffect(player.userID, assetStringPath);
				PLUGIN.timer.In(duration, () =>
				{
					CuiHelper.DestroyUi(player, assetStringPath + thisId);
					if (debug)
						PLUGIN.Puts("Cleared screen effect for " + assetStringPath + thisId);
					RemoveEffect(player.userID, assetStringPath + thisId);
				});

			}
		}

		class DiseaseManager
		{
			public readonly LookupList<BasePlayer, string> InfectedPlayers;  /* playerid, diseasename */
			public readonly Dictionary<string, Disease> LoadedDiseases; /*diseasename, disease*/
			private DoubleDictionary<ulong, string, int> InfectedTimes;
			private DoubleDictionary<ulong, string, int> NextGoalTimes;
			private DoubleDictionary<ulong, string, bool> IsImmune;
			private DoubleDictionary<ulong, string, bool> IsTreated;
			private DoubleDictionary<ulong, string, bool> ActiveTick;
			private Dictionary<ulong, bool> SafeZone;
			public readonly DoubleDictionary<string, string, CureItem> ItemsThatCure;
			public readonly DoubleDictionary<string, string, InfectionItem> ItemsThatInfect;
			public readonly DoubleDictionary<string, string, TreatmentItem> ItemsThatTreat;
			public readonly DoubleDictionary<string, string, SpreaderEntity> EntitiesThatInfect;


			public DiseaseManager()
			{
				InfectedPlayers = new LookupList<BasePlayer, string>();
				LoadedDiseases = new Dictionary<string, Disease>();
				InfectedTimes = new DoubleDictionary<ulong, string, int>();
				NextGoalTimes = new DoubleDictionary<ulong, string, int>();
				IsImmune = new DoubleDictionary<ulong, string, bool>();
				IsTreated = new DoubleDictionary<ulong, string, bool>();
				ActiveTick = new DoubleDictionary<ulong, string, bool>();
				ItemsThatCure = new DoubleDictionary<string, string, CureItem>();
				ItemsThatInfect = new DoubleDictionary<string, string, InfectionItem>();
				ItemsThatTreat = new DoubleDictionary<string, string, TreatmentItem>();
				EntitiesThatInfect = new DoubleDictionary<string, string, SpreaderEntity>();
			}

			public void LoadAllDiseases(Action callback = null)
			{
				List<string> files = new List<string>();
				try
				{
					files = Interface.Oxide.DataFileSystem.GetFiles("Diseases/").ToList();
				}
				catch (Exception) {
					PLUGIN.PrintError("Failed to read files from /Diseases directory");
				};

				/* Generate Default Disease */
				if (PLUGIN.config.GenerateDefaultDisease)
				{
					Disease disease = Disease.GenerateDefault();
					LoadDisease(disease);
					Disease.SaveToFile(disease);
				}
				foreach (string filename in files)
				{
					string[] split = filename.Split('/');
					string diseaseName = split[split.Length - 1].Replace(".json", "");
					foreach (string nameInConfig in PLUGIN.config.DefaultLoadedDiseases)
					{
						
						if (PLUGIN.AsTitle(nameInConfig) == PLUGIN.AsTitle(diseaseName))
						{
							Disease disease = Disease.ReadFromFile(diseaseName);
							if (disease != null)
							{
								LoadDisease(disease);
								PLUGIN.Puts($"Loaded disease {disease.name}");
							}
								
						}
					}
				}
				callback?.Invoke();
			}

			public void LoadDisease(Disease disease, Action callback = null)
			{
				if (!LoadedDiseases.ContainsKey(disease.name))
				{
					LoadedDiseases.Add(disease.name, disease);
					LoadItems(disease);
					LoadEntities(disease);
					PLUGIN.diseaseStats.Add(disease.name, new DiseaseStats());

					if (debug)
						PLUGIN.Puts("Loaded " + disease.name);
					callback?.Invoke();
				}
			}

			public void UnloadDisease(string diseasename)
			{
				InfectedPlayers.Remove(diseasename);
				InfectedTimes.Delete(diseasename);
				NextGoalTimes.Delete(diseasename);
				IsImmune.Delete(diseasename);
				PLUGIN.diseaseStats.Remove(diseasename);
				LoadedDiseases.Remove(diseasename);
				if (debug)
					PLUGIN.Puts("Unloaded " + diseasename);
			}

			public Disease GetDisease(string diseasename)
			{
				if (LoadedDiseases.ContainsKey(diseasename))
					return LoadedDiseases[diseasename];
				return null;
			}

			// Called when a player is to be infected with the given disease
			public bool AddInfected(BasePlayer player, Disease disease)
			{
				if (CanInfect(player, disease))
				{
					InfectedPlayers.Add(player, disease.name);
					InfectedTimes.Set(player.userID, disease.name, 0);
					NextGoalTimes.Set(player.userID, disease.name, disease.InfectedTime());
					IsImmune.Set(player.userID, disease.name, false);
					if (debug)
						PLUGIN.Puts("Infected: " + player.displayName + " with " + disease.name + " for " + NextGoalTimes.Get(player.userID, disease.name) + " seconds");
					return true;
				}
				return false;
			}

			// Called when a player is to be no longer infected with the given disease
			public bool RemoveInfected(BasePlayer player, Disease disease)
			{
				if (InfectedPlayers.ContainsKey(player))
				{
					InfectedPlayers.Remove(player, disease.name);
					InfectedTimes.Delete(player.userID);
					NextGoalTimes.Delete(player.userID);
					IsImmune.Delete(player.userID);
					if (debug)
						PLUGIN.Puts("Uninfected: " + player.displayName + " from " + disease.name);
					return true;
				}
				return false;
			}

			public bool RemoveAllInfected(string diseasename)
			{
				if (InfectedPlayers.ContainsKey(diseasename))
				{
					InfectedPlayers.Remove(diseasename);
					if (debug)
						PLUGIN.Puts("Uninfected all of: " + diseasename);
					return true;
				}
				return false;
			}

			// Returns an iterable copy of all players infected by the disease
			public BasePlayer[] GetInfectedList(string diseasename)
			{
				return InfectedPlayers.Get(diseasename).ToArray();
			}

			// Returns an iterable copy of all diseases a player has
			public string[] GetPlayerInfections(BasePlayer player)
			{
				return InfectedPlayers.Get(player).ToArray();
			}

			public void SetActiveTick(BasePlayer player, Disease disease, bool value)
			{
				ActiveTick.Set(player.userID, disease.name, value);
			}

			public bool HasActiveTick(BasePlayer player, Disease disease)
			{
				if (ActiveTick.ContainsKey(player.userID, disease.name))
					return ActiveTick.Get(player.userID, disease.name);
				return false;
			}

			public bool CanInfect(BasePlayer player, Disease disease)
			{
				return player.IsValid() && player.IsConnected && PLUGIN.permission.UserHasPermission(player.UserIDString, PermissionUse) && !HasDisease(player, disease) && !HasImmunity(player, disease) && InfectedPlayers.Count() < PLUGIN.config.MaxInfectedEntities && !(player.InSafeZone() && PLUGIN.config.SafezoneInfectionSpread);
			}

			public bool HasDisease(BasePlayer player, Disease disease)
			{
				return InfectedPlayers.Contains(player, disease.name);
			}

			public bool HasImmunity(BasePlayer player, Disease disease)
			{
				return IsImmune.ContainsKey(player.userID, disease.name) && IsImmune.Get(player.userID, disease.name);
			}

			public void SetTreatment(BasePlayer player, Disease disease, bool value)
			{
				IsTreated.Set(player.userID, disease.name, value);
			}

			public bool HasTreatment(BasePlayer player, Disease disease)
			{
				return IsTreated.ContainsKey(player.userID, disease.name) && IsTreated.Get(player.userID, disease.name);
			}

			// Modifies the infected time for a user/disease combo with the modifier, returns true if goal is reached
			public bool UpdateInfectedTime(BasePlayer player, Disease disease, int modifier)
			{
				if (HasImmunity(player, disease)) return true;
				if (InfectedTimes.ContainsKey(player.userID, disease.name) && NextGoalTimes.ContainsKey(player.userID, disease.name) && LoadedDiseases.ContainsKey(disease.name))
				{
					InfectedTimes.Set(player.userID, disease.name, InfectedTimes.Get(player.userID, disease.name) + modifier);
					return InfectedTimes.Get(player.userID, disease.name) >= NextGoalTimes.Get(player.userID, disease.name);
				}
				return false;
			}

			public bool SetRecovery(BasePlayer player, Disease disease)
			{
				if (!HasImmunity(player, disease))
				{
					InfectedTimes.Set(player.userID, disease.name, 0);
					IsImmune.Set(player.userID, disease.name, true);
					return true;
				}
				return false;
			}

			private void LoadItems(Disease disease)
			{
				foreach (CureItem item in disease.cureItems)
					ItemsThatCure.Set(item.shortPrefabName, disease.name, item);
				foreach (InfectionItem item in disease.infectionItems)
					ItemsThatInfect.Set(item.shortPrefabName, disease.name, item);
				foreach (TreatmentItem item in disease.treatmentItems)
					ItemsThatTreat.Set(item.shortPrefabName, disease.name, item);
			}

			private void LoadEntities(Disease disease)
			{
				foreach (SpreaderEntity entity in disease.spreaderEntities)
				{
					if (debug)
						PLUGIN.Puts("Set " + entity.shortPrefabName + " " + disease.name);
					EntitiesThatInfect.Set(entity.shortPrefabName, disease.name, entity);
				}
			}

		}
		#endregion

		#region Commands
		private Dictionary<string, ChatCmd> commands = new Dictionary<string, ChatCmd>();
		void InitCommands()
		{
			commands.Add("help", new ChatCmd
			{
				perms = new List<string>() { PermissionUse },
				function = (IPlayer player, string command, string[] args) => { cmd_help(player, command, args); }
			});
			commands.Add("infect", new ChatCmd
			{
				usages = new List<string>() { "<player_name> <disease_name>" },
				perms = new List<string>() { PermissionAdmin },
				function = (IPlayer player, string command, string[] args) => { cmd_infect(player, command, args); }
			});
			commands.Add("cure", new ChatCmd
			{
				usages = new List<string>() { "<player_name> <disease_name>" },
				perms = new List<string>() { PermissionAdmin },
				function = (IPlayer player, string command, string[] args) => { cmd_cure(player, command, args); }
			});
			commands.Add("outbreak", new ChatCmd
			{
				usages = new List<string>() { "<player_name> <disease_name>", "<player_name> <disease_name> <number>" },
				perms = new List<string>() { PermissionAdmin },
				function = (IPlayer player, string command, string[] args) => { cmd_outbreak(player, command, args); }
			});
			commands.Add("eradicate", new ChatCmd
			{
				usages = new List<string>() { "<disease_name>" },
				perms = new List<string>() { PermissionAdmin },
				function = (IPlayer player, string command, string[] args) => { cmd_eradicate(player, command, args); }
			});
			commands.Add("list", new ChatCmd
			{
				perms = new List<string>() { PermissionUse, PermissionAdmin },
				function = (IPlayer player, string command, string[] args) => { cmd_list(player, command, args); }
			});
			commands.Add("info", new ChatCmd
			{
				usages = new List<string>() { "<disease_name>" },
				perms = new List<string>() { PermissionUse, PermissionAdmin },
				function = (IPlayer player, string command, string[] args) => { cmd_info(player, command, args); }
			});
			commands.Add("stats", new ChatCmd
			{
				usages = new List<string>() { "<disease_name>" },
				perms = new List<string>() { PermissionUse, PermissionAdmin },
				function = (IPlayer player, string command, string[] args) => { cmd_stats(player, command, args); }
			});
			commands.Add("config", new ChatCmd
			{
				usages = new List<string>() { "<disease_name>" },
				perms = new List<string>() { PermissionAdmin },
				function = (IPlayer player, string command, string[] args) => { cmd_config(player, command, args); }
			});
			commands.Add("load", new ChatCmd
			{
				usages = new List<string>() { "<disease_name>" },
				perms = new List<string>() { PermissionAdmin },
				function = (IPlayer player, string command, string[] args) => { cmd_load(player, command, args); }
			});
			commands.Add("unload", new ChatCmd
			{
				usages = new List<string>() { "<disease_name>" },
				perms = new List<string>() { PermissionAdmin },
				function = (IPlayer player, string command, string[] args) => { cmd_unload(player, command, args); }
			});
		}

		class ChatCmd
		{
			public string prefix = "";
			public List<string> usages = new List<string>();
			public List<string> perms = new List<string>();
			public Action<IPlayer, string, string[]> function;

			public bool HasPerms(string id)
			{
				foreach (string perm in perms)
					if (PLUGIN.permission.UserHasPermission(id, perm))
						return true;
				return false;
			}

			public string Usage(string prefix, string usg)
			{
				prefix = PLUGIN.Color(prefix, "orange");
				usg = PLUGIN.Color(usg, "#00ffff");
				return $"{prefix} {usg}";
			}
		}

		[Command("disease")]
		private void cmd_disease(IPlayer player, string command, string[] args)
		{
			if (args.Length == 0)
			{
				/* Help command */
			}
			if (args.Length >= 1)
			{
				string prefix = args[0];
				if (commands.ContainsKey(prefix))
				{
					args = args.Skip(1).ToArray();
					ChatCmd cmd = commands[prefix];
					if (cmd.HasPerms(player.Id))
					{
						try
						{
							commands[prefix].function(player, command, args);
							return;
						}
						catch { }
					}
				}
			}
			player.Reply(Lang("Invalid", player.Id));
		}

		[Command("disease.help"), Permission(PermissionAdmin)]
		private void cmd_help(IPlayer player, string command, string[] args)
		{
			string size = "16";
			string cmdList = Size(Color("Diseases", "green"), size) + "\n";
			foreach (string prefix in commands.Keys)
			{
				ChatCmd cmd = commands[prefix];
				if (cmd.HasPerms(player.Id))
				{
					foreach (string usg in cmd.usages)
					{
						cmdList += Color($"/disease {cmd.Usage(prefix, usg)}\n", "yellow");
					}
				}
			}
			player.Reply(cmdList);
		}

		[Command("infect"), Permission(PermissionAdmin)]
		private void cmd_infect(IPlayer player, string command, string[] args)
		{
			string playerName = args[0];
			string diseaseName = AsTitle(args[1]);
			BasePlayer target = BasePlayer.Find(playerName);
			Disease disease = diseaseManager.GetDisease(diseaseName);
			if (!target.IsValid())
			{
				player.Reply(Lang("NoPlayer", player.Id, playerName));
			}
			else if (disease == null)
			{
				player.Reply(Lang("NoExist", player.Id, diseaseName));
			}
			else
			{
				if (diseaseManager.CanInfect(target, disease))
				{
					InfectPlayer(target, disease, () =>
					{
						diseaseStats[disease.name].infectedFromCommand.count += 1; ;
					});
					player.Reply(Lang("Infect", player.Id, target.displayName, disease.name));
				}
				else
				{
					player.Reply(Lang("NoInfect", player.Id, target.displayName, disease.name));
				}
			}
		}

		[Command("cure"), Permission(PermissionAdmin)]
		private void cmd_cure(IPlayer player, string command, string[] args)
		{
			string playerName = args[0];
			string diseaseName = AsTitle(args[1]);
			BasePlayer target = BasePlayer.Find(playerName);
			Disease disease = diseaseManager.GetDisease(diseaseName);
			if (!target.IsValid())
			{
				player.Reply(Lang("NoPlayer", player.Id, playerName));
			}
			else if (disease == null)
			{
				player.Reply(Lang("NoExist", player.Id, diseaseName));
			}
			else
			{
				if (diseaseManager.HasDisease(target, disease))
				{
					CurePlayer(target, disease);
					player.Reply(Lang("Cure", player.Id, target.displayName, disease.name));
				}
				else
				{
					player.Reply(Lang("NoCure", player.Id, target.displayName, disease.name));
				}
			}
		}

		[Command("outbreak"), Permission(PermissionAdmin)]
		private void cmd_outbreak(IPlayer player, string command, string[] args)
		{
			string diseaseName = AsTitle(args[0]);
			int tries = args.Length >= 2 ? int.Parse(args[1]) : 1;
			Disease disease = diseaseManager.GetDisease(diseaseName);
			if (disease == null)
			{
				player.Reply(Lang("NoExist", player.Id, diseaseName));
			}
			else
			{
				int infected = Outbreak(disease, tries);
				player.Reply(Lang("Outbreak", player.Id, disease.name, infected));
			}
		}

		[Command("eradicate"), Permission(PermissionAdmin)]
		private void cmd_eradicate(IPlayer player, string command, string[] args)
		{
			string diseaseName = AsTitle(args[0]);
			Disease disease = diseaseManager.GetDisease(diseaseName);
			if (disease == null)
			{
				player.Reply(Lang("NoExist", player.Id, diseaseName));
			}
			else if (diseaseManager.RemoveAllInfected(diseaseName))
			{
				player.Reply(Lang("Eradicate", player.Id, disease.name));
			}
			else
			{
				player.Reply(Lang("NoEradicate", player.Id, disease.name));
			}
		}

		[Command("disease.load"), Permission(PermissionAdmin)]
		private void cmd_load(IPlayer player, string command, string[] args)
		{
			string diseaseName = AsTitle(args[0]);
			if (!diseaseManager.LoadedDiseases.ContainsKey(diseaseName))
			{
				Disease disease = Disease.ReadFromFile(diseaseName);
				if (disease != null)
				{
					diseaseManager.LoadDisease(disease, () =>
					{
						player.Reply(Lang("Load", player.Id, disease.name));
					});
				}
				return;
			}
			player.Reply(Lang("NoLoad", player.Id, diseaseName));
		}

		[Command("disease.unload"), Permission(PermissionAdmin)]
		private void cmd_unload(IPlayer player, string command, string[] args)
		{
			string diseaseName = AsTitle(args[0]);
			if (diseaseManager.LoadedDiseases.ContainsKey(diseaseName))
			{
				diseaseManager.UnloadDisease(diseaseName);
				player.Reply(Lang("Unload", player.Id, diseaseName));
				return;
			}
			player.Reply(Lang("NoUnload", player.Id, diseaseName));
		}

		[Command("disease.list"), Permission(PermissionAdmin)]
		private void cmd_list(IPlayer player, string command, string[] args)
		{
			player.Reply(Lang("List", player.Id, diseaseManager.LoadedDiseases.Keys.ToSentence()));
		}

		[Command("disease.info"), Permission(PermissionAdmin)]
		private void cmd_info(IPlayer player, string command, string[] args)
		{
			string diseaseName = AsTitle(args[0]);
			if (diseaseManager.LoadedDiseases.ContainsKey(diseaseName))
			{
				Disease disease = diseaseManager.GetDisease(diseaseName);
				player.Reply($"{Color(disease.name, config.DiseaseNameColor)}: {disease.info}");
				return;
			}
			else
			{
				player.Reply(Lang("NoExist", player.Id, diseaseName));
			}
		}

		[Command("disease.config"), Permission(PermissionAdmin)]
		private void cmd_config(IPlayer player, string command, string[] args)
		{
			string diseaseName = AsTitle(args[0]);
			if (diseaseManager.LoadedDiseases.ContainsKey(diseaseName))
			{
				Disease disease = diseaseManager.GetDisease(diseaseName);
				var jsonString = JsonConvert.SerializeObject(
				disease, Formatting.Indented,
				new JsonConverter[] { new StringEnumConverter() });
				player.Reply(jsonString);
			}
			else
			{
				player.Reply(Lang("NoExist", player.Id, diseaseName));
			}
		}

		[Command("disease.stats"), Permission(PermissionAdmin)]
		private void cmd_stats(IPlayer player, string command, string[] args)
		{
			string diseaseName = AsTitle(args[0]);
			if (diseaseManager.LoadedDiseases.ContainsKey(diseaseName))
			{
				DiseaseStats stats = diseaseStats[diseaseName];
				string entry = "{0}: " + Color("{1}", "yellow") + "\n";
				string message = $"{Color(diseaseName, config.DiseaseNameColor)}:\n";
				message += string.Format(entry, stats.activeInfected.name, stats.activeInfected.count);
				message += string.Format(entry, stats.sleepersInfected.name, stats.sleepersInfected.count);
				message += string.Format(entry, stats.totalInfected.name, stats.totalInfected.count);
				message += string.Format(entry, stats.playersKilled.name, stats.playersKilled.count);
				message += string.Format(entry, stats.numOutbreaks.name, stats.numOutbreaks.count);
				message += string.Format(entry, stats.infectedFromSpreading.name, stats.infectedFromSpreading.count);
				message += string.Format(entry, stats.infectedFromEntity.name, stats.infectedFromEntity.count);
				message += string.Format(entry, stats.infectedFromItem.name, stats.infectedFromItem.count);
				message += string.Format(entry, stats.infectedFromOutbreak.name, stats.infectedFromOutbreak.count);
				message += string.Format(entry, stats.infectedFromCommand.name, stats.infectedFromCommand.count);
				message += string.Format(entry, stats.timesTreatedFromItem.name, stats.timesTreatedFromItem.count);
				message += string.Format(entry, stats.timesCuredFromItem.name, stats.timesCuredFromItem.count);
				player.Reply(message);
				return;
			}
			else
			{
				player.Reply(Lang("NoExist", player.Id, diseaseName));
			}
		}
		#endregion

		#region Configuration

		private Configuration config;
		private class Configuration
		{
			[JsonProperty(PropertyName = "Generate default disease (true/false)")]
			public bool GenerateDefaultDisease = true;

			[JsonProperty(PropertyName = "Loaded diseases on startup")]
			public string[] DefaultLoadedDiseases { get; set; } = {
			  "Norovirus",
			};

			[JsonProperty(PropertyName = "Max number of infected entities")]
			public int MaxInfectedEntities { get; set; } = 100;

			[JsonProperty(PropertyName = "Disease name color in messages")]
			public string DiseaseNameColor { get; set; } = "green";

			[JsonProperty(PropertyName = "Face covering items")]
			public string[] FaceCoveringItems { get; set; } = {
			  "mask.bandana",
			  "burlap.headwrap",
			  "clatter.helmet",
			  "coffeecan.helmet",
			  "heavy.plate.helmet",
			  "hazmatsuit",
			  "hazmatsuit.spacesuit",
			  "metal.facemask",
			  "halloween.surgeonsuit"
			};

			[JsonProperty(PropertyName = "Show status chat messages")]
			public bool ShowStatusMessages { get; set; } = true;

			[JsonProperty(PropertyName = "Infections can spread in safe zones")]
			public bool SafezoneInfectionSpread { get; set; } = true;

			[JsonProperty(PropertyName = "Broadcast disease death messages")]
			public bool BroadcastDeathMessages { get; set; } = false;

			[JsonProperty(PropertyName = "Broadcast outbreaks")]
			public bool BroadcastOutbreaks { get; set; } = false;

			[JsonProperty(PropertyName = "HUD indicator list")]
			public PositionUI IndicatorHUD { get; set; } = new PositionUI { AnchorMin = "0.9 0.6", AnchorMax = "0.985 0.9", EntryHeight = 0.1f, ImgAnchorMin = "0.04 0.1", ImgAnchorMax = "0.22 0.85", ImgShow = true };
		}

		private class PositionUI
		{
			[JsonProperty(PropertyName = "Anchor min")]
			public string AnchorMin;
			[JsonProperty(PropertyName = "Anchor max")]
			public string AnchorMax;
			[JsonProperty(PropertyName = "Entry height")]
			public float EntryHeight;
			[JsonProperty(PropertyName = "Anchor min (image)")]
			public string ImgAnchorMin;
			[JsonProperty(PropertyName = "Anchor max (image)")]
			public string ImgAnchorMax;
			[JsonProperty(PropertyName = "Show image (requires ImageLibrary)")]
			public bool ImgShow;
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();

			try
			{
				config = Config.ReadObject<Configuration>();
				if (config == null) throw new Exception();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig() => Config.WriteObject(config);

		protected override void LoadDefaultConfig() => config = new Configuration();
		#endregion

		#region Localization
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["StatusInfected"] = "You are infected with {0}",
				["StatusRecovered"] = "You have recovered from {0}",
				["StatusTreated"] = "You treated the {0} and will recover faster",
				["StatusCured"] = "You are not longer suffering from {0}",
				["StatusOutbreak"] = "An outbreak of {0} has started",
				["Infect"] = "Infected {0} with {1}",
				["NoInfect"] = "{0} is already infected with {1} or is immune",
				["Cure"] = "Cured {0} of {1}",
				["NoCure"] = "{0} is not infected with {0} or cannot be cured",
				["Outbreak"] = "Outbreak of {0} infected {1} players",
				["Eradicate"] = "Cured all players of {0}",
				["NoEradicate"] = "No players are infected with {0}",
				["Load"] = "Loaded disease {0}",
				["NoLoad"] = "Failed to load disease {0}, it is either already loaded or file is invalid",
				["Unload"] = "Unloaded disease {0}",
				["NoUnload"] = "Failed to unload disease {0}, it is not currently loaded",
				["List"] = "Loaded diseases: {0}",
				["NoExist"] = "The disease {0} is not currently loaded or doesn't exist on this server",
				["NoExist"] = "The disease {0} is not currently loaded or doesn't exist on this server",
				["NoPlayer"] = "There is no active player with the name {0}",
				["StatInfected"] = "Players infected: {0}",
				["StatInfectedTotal"] = "Total players infected: {0}",
				["StatKilled"] = "Total players killed: {0}",
				["Death"] = "{0} died from {1}",
				["Invalid"] = "Invalid command, try /disease help for a list of commands",
				["Treated"] = "Treated"
			}, this);
		}

		private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

		private string Color(string text, string colorCode)
		{
			return $"<color={colorCode}>{text}</color>";
		}

		private string Size(string text, string fontSize)
		{
			return $"<size={fontSize}>{text}</size>";
		}
		#endregion

		#region UI
		string COLOR_RED = "0.79 0.29 0.23 0.8";
		string COLOR_YELLOW = "0.79 0.59 0.23 0.8";

		void RefreshIndicator(BasePlayer player)
		{
			HideAllIndicators(player);
			CuiElementContainer container = new CuiElementContainer();
			container.Add(new CuiElement
			{
				Name = "diseasesIndicator",
				Parent = "Hud",
				Components =
				{
					new CuiImageComponent
					{
						Color = "0 0 0 0"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = config.IndicatorHUD.AnchorMin,
						AnchorMax = config.IndicatorHUD.AnchorMax
					}
				}
			});
			int index = 0;
			foreach (String diseasename in diseaseManager.GetPlayerInfections(player))
			{
				Disease disease = diseaseManager.GetDisease(diseasename);
				if (disease != null)
				{
					AddIndicator(container, player, disease, index);
					index++;
				}
			}
			CuiHelper.AddUi(player, container);
		}

		void AddIndicator(CuiElementContainer container, BasePlayer player, Disease disease, int index)
		{
			float entryHeight = config.IndicatorHUD.EntryHeight;
			float padding = 0.02f;
			float bottom = 1 - ((padding + entryHeight) * (index + 1));
			string color = diseaseManager.HasImmunity(player, disease) ? COLOR_YELLOW : COLOR_RED;
			string txtcolor = diseaseManager.HasTreatment(player, disease) ? "0 1 0 1" : "1 1 1 1";
			string text = disease.name.ToUpper();
			if (diseaseManager.HasTreatment(player, disease))
				text += $" ({Lang("Treated", player.UserIDString)})";
			container.Add(new CuiElement
			{
				Name = "diseasesIndicatorEntry" + index.ToString(),
				Parent = "diseasesIndicator",
				Components =
				{
					new CuiImageComponent
					{
						Color = color
					},
					new CuiRectTransformComponent
					{
						AnchorMin = CalcUI(0, bottom),
						AnchorMax = CalcUI(1, bottom+entryHeight)
					}
				}
			});
			if (config.IndicatorHUD.ImgShow && ImageLibrary)
			{
				container.Add(new CuiElement
				{
					Name = "diseasesIndicatorEntryImage" + index.ToString(),
					Parent = "diseasesIndicatorEntry" + index.ToString(),
					Components =
					{
					new CuiRawImageComponent
					{
						Png = ImageLibrary?.Call<string>("GetImage", "DiseaseIndicatorIcon"),
						Color = "0.5 0.5 0.5 1"
					},
					new CuiRectTransformComponent
					{
						AnchorMin = config.IndicatorHUD.ImgAnchorMin,
						AnchorMax = config.IndicatorHUD.ImgAnchorMax
					}
					}
				});
			}

			container.Add(new CuiElement
			{
				Name = "diseasesIndicatorEntryText" + index.ToString(),
				Parent = "diseasesIndicatorEntry" + index.ToString(),
				Components =
				{
					new CuiTextComponent
					{
						Text = text,
						Align = (config.IndicatorHUD.ImgShow && ImageLibrary)? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
						FontSize = 10,
						Color = txtcolor
					},
					new CuiRectTransformComponent
					{
						AnchorMin = (config.IndicatorHUD.ImgShow && ImageLibrary)?  "0.175 0" : "0 0",
						AnchorMax = (config.IndicatorHUD.ImgShow && ImageLibrary)? "0.99 1" : "1 1"
					}
				}
			});
		}

		void HideAllIndicators(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, "diseasesIndicator");
		}

		string CalcUI(float left, float right)
		{
			return $"{left} {right}";
		}
		#endregion
	}
}
