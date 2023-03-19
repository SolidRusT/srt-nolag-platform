using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using Oxide.Core.Libraries.Covalence;


namespace Oxide.Plugins
{
    [Info("Custom Genetics", "yoshi2", "0.15")]
	[Description("Allows players to change genetics for seeds in their inventory​")]
	//discord yoshi#8395
    class CustomGenetics : CovalencePlugin
    {
		public Dictionary<ulong, string> Settings = new Dictionary<ulong, string> { };
		private const string CustomGenes = "customgenetics.use";

		#region manual genes
		void GetUserGenes(IPlayer player, string command, string[] args)
		{
			BasePlayer basePlayer = player.Object as BasePlayer;

			if (!(config.AdminBypass && player.IsAdmin) && !player.HasPermission(CustomGenes))
			{
				ReplyToPlayer(player, "NoPermmision");
				return;
			}

			if (config.WholeInventory)
			{
				Item item = basePlayer.GetActiveItem();
				if (item == null)
				{
					ReplyToPlayer(player, "WrongItem");
					return; 
				}

				if (!config.AllowedPlants.Contains(item.info.shortname) || config.AllowedPlants.Contains(item.info.name))
				{
					ReplyToPlayer(player, "WrongItem");
					return;
				}
			}

			if (args.Length != 1 || args[0].Length != 6)
			{
				ReplyToPlayer(player, "WrongFormat");
				return;
			}

			string ColoredGenes = $"<size=18><color=#006300>{args[0]}</size></color>";

			var input = args[0].ToLower().ToCharArray(0, 6);

			foreach (char h in input)
			{
				if (!PossibleGenes.Contains(h))
				{
					ReplyToPlayer(player, "WrongGene");
					return;
				}
			}

			player.Message(lang.GetMessage("GenesSet", this, player.Id) + " " + ColoredGenes);

			Settings[basePlayer.userID] = args[0].ToLower(); 

			EditGenes(basePlayer, Settings[basePlayer.userID]);
		}
		public GrowableGenes Genes = new GrowableGenes();

		void EditGenes(BasePlayer player, string input)
		{
            if (config.WholeInventory)
			{
				Item item = player.GetActiveItem();
				if(item == null) 
				{
					ReplyToPlayer(player.IPlayer, "WrongItem");
					return; 
				}

				NewGenes(item, player, input);
				return;
			}

			foreach(Item item in player.inventory.AllItems())
            {
				NewGenes(item, player, input);
            }
		}

		void NewGenes(Item item, BasePlayer player, string input)
        {
			if (config.AllowedPlants.Contains(item.info.shortname) || config.AllowedPlants.Contains(item.info.name))
			{
				for (int i = 0; i < 6; ++i)
				{
					Genes.Genes[i].Set(CharToGeneType(input[i]), true);
				}
				EncodeGenesToItem(Genes, item);
				item.MarkDirty();
			}
			else
			{
				if (!config.WholeInventory) return;
				ReplyToPlayer(player.IPlayer, "WrongItem");
			}
		}

		#endregion


		#region helpers
		GrowableGenetics.GeneType CharToGeneType(char h)
		{
			switch (h)
			{
				case 'g': return GrowableGenetics.GeneType.GrowthSpeed;
				case 'y': return GrowableGenetics.GeneType.Yield;
				case 'h': return GrowableGenetics.GeneType.Hardiness;
				case 'x': return GrowableGenetics.GeneType.Empty;
				case 'w': return GrowableGenetics.GeneType.WaterRequirement;
				default: return GrowableGenetics.GeneType.Empty;
			}

		}

		public HashSet<char> PossibleGenes = new HashSet<char>
		{
			'g',
			'y',
			'h',
			'x',
			'w',
		};

		void Init()
		{
			permission.RegisterPermission(CustomGenes, this);

			AddCovalenceCommand(config.CommandName, nameof(GetUserGenes));
		}

		protected override void LoadDefaultMessages()
		{
			// English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["GenesSet"] = "Your seeds genetics have been set to ",
				["NoPermmision"] = "you're not allowed to use this command",
				["WrongFormat"] = "Syntax error, proper format is \"/setgenes GGGGYY\"",
				["WrongGene"] = "Syntax error, invalid gene type",
				["WrongItem"] = "the item you're holding is not a valid seed",
			}, this, "en");

			// French
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["GenesSet"] = "La génétique de vos graines a été réglée sur ",
				["NoPermmision"] = "vous n'êtes pas autorisé à utiliser cette commande",
				["WrongFormat"] = "Erreur de syntaxe, le format correct est \"/setgenes GGGGYY\"",
				["WrongGene"] = "Erreur de syntaxe,type de gène non valide",
				["WrongItem"] = "l'article que vous tenez n'est pas une graine valide"
			}, this, "fr");

			// German
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["GenesSet"] = "Ihre Samengenetik wurde eingestellt ",
				["NoPermmision"] = "Sie dürfen diesen Befehl nicht verwenden",
				["WrongFormat"] = "Syntaxfehler, das richtige Format ist \"/setgenes GGGGYY\"",
				["WrongGene"] = "Syntaxfehler, ungültiger Gentyp",
				["WrongItem"] = "Der Gegenstand, den Sie halten, ist kein gültiger Samen"
			}, this, "de");

			// Russian
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["GenesSet"] = "Генетика ваших семян настроена на ",
				["NoPermmision"] = "вам не разрешено использовать эту команду",
				["WrongFormat"] = "Ошибка синтаксиса, правильный формат \"/setgenes GGGGYY\"",
				["WrongGene"] = "Синтаксическая ошибка, недопустимый тип гена",
				["WrongItem"] = "предмет, который вы держите, не является правильным семенем"
			}, this, "ru");

			// Spanish
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["GenesSet"] = "La genética de tus semillas se ha configurado para ",
				["NoPermmision"] = "no tienes permitido usar este comando",
				["WrongFormat"] = "Error de sintaxis, el formato correcto es \"/setgenes GGGGYY\"",
				["WrongGene"] = "Error de sintaxis, tipo de gen no válido",
				["WrongItem"] = "el artículo que tienes no es una semilla válida"
			}, this, "es");
		}

		private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
			player.Reply(string.Format(GetMessage(player, messageName), args));

		private void ChatMessage(BasePlayer player, string messageName, params object[] args) =>
			player.ChatMessage(string.Format(GetMessage(player.IPlayer, messageName), args));

		private string GetMessage(IPlayer player, string messageName, params object[] args)
		{
			var message = lang.GetMessage(messageName, this, player.Id);
			return args.Length > 0 ? string.Format(message, args) : message;
		}

		public static void EncodeGenesToItem(GrowableGenes sourceGrowable, Item targetItem)
		{
			GrowableGeneEncoding.EncodeGenesToItem(GrowableGeneEncoding.EncodeGenesToInt(sourceGrowable), targetItem);
		}

		#endregion


		#region config
		private Configuration config;
		public class Configuration
		{
			[JsonProperty(PropertyName = "allowed seeds (full or short names)")]
			public HashSet<string> AllowedPlants = new HashSet<string>
			{
				"seed.black.berry",
				"seed.blue.berry",
				"seed.corn",
				"seed.green.berry",
				"seed.hemp",
				"seed.potato",
				"seed.pumpkin",
				"seed.red.berry",
				"seed.white.berry",
				"seed.yellow.berry",
				"clone.black.berry",
				"clone.blue.berry",
				"clone.corn",
				"clone.green.berry",
				"clone.hemp",
				"clone.potato",
				"clone.pumpkin",
				"clone.red.berry",
				"clone.white.berry",
				"clone.yellow.berry"
			};

			[JsonProperty(PropertyName = "admins bypass (true/false)")]
			public bool AdminBypass = true;

			[JsonProperty(PropertyName = "only affect the active item (true/false)")]
			public bool WholeInventory = true;

			[JsonProperty(PropertyName = "command name")]
			public string CommandName = "setgenes";
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<Configuration>();
				if (config == null) throw new Exception();
				SaveConfig();
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
	}
}
