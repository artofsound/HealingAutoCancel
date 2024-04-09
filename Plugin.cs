using Aki.Reflection.Patching;
using BepInEx;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using System.Reflection;
using static EFT.Player;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace ImprovedSelfcare
{
	public class Globals
	{
		public static Player player { get; private set; }
		public static PlayerHealthController playerHealthController { get; private set; }
		public static void SetPlayer(Player p) => player = p;
		public static void SetPlayerHealthController(PlayerHealthController controller) => playerHealthController = controller;
	}

	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		private void Awake()
		{
			Config.SaveOnConfigSet = true;
			SetupConfig();

			new HealingAutoCancelPatch().Enable();
		}

		internal static ConfigEntry<bool> EnableAutoHealCanceling;

		private void SetupConfig()
		{
			EnableAutoHealCanceling = Config.Bind("Heal", "Enable automatic heal canceling", true);
		}
	}

	internal class HealingAutoCancelPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(GameWorld).GetMethod("OnGameStarted", BindingFlags.Public | BindingFlags.Instance);
		}

		[PatchPostfix]
		static void PostFix()
		{
			GameWorld gameWorld = Singleton<GameWorld>.Instance;

			Globals.SetPlayer(gameWorld.MainPlayer);
			Globals.SetPlayerHealthController(gameWorld.MainPlayer.PlayerHealthController);
			Globals.playerHealthController.HealthChangedEvent += ActiveHealthController_HealthChangedEvent;
		}

		private static void ActiveHealthController_HealthChangedEvent(EBodyPart bodyPart, float amount, DamageInfo damageInfo)
		{
			if (damageInfo.DamageType != EDamageType.Medicine)
				return;

			MedsClass medkitInHands = Globals.player.TryGetItemInHands<MedsClass>();

			//Try to ignore any healing done by stims and ensure we do not try to cancel fixing a broken limb
			if (medkitInHands != null && !Globals.playerHealthController.IsBodyPartBroken(bodyPart))
			{
				ValueStruct bodyPartHealth = Globals.playerHealthController.GetBodyPartHealth(bodyPart);

				//There might be a better way to check bleeding status
				//This works though
				var effects = Globals.playerHealthController.BodyPartEffects.Effects[bodyPart];
				bool bleeding = effects.ContainsKey("LightBleeding") || effects.ContainsKey("HeavyBleeding");				

				//Feels like this is not working correctly
				//Autocancel should trigger when medkit runs out
				bool healingItemDepleted = medkitInHands.MedKitComponent.HpResource < 1;

				if ((bodyPartHealth.AtMaximum && !bleeding) || healingItemDepleted)
					//This is the magical part! Woooaahh
					Globals.playerHealthController.CancelApplyingItem();
			}
		}
	}
}