using Oxide.Core.Configuration;
using Rust;

namespace Oxide.Plugins
{
	[Info("Recycle Blueprints", "Zugzwang", "1.0.6")]
	[Description("Allows players to recycle blueprints for scrap.")]

	class RecycleBlueprints : CovalencePlugin
   {
		#region Configuration and Scrap Yields

		// Scrap ID
		int scrapID = -932201673; 
		
		// Default scrap yields.
		int common = 10;
		int uncommon = 25;		
		int rare = 100;
		int veryrare = 300;	

		// Save scrap yields if file doesnt exist.
		protected override void LoadDefaultConfig()
		{
			Config["Category_Common"] = common;
			Config["Category_Uncommon"] = uncommon;
			Config["Category_Rare"] = rare;
			Config["Category_VeryRare"] = veryrare;
			
			PrintWarning("New configuration file created.");
		}
	
		// Load scrap yields on startup.
		void Init()
		{
			common = (int)Config["Category_Common"];
			uncommon = (int)Config["Category_Uncommon"];
			rare = (int)Config["Category_Rare"];
			veryrare = (int)Config["Category_VeryRare"];
		}

		void OnServerInitialized()
		{
			// Look for scrap ID on load, just in case they change it again...
			ItemDefinition scrap = ItemManager.FindItemDefinition("scrap");
			if (scrap?.itemid != null)
				scrapID = scrap.itemid;

			bool changed = false;
			var bpList = ItemManager.bpList;
			
			// Look for new blueprints	
			foreach (ItemBlueprint bp in bpList)
			{
				if (bp.defaultBlueprint || !(bp.isResearchable))
					continue;

				if (Config["Custom_" + bp.targetItem.shortname] == null)
				{
					Config["Custom_" + bp.targetItem.shortname] = -1;
					changed = true;
				}
			}
			
			if (changed)
			{				
				PrintWarning("Updating configuration file with new blueprints.");
				SaveConfig();				
			}
		}

		int ScrapValue(Item i)
		{
			int amount = 0;
			
			if (i?.IsBlueprint() == true)
			{
				ItemDefinition target = ItemManager.FindItemDefinition(i.blueprintTarget);
				if (target == null) return 0;
				
				int custom = (int)Config["Custom_" + target.shortname];
				
				// Set scrap amount based on custom setting, or rarity category.
				if (custom > 0)
					amount = custom;
				else if (target?.rarity == null)
					amount = 1;
				else if (target.rarity == Rarity.Common)
					amount = common;
				else if (target.rarity == Rarity.Uncommon)
					amount = uncommon;
				else if (target.rarity == Rarity.Rare)
					amount = rare;
				else if (target.rarity == Rarity.VeryRare || target.rarity == Rarity.None)
					amount = veryrare;
			}

			return amount;
		}
		
		#endregion Configuration and Scrap Yields


		#region Oxide Hooks 

		// Allow recycling of enabled blueprint categories.
		object CanRecycle(Recycler recycler, Item item)
		{
			if (item?.IsBlueprint() == true && ScrapValue(item) > 0)	
			{
				return true;
			}
			
			return null;
		}

		// Allow blueprints to be put into the recycler.
		object CanBeRecycled(Item i, Recycler r)
		{
			return CanRecycle(r, i);
		}
		
		// Turn those blueprints into scrap.
		object OnRecycleItem(Recycler recycler, Item item)
		{
			if (item?.IsBlueprint() == true)
			{
				int amount = ScrapValue(item);

				if (amount > 0)
				{
					Item reward = ItemManager.CreateByItemID(scrapID, amount);
					if (reward != null)
					{
						recycler.MoveItemToOutput(reward);
						item.UseItem(1);
						return true;
					}
				}
			}

			return null;
		}
		
		#endregion Oxide Hooks 
	}
}
