﻿
/*
RTAutoSprintEx, original by Relocity and Tharwnarch, fixed and extended by JohnEdwa

Character names: x_BODY_NAME
COMMANDO, MAGE, ENGI, MERC, HUNTRESS, TOOLBOT


Skill notes:

Commando:
	PRI soft-blocks sprinting
	SEC, SPL, UTI cancels sprint

Huntress:
	PRI allows spriting
	SEC, SPC UTI cancels sprint

MUL-T
	Nailgun stops on sprint.
	Rebar-puncher cancels, but allows while charging.
	Scrap-launcher cancels sprint.
	Buzz-saw allows sprinting while firing.
	
	SEC casts if you sprint.
	UTI makes you sprint afterwards.
	
REX:
	PRI cancels sprint.
	Drill casts on sprinting.
	Boop cancels sprint.
	Succ cancels sprint,

Engineer:
 * PRI cancels sprint, can spring while charging.
 * SEC cancels sprint
 * SPL cancels sprint, can spring while charging.

Artificer: 
 * (SPL) Flamethrower, stops casting
 * (UTI) Ice Wall, forces a cast

Acrid:
 * Melee and Sprint have annoying animation cancelling - wontfix: waiting for official fix
 * Sprint only cancelled by melee.

*/

using System;
using System.Reflection;
using System.Collections;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using Rewired;
using UnityEngine;
using R2API.Utils;
using RoR2;




namespace RT_AutoSprint
{
	[BepInDependency("com.bepis.r2api")]
	[BepInPlugin(pluginGuid, pluginName, pluginVersion)]

	public class RTAutoSprintEx : BaseUnityPlugin
	{
		public const string pluginGuid = "com.RelocityThrawnarchJohnEdwa" + pluginName;
        private const string pluginName = "RTAutoSprintEx";
		private const string pluginVersion = "4478858.1.1";

		private static ConfigWrapper<bool> ArtificerFlamethrowerToggle;
		private static ConfigWrapper<bool> EngineerAllowM2Sprint;

		private static double RT_num;
		public static bool RT_isSprinting;
		public static bool RT_tempDisable;

		public static bool RT_enabled = true;
		public static bool RT_allowall = false;

		public void Awake()
		{

			RTAutoSprintEx.RT_num = 0.0;

		// Configuration
			On.RoR2.Console.Awake += (orig, self) => {
				CommandHelper.RegisterCommands(self);
				orig(self);
			};

			ArtificerFlamethrowerToggle = Config.Wrap<bool>(
				"Artificer",
				"ArtificerFlamethrowerToggle",
				"Sprinting cancels the flamethrower, therefore it either has to disable AutoSprint for a moment, or you need to keep the button held down\ntrue: Flamethrower is a toggle, cancellable by hitting Sprint or casting M2\nfalse: Flamethrower is cast when the button is held down (binding to side mouse button recommended).",
				true
			);

			EngineerAllowM2Sprint = Config.Wrap<bool>(
				"Engineer",
				"EngineerM2Sprint",
				"Allows Engineer to auto-sprint between throwing mines. Looks really janky but technically possible.",
				false
			);

		// Artificer Flamethrower workaround logic
			On.EntityStates.Mage.Weapon.Flamethrower.OnEnter += (orig, self) => {
				RTAutoSprintEx.RT_tempDisable = ArtificerFlamethrowerToggle.Value;
				orig(self);
			};
			On.EntityStates.Mage.Weapon.Flamethrower.OnExit += (orig, self) => { 
				RTAutoSprintEx.RT_tempDisable = false; 
				orig(self);	
			};

		// MUL-T workaround logic, disable sprinting while using the Nailgun, Scrap Launcher, or Stun Grenade.
			//Nailgun
			On.EntityStates.FireNailgun.OnEnter += (orig, self) => { RTAutoSprintEx.RT_tempDisable = true; orig(self); };
			On.EntityStates.FireNailgun.FixedUpdate += (orig, self) => {
				orig(self); 
				if (self.GetFieldValue<bool>("beginToCooldown")) {RTAutoSprintEx.RT_tempDisable = false;};
			};
			// Scrap Launcher
			On.EntityStates.Toolbot.RecoverAimStunDrone.OnEnter += (orig, self) => { RTAutoSprintEx.RT_tempDisable = true; orig(self); };
			On.EntityStates.Toolbot.RecoverAimStunDrone.OnExit += (orig, self) => { RTAutoSprintEx.RT_tempDisable = false; orig(self); };
			// Stun Grenade (M2)
			On.EntityStates.Toolbot.AimStunDrone.OnEnter += (orig, self) => { RTAutoSprintEx.RT_tempDisable = true; orig(self); };
			On.EntityStates.Toolbot.AimStunDrone.OnExit += (orig, self) => { RTAutoSprintEx.RT_tempDisable = false; orig(self); };

		// Sprinting logic
			On.RoR2.PlayerCharacterMasterController.FixedUpdate += delegate(On.RoR2.PlayerCharacterMasterController.orig_FixedUpdate orig, RoR2.PlayerCharacterMasterController self) {
				if (Input.GetKeyDown(KeyCode.F2)) {
					RTAutoSprintEx.RT_enabled = !RTAutoSprintEx.RT_enabled;
					RoR2.Chat.AddMessage("RTAutoSprintEx " + ((RTAutoSprintEx.RT_enabled) ? " enabled." : " disabled."));
				}
				if (Input.GetKeyDown(KeyCode.F3)) {
					RTAutoSprintEx.RT_allowall = !RTAutoSprintEx.RT_allowall;
					RoR2.Chat.AddMessage("RTAutoSprintEx All Skills Autosprint" + ((RTAutoSprintEx.RT_allowall) ? " enabled." : " disabled."));
				}
				
				RTAutoSprintEx.RT_isSprinting = false;
				bool skillsAllowAutoSprint = false;
				RoR2.NetworkUser networkUser = self.networkUser;
				RoR2.InputBankTest instanceFieldBodyInputs = self.GetInstanceField<RoR2.InputBankTest>("bodyInputs");
				if (instanceFieldBodyInputs) {
					if (networkUser && networkUser.localUser != null && !networkUser.localUser.isUIFocused) {
						Player inputPlayer = networkUser.localUser.inputPlayer;
						RoR2.CharacterBody instanceFieldBody = self.GetInstanceField<RoR2.CharacterBody>("body");
						if (instanceFieldBody) {
							RTAutoSprintEx.RT_isSprinting = instanceFieldBody.isSprinting;
							if (!RTAutoSprintEx.RT_isSprinting) {
								if (RTAutoSprintEx.RT_num > 0.1) {
									RTAutoSprintEx.RT_isSprinting = !RTAutoSprintEx.RT_isSprinting;
									RTAutoSprintEx.RT_num = 0.0;
								}
								if (RTAutoSprintEx.RT_allowall) skillsAllowAutoSprint = true;
								else {
								switch(instanceFieldBody.baseNameToken){
									case "COMMANDO_BODY_NAME":
										skillsAllowAutoSprint = (!inputPlayer.GetButton("PrimarySkill"));
										break;
									case "HUNTRESS_BODY_NAME":
										skillsAllowAutoSprint = (!inputPlayer.GetButton("SpecialSkill"));
										break;
									case "MAGE_BODY_NAME":
										if (ArtificerFlamethrowerToggle.Value) RTAutoSprintEx.RT_tempDisable = inputPlayer.GetButton("Sprint"); // Cancel flamethrower if sprint key is pressed.
										skillsAllowAutoSprint = (!inputPlayer.GetButton("PrimarySkill") && !inputPlayer.GetButton("SpecialSkill") && !inputPlayer.GetButton("UtilitySkill") && !RTAutoSprintEx.RT_tempDisable);
										break;
									case "ENGI_BODY_NAME":
										if (EngineerAllowM2Sprint.Value) skillsAllowAutoSprint = (!inputPlayer.GetButton("UtilitySkill"));
										else skillsAllowAutoSprint = (!inputPlayer.GetButton("SecondarySkill") && !inputPlayer.GetButton("UtilitySkill"));
										break;									
									case "MERC_BODY_NAME":
	// ToDo: check all skills
	// Merc secondary cancels, check if it can work
										skillsAllowAutoSprint = (!inputPlayer.GetButton("SecondarySkill") && !inputPlayer.GetButton("SpecialSkill"));
										break;
									case "TREEBOT_BODY_NAME":
										skillsAllowAutoSprint = (!inputPlayer.GetButton("PrimarySkill") && !inputPlayer.GetButton("SecondarySkill") && !inputPlayer.GetButton("SpecialSkill"));
										break;
									case "LOADER_BODY_NAME":
										skillsAllowAutoSprint = (!inputPlayer.GetButton("PrimarySkill"));
										break;											
									case "CROCO_BODY_NAME":
										skillsAllowAutoSprint = (!inputPlayer.GetButton("PrimarySkill"));
										break;
									case "TOOLBOT_BODY_NAME":
										skillsAllowAutoSprint = (!inputPlayer.GetButton("SpecialSkill") && !RTAutoSprintEx.RT_tempDisable);
										break;
									default:
										skillsAllowAutoSprint = (!inputPlayer.GetButton("PrimarySkill") && !inputPlayer.GetButton("SecondarySkill") && !inputPlayer.GetButton("SpecialSkill"));
										break;
								}
								}
							}
							if (skillsAllowAutoSprint) {
								RTAutoSprintEx.RT_num += (double)Time.deltaTime;
							} else {
								RTAutoSprintEx.RT_num = 0.0;
							}
							// Disable sprinting if we movement angle is too large
							if (RTAutoSprintEx.RT_isSprinting) {
								Vector3 aimDirection = instanceFieldBodyInputs.aimDirection;
								aimDirection.y = 0f;
								aimDirection.Normalize();
								Vector3 moveVector = instanceFieldBodyInputs.moveVector;
								moveVector.y = 0f;
								moveVector.Normalize();
								if (Vector3.Dot(aimDirection, moveVector) < self.GetInstanceField<float>("sprintMinAimMoveDot")) {
									RTAutoSprintEx.RT_isSprinting = false;
								}
							}
						}
					}
				}
				orig.Invoke(self);
				if (instanceFieldBodyInputs && RTAutoSprintEx.RT_enabled) {
					instanceFieldBodyInputs.sprint.PushState(RTAutoSprintEx.RT_isSprinting);
				}
			};
			Debug.Log("Loaded RT AutoSprint Extended\nArtificer flamethrower mode is " + ((ArtificerFlamethrowerToggle.Value) ? " [toggle]." : " [hold]."));
		}

		[RoR2.ConCommand(commandName = "rt_artiflamemode", flags = ConVarFlags.None, helpText = "Artificer Flamethrower Mode: Toggle or Hold")]
        private static void RTArtiFlameMode(RoR2.ConCommandArgs args) {
			args.CheckArgumentCount(1);
			switch (args[0].ToLower()) {
				case "toggle":
				case "true":
				case "1":
					ArtificerFlamethrowerToggle.Value = true;
					break;
				case "hold":
				case "false":
				case "0":
					ArtificerFlamethrowerToggle.Value = false;
					break;
				default:
					Debug.Log("Invalid argument. Valid argument: true/false, toggle/hold, 1/0");
					break;
			}
			Debug.Log($"Artificer flamethrower mode is " + ((ArtificerFlamethrowerToggle.Value) ? " [toggle]." : " [hold]."));
		}
	}

	// Helper classes

	public class CommandHelper {
        public static void RegisterCommands(RoR2.Console self) {
            var types = typeof(CommandHelper).Assembly.GetTypes();
            var catalog = self.GetFieldValue<IDictionary>("concommandCatalog");
            foreach (var methodInfo in types.SelectMany(x => x.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))) {
                var customAttributes = methodInfo.GetCustomAttributes(false);
                foreach (var attribute in customAttributes.OfType<RoR2.ConCommandAttribute>()) {
                    var conCommand = Reflection.GetNestedType<RoR2.Console>("ConCommand").Instantiate();
                    conCommand.SetFieldValue("flags", attribute.flags);
                    conCommand.SetFieldValue("helpText", attribute.helpText);
                    conCommand.SetFieldValue("action", (RoR2.Console.ConCommandDelegate)Delegate.CreateDelegate(typeof(RoR2.Console.ConCommandDelegate), methodInfo));
                    catalog[attribute.commandName.ToLower()] = conCommand;
                }
            }
        }
    }

    public static class Utils
	{
		public static T GetInstanceField<T>(this object instance, string fieldName) {
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			FieldInfo field = instance.GetType().GetField(fieldName, bindingAttr);
			return (T)((object)field.GetValue(instance));
		}
		public static void SetInstanceField<T>(this object instance, string fieldName, T value) {
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			FieldInfo field = instance.GetType().GetField(fieldName, bindingAttr);
			field.SetValue(instance, value);
		}
	}
}