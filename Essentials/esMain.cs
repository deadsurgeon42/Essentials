﻿/*
 * Original plugin by Scavenger.
 * 
 */

using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Terraria;
using Terraria.DataStructures;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace Essentials
{
	[ApiVersion(2, 1)]
	public class Essentials : TerrariaPlugin
	{
		public override string Name { get { return "Essentials"; } }
		public override string Author { get { return "Zaicon"; } }
		public override string Description { get { return "Some Essential commands for TShock!"; } }
		public override Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

		public Dictionary<string, int[]> Disabled = new Dictionary<string, int[]>();
		public esPlayer[] esPlayers = new esPlayer[256];
		public DateTime LastCheck = DateTime.UtcNow;
		public static esConfig Config = new esConfig();
		public static string SavePath = string.Empty;

		public Essentials(Main game)
			: base(game)
		{
			base.Order = 4;
		}

		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
			ServerApi.Hooks.ServerChat.Register(this, OnChat);
			PlayerHooks.PlayerCommand += OnPlayerCommand;
			ServerApi.Hooks.NetGetData.Register(this, OnGetData);
			ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
		}

		protected override void Dispose(bool Disposing)
		{
			if (Disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
				ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
				PlayerHooks.PlayerCommand -= OnPlayerCommand;
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
				ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
			}
			base.Dispose(Disposing);
		}

		public void OnInitialize(EventArgs args)
		{
			#region Add Commands
			Commands.ChatCommands.Add(new Command("essentials.more", CMDmore, "more"));
			Commands.ChatCommands.Add(new Command("essentials.position.ruler", CMDruler, "ruler"));
			Commands.ChatCommands.Add(new Command("essentials.helpop.ask", CMDhelpop, "helpop"));
			Commands.ChatCommands.Add(new Command("essentials.suicide", CMDsuicide, "suicide", "die"));
			Commands.ChatCommands.Add(new Command("essentials.pvp.burn", CMDburn, "burn"));
			Commands.ChatCommands.Add(new Command("essentials.moon", CMDmoon, "moon"));
			Commands.ChatCommands.Add(new Command(new List<string> { "essentials.back.tp", "essentials.back.death" }, CMDback, "b"));
			Commands.ChatCommands.Add(new Command("essentials.home", CMDsethome, "sethome"));
			Commands.ChatCommands.Add(new Command("essentials.home", CMDmyhome, "myhome"));
			Commands.ChatCommands.Add(new Command("essentials.home", CMDdelhome, "delhome"));
			Commands.ChatCommands.Add(new Command("essentials.essentials", CMDessentials, "essentials"));
			Commands.ChatCommands.Add(new Command(/*no permission*/ CMDteamunlock, "teamunlock"));
			Commands.ChatCommands.Add(new Command("essentials.lastcommand", CMDequals, "=") { DoLog = false });
			Commands.ChatCommands.Add(new Command("essentials.pvp.killr", CMDkillr, "killr"));
			Commands.ChatCommands.Add(new Command("essentials.disable", CMDdisable, "disable"));
			Commands.ChatCommands.Add(new Command("essentials.level.top", CMDtop, "top"));
			Commands.ChatCommands.Add(new Command("essentials.level.up", CMDup, "up"));
			Commands.ChatCommands.Add(new Command("essentials.level.down", CMDdown, "down"));
			Commands.ChatCommands.Add(new Command("essentials.level.side", CMDleft, "left"));
			Commands.ChatCommands.Add(new Command("essentials.level.side", CMDright, "right"));
			Commands.ChatCommands.Add(new Command("essentials.ping", CMDping, "ping", "pong", "echo"));
			Commands.ChatCommands.Add(new Command("essentials.sudo", CMDsudo, "sudo"));
			Commands.ChatCommands.Add(new Command("essentials.near", CMDnear, "near"));
			Commands.ChatCommands.Add(new Command("essentials.forcelogin", CMDforcelogin, "forcelogin"));
			Commands.ChatCommands.Add(new Command("essentials.inventory.see", CMDinvsee, "invsee"));
			Commands.ChatCommands.Add(new Command("essentials.whois", CMDwhois, "whois"));
			Commands.ChatCommands.Add(new Command("essentials.ids.search", ESFind, "find"));

			#endregion

			SavePath = Path.Combine(TShock.SavePath, "Essentials");
			if (!Directory.Exists(SavePath))
			{
				Directory.CreateDirectory(SavePath);
			}

			EsSQL.SetupDB();
			esConfig.LoadConfig();
		}

		#region esPlayer Join / Leave
		public void OnJoin(JoinEventArgs args)
		{
			esPlayers[args.Who] = new esPlayer(args.Who);

			if (esPlayers[args.Who] != null && TShock.Players[args.Who] != null)
			{
				if (Disabled.ContainsKey(TShock.Players[args.Who].Name))
				{
					var ePly = esPlayers[args.Who];
					ePly.DisabledX = Disabled[TShock.Players[args.Who].Name][0];
					ePly.DisabledY = Disabled[TShock.Players[args.Who].Name][1];
					ePly.TSPlayer.Teleport(ePly.DisabledX * 16F, ePly.DisabledY * 16F);
					ePly.Disabled = true;
					ePly.Disable();
					ePly.LastDisabledCheck = DateTime.UtcNow;
					ePly.TSPlayer.SendErrorMessage("You are still disabled.");
				}

				if (EsSQL.GetNickname(TShock.Players[args.Who].Name, out string nickname))
				{
					var ePly = esPlayers[args.Who];
					ePly.HasNickName = true;
					ePly.OriginalName = ePly.TSPlayer.Name;
					ePly.Nickname = nickname;
				}
			}
		}

		public void OnLeave(LeaveEventArgs args)
		{
			if (esPlayers[args.Who] != null)
				if (esPlayers[args.Who].InvSee != null)
				{
					esPlayers[args.Who].InvSee.RestoreCharacter(TShock.Players[args.Who]);

					esPlayers[args.Who].InvSee = null;

					esPlayers[args.Who] = null;
				}
		}
		#endregion

		#region Chat / Command
		public void OnChat(ServerChatEventArgs e)
		{
			if (e.Handled)
			{
				return;
			}

			var ePly = esPlayers[e.Who];
			var tPly = TShock.Players[e.Who];

			if (ePly == null || tPly == null)
				return;

			if (ePly.TSPlayer.Active && tPly.Active)
			{
				if (ePly.HasNickName && !e.Text.StartsWith("/") && !tPly.mute)
				{
					e.Handled = true;
					string nick = Config.PrefixNicknamesWith + ePly.Nickname;
					TSPlayer.All.SendMessage(String.Format(TShock.Config.ChatFormat, tPly.Group.Name, tPly.Group.Prefix, nick, tPly.Group.Suffix, e.Text),
									tPly.Group.R, tPly.Group.G, tPly.Group.B);
				}
				else if (ePly.HasNickName && e.Text.StartsWith("/me ") && !tPly.mute)
				{
					e.Handled = true;
					string nick = Config.PrefixNicknamesWith + ePly.Nickname;
					TSPlayer.All.SendMessage(string.Format("*{0} {1}", nick, e.Text.Remove(0, 4)), 205, 133, 63);
				}
			}
		}
		public void OnPlayerCommand(PlayerCommandEventArgs e)
		{
			if (e.Handled)
			{
				return;
			}

			if (e.Player.Index >= 0 && e.Player.Index <= 255)
			{
				if (e.CommandName != "=")
				{
					esPlayers[e.Player.Index].LastCMD = string.Concat("/", e.CommandText);
				}

				if ((e.CommandName == "tp" && e.Player.Group.HasPermission(Permissions.tp)) ||
					(e.CommandName == "home" && e.Player.Group.HasPermission(Permissions.home)) ||
					(e.CommandName == "spawn" && e.Player.Group.HasPermission(Permissions.spawn)) ||
					(e.CommandName == "warp" && e.Player.Group.HasPermission(Permissions.warp)))
				{
					var ePly = esPlayers[e.Player.Index];
					ePly.LastBackX = e.Player.TileX;
					ePly.LastBackY = e.Player.TileY;
					ePly.LastBackAction = BackAction.TP;
				}
			}
			else if (e.CommandName == "whisper" || e.CommandName == "w" || e.CommandName == "tell" ||
					e.CommandName == "reply" || e.CommandName == "r")
			{
				if (!e.Player.Group.HasPermission(Permissions.whisper))
				{
					return;
				}
				foreach (var player in esPlayers)
				{
					if (player == null || !player.SocialSpy || player.Index == e.Player.Index)
					{
						continue;
					}
					if ((e.CommandName == "reply" || e.CommandName == "r") && e.Player.LastWhisper != null)
					{
						player.TSPlayer.SendMessage(string.Format("[SocialSpy] from {0} to {1}: /{2}", e.Player.Name, e.Player.LastWhisper.Name, e.CommandText), Color.Gray);
					}
					else
					{
						player.TSPlayer.SendMessage(string.Format("[SocialSpy] {0}: /{1}", e.Player.Name, e.CommandText), Color.Gray);
					}
				}
			}
		}
		#endregion

		#region Get Data
		public void OnGetData(GetDataEventArgs e)
		{
			if (e.MsgID != PacketTypes.PlayerTeam || !(Config.LockRedTeam || Config.LockGreenTeam || Config.LockBlueTeam || Config.LockYellowTeam))
			{
				return;
			}

			var who = e.Msg.readBuffer[e.Index];
			var team = e.Msg.readBuffer[e.Index + 1];

			var ePly = esPlayers[who];
			var tPly = TShock.Players[who];
			if (ePly == null || tPly == null) return;
			switch (team)
			{
				#region Red
				case 1:
					if (Config.LockRedTeam && !tPly.Group.HasPermission(Config.RedTeamPermission) && (ePly.RedPassword != Config.RedTeamPassword || ePly.RedPassword == ""))
					{
						e.Handled = true;
						tPly.SetTeam(tPly.Team);
						if (Config.RedTeamPassword == "")
							tPly.SendErrorMessage("You do not have permission to join that team.");
						else
							tPly.SendErrorMessage("That team is locked, use \'/teamunlock red <password>\' to access it.");

					}
					break;
				#endregion

				#region Green
				case 2:
					if (Config.LockGreenTeam && !tPly.Group.HasPermission(Config.GreenTeamPermission) && (ePly.GreenPassword != Config.GreenTeamPassword || ePly.GreenPassword == ""))
					{
						e.Handled = true;
						tPly.SetTeam(tPly.Team);
						if (Config.GreenTeamPassword == "")
							tPly.SendErrorMessage("You do not have permission to join that team.");
						else
							tPly.SendErrorMessage("That team is locked, use \'/teamunlock green <password>\' to access it.");

					}
					break;
				#endregion

				#region Blue
				case 3:
					if (Config.LockBlueTeam && !tPly.Group.HasPermission(Config.BlueTeamPermission) && (ePly.BluePassword != Config.BlueTeamPassword || ePly.BluePassword == ""))
					{
						e.Handled = true;
						tPly.SetTeam(tPly.Team);
						if (Config.BlueTeamPassword == "")
							tPly.SendErrorMessage("You do not have permission to join that team.");
						else
							tPly.SendErrorMessage("That team is locked, use \'/teamunlock blue <password>\' to access it.");

					}
					break;
				#endregion

				#region Yellow
				case 4:
					if (Config.LockYellowTeam && !tPly.Group.HasPermission(Config.YellowTeamPermission) && (ePly.YellowPassword != Config.YellowTeamPassword || ePly.YellowPassword == ""))
					{
						e.Handled = true;
						tPly.SetTeam(tPly.Team);
						if (Config.YellowTeamPassword == "")
							tPly.SendErrorMessage("You do not have permission to join that team.");
						else
							tPly.SendErrorMessage("That team is locked, use \'/teamunlock yellow <password>\' to access it.");

					}
					break;
					#endregion
			}
		}
		#endregion

		#region Timer
		public void OnUpdate(EventArgs args)
		{
			if ((DateTime.UtcNow - LastCheck).TotalMilliseconds >= 1000)
			{
				LastCheck = DateTime.UtcNow;
				foreach (var ePly in esPlayers)
				{
					if (ePly == null)
					{
						continue;
					}
					if (ePly.TSPlayer == null)
					{
						continue;
					}

					if (!ePly.SavedBackAction && ePly.TSPlayer.Dead)
					{
						if (ePly.TSPlayer.Group.HasPermission("essentials.back.death"))
						{
							ePly.LastBackX = ePly.TSPlayer.TileX;
							ePly.LastBackY = ePly.TSPlayer.TileY;
							ePly.LastBackAction = BackAction.Death;
							ePly.SavedBackAction = true;
							if (Config.ShowBackMessageOnDeath)
								ePly.TSPlayer.SendSuccessMessage("Type \"/b\" to return to your position before you died.");
						}
					}
					else if (ePly.SavedBackAction && !ePly.TSPlayer.Dead)
						ePly.SavedBackAction = false;

					if (ePly.BackCooldown > 0)
						ePly.BackCooldown--;

					if (ePly.ptTime > -1.0)
					{
						ePly.ptTime += 60.0;
						if (!ePly.ptDay && ePly.ptTime > 32400.0)
						{
							ePly.ptDay = true; ePly.ptTime = 0.0;
						}
						else if (ePly.ptDay && ePly.ptTime > 54000.0)
						{
							ePly.ptDay = false; ePly.ptTime = 0.0;
						}
					}

					if (ePly.Disabled && ((DateTime.UtcNow - ePly.LastDisabledCheck).TotalMilliseconds) > 3000)
					{
						ePly.LastDisabledCheck = DateTime.UtcNow;
						ePly.Disable();
						if ((ePly.TSPlayer.TileX > ePly.DisabledX + 5 || ePly.TSPlayer.TileX < ePly.DisabledX - 5) || (ePly.TSPlayer.TileY > ePly.DisabledY + 5 || ePly.TSPlayer.TileY < ePly.DisabledY - 5))
						{
							ePly.TSPlayer.Teleport(ePly.DisabledX * 16F, ePly.DisabledY * 16F);
						}
					}
				}
			}
		}
		#endregion

		/* Commands: */

		#region More
		private void CMDmore(CommandArgs args)
		{
			if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "all")
			{
				bool full = true;
				int i = 0;
				foreach (Item item in args.TPlayer.inventory)
				{
					if (item == null || item.stack == 0) continue;
					int amtToAdd = item.maxStack - item.stack;
					if (item.stack > 0 && amtToAdd > 0 && !item.Name.ToLower().Contains("coin"))
					{
						full = false;
						args.Player.GiveItem(item.type, item.Name, item.width, item.height, amtToAdd);
					}
					i++;
				}
				if (!full)
					args.Player.SendSuccessMessage("Filled all your items.");
				else
					args.Player.SendErrorMessage("Your inventory is already full.");
			}
			else
			{
				Item holding = args.Player.TPlayer.inventory[args.TPlayer.selectedItem];
				int amtToAdd = holding.maxStack - holding.stack;
				if (holding.stack > 0 && amtToAdd > 0)
					args.Player.GiveItem(holding.type, holding.Name, holding.width, holding.height, amtToAdd);
				if (amtToAdd == 0)
					args.Player.SendErrorMessage("Your {0} is already full.", holding.Name);
				else
					args.Player.SendSuccessMessage("Filled up your {0}.", holding.Name);
			}
		}
		#endregion

		#region Position Commands

		private void CMDtppos(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("Usage: /tppos <X> [Y]");
				return;
			}

			int X = 0, Y = 0;
			if (!int.TryParse(args.Parameters[0], out X) || (args.Parameters.Count == 2 && !int.TryParse(args.Parameters[1], out Y)))
			{
				args.Player.SendErrorMessage("Usage: /tppos <X> [Y]");
				return;
			}

			if (args.Parameters.Count == 1)
				Y = EsUtils.GetTop(X);

			var ePly = esPlayers[args.Player.Index];
			if (ePly != null)
			{
				ePly.LastBackX = args.Player.TileX;
				ePly.LastBackY = args.Player.TileY;
				ePly.LastBackAction = BackAction.TP;
			}

			args.Player.Teleport(X * 16F, Y * 16F);
			args.Player.SendSuccessMessage("Teleported you to X: {0} - Y: {1}", X, Y);
		}

		private void CMDruler(CommandArgs args)
		{
			if (args.Parameters.Count == 1 &&
				int.TryParse(args.Parameters[0], out int choice) &&
				choice >= 1 && choice <= 2)
			{
				args.Player.SendInfoMessage("Hit a block to Set Point {0}.", choice);
				args.Player.AwaitingTempPoint = choice;
			}
			else
			{
				if (args.Player.TempPoints[0] == Point.Zero || args.Player.TempPoints[1] == Point.Zero)
					args.Player.SendErrorMessage("Invalid Points. To set points use: /ruler [1/2]");
				else
				{
					var width = Math.Abs(args.Player.TempPoints[0].X - args.Player.TempPoints[1].X);
					var height = Math.Abs(args.Player.TempPoints[0].Y - args.Player.TempPoints[1].Y);
					args.Player.SendSuccessMessage("Area Height: {0} Width: {1}", height, width);
					args.Player.TempPoints[0] = Point.Zero; args.Player.TempPoints[1] = Point.Zero;
				}
			}
		}
		#endregion
		#region HelpOp
		private void CMDhelpop(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Usage: /helpop <message>");
				return;
			}

			string text = string.Join(" ", args.Parameters);

			List<string> online = new List<string>();

			foreach (var ePly in esPlayers)
			{
				if (ePly == null || !ePly.TSPlayer.Group.HasPermission("essentials.helpop.receive")) continue;
				online.Add(ePly.TSPlayer.Name);
				ePly.TSPlayer.SendMessage(string.Format("[HelpOp] {0}: {1}", args.Player.Name, text), Color.MediumPurple);
			}
			if (online.Count < 1)
				args.Player.SendMessage("[HelpOp] There are no operators online to receive your message.", Color.MediumPurple);
			else
			{
				string to = string.Join(", ", online.Select(p => p));
				args.Player.SendMessage(string.Format("[HelpOp] Your message has been received by the operator(s): {0}", to), Color.MediumPurple);
			}
		}
		#endregion

		#region Suicide
		private void CMDsuicide(CommandArgs args)
		{
			if (!args.Player.RealPlayer)
				return;
			NetMessage.SendPlayerHurt(args.Player.Index, PlayerDeathReason.ByCustomReason(" decided it wasnt worth living."), 15000, 1, false, false, 0);
		}
		#endregion

		#region Burn
		private void CMDburn(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("Usage: /burn <player> [seconds]");
				return;
			}

			int duration = 30;
			if (args.Parameters.Count == 2 && !int.TryParse(args.Parameters[1], out duration))
				duration = 30;
			duration *= 60;
			var PlayersFound = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (PlayersFound.Count != 1)
			{
				var matches = new List<string>();
				PlayersFound.ForEach(pl => { matches.Add(pl.Name); });
				TShock.Utils.SendMultipleMatchError(args.Player, matches);
				return;
			}
			PlayersFound[0].SetBuff(24, duration);
			args.Player.SendSuccessMessage("{0} Has been set on fire for {1} second(s).", PlayersFound[0].Name, duration);
		}
		#endregion

		#region Moon
		private void CMDmoon(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Usage: /moon [ new | 1/4 | half | 3/4 | full ]");
				return;
			}

			string subcmd = args.Parameters[0].ToLower();

			switch (subcmd)
			{
				case "new":
					Main.moonPhase = 4;
					break;
				case "1/4":
					Main.moonPhase = 3;
					break;
				case "half":
					Main.moonPhase = 2;
					break;
				case "3/4":
					Main.moonPhase = 1;
					break;
				case "full":
					Main.moonPhase = 0;
					break;
				default:
					args.Player.SendErrorMessage("Usage: /moon [ new | 1/4 | half | 3/4 | full ]");
					break;
			}
			NetMessage.SendData(7);
			args.Player.SendSuccessMessage("Moon Phase set to {0} Moon.", subcmd);
		}
		#endregion

		#region Back
		private void CMDback(CommandArgs args)
		{
			var ePly = esPlayers[args.Player.Index];
			if (ePly.TSPlayer.Dead)
			{
				args.Player.SendErrorMessage("Please wait till you respawn.");
				return;
			}
			if (ePly.BackCooldown > 0)
			{
				args.Player.SendErrorMessage("You must wait another {0} seconds before you can use /b again.", ePly.BackCooldown);
				return;
			}

			if (ePly.LastBackAction == BackAction.None)
				args.Player.SendErrorMessage("You do not have a /b position stored.");
			else if (ePly.LastBackAction == BackAction.TP)
			{
				if (Config.BackCooldown > 0 && !args.Player.Group.HasPermission("essentials.back.nocooldown"))
				{
					ePly.BackCooldown = Config.BackCooldown;
				}
				args.Player.Teleport(ePly.LastBackX * 16F, ePly.LastBackY * 16F);
				args.Player.SendSuccessMessage("Moved you to your position before you last teleported.");
			}
			else if (ePly.LastBackAction == BackAction.Death && args.Player.Group.HasPermission("essentials.back.death"))
			{
				if (Config.BackCooldown > 0 && !args.Player.Group.HasPermission("essentials.back.nocooldown"))
				{
					ePly.BackCooldown = Config.BackCooldown;
				}
				args.Player.Teleport(ePly.LastBackX * 16F, ePly.LastBackY * 16F);
				args.Player.SendSuccessMessage("Moved you to your position before you last died.");
			}
			else
				args.Player.SendErrorMessage("You do not have permission to /b after death.");
		}
		#endregion

		#region Seach IDs
		private void CMDspage(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("Usage: /spage <page>");
				return;
			}

			int Page = 1;
			if (!int.TryParse(args.Parameters[0], out Page))
			{
				args.Player.SendErrorMessage(string.Format("Specified page ({0}) invalid.", args.Parameters[0]));
				return;
			}

			var ePly = esPlayers[args.Player.Index];

			EsUtils.DisplaySearchResults(args.Player, ePly.LastSearchResults, Page);
		}

		private void CMDsitems(CommandArgs args)
		{
			Commands.HandleCommand(args.Player, "/find -item " + string.Join(" ", args.Parameters.Select(p => p)));
		}

		private void CMDsnpcs(CommandArgs args)
		{
			Commands.HandleCommand(args.Player, "/find -npc " + string.Join(" ", args.Parameters.Select(p => p)));
		}

		private void ESFind(CommandArgs args)
		{
			var regex = new Regex(@"^\w+ -(\w+) (.+?) ?(\d*)$");
			Match match = regex.Match(args.Message);
			if (!match.Success)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}find <-switch> <name...> [page]", TShock.Config.CommandSpecifier);
				args.Player.SendSuccessMessage("Valid {0}find switches:", TShock.Config.CommandSpecifier);
				args.Player.SendInfoMessage("-command, -item, -npc, -tile, -wall, -buff, -projectile, -prefix");
				return;
			}

			int page = 1;
			if (!String.IsNullOrWhiteSpace(match.Groups[3].Value) && (!int.TryParse(match.Groups[3].Value, out page) || page <= 0))
			{
				args.Player.SendErrorMessage("Invalid page '{0}'!", match.Groups[3].Value);
				return;
			}

			switch (match.Groups[1].Value.ToLowerInvariant())
			{
				#region Command
				case "command":
					var commands = new List<string>();

					foreach (Command command in TShockAPI.Commands.ChatCommands.Where(c => c.Names.Any(s => s.ToLower().Contains(match.Groups[2].Value.ToLower()))))
						commands.Add(String.Format("{0} (Permission: {1})", command.Name, command.Permissions.FirstOrDefault()));

					PaginationTools.SendPage(args.Player, page, commands,
						new PaginationTools.Settings
						{
							HeaderFormat = "Found Commands ({0}/{1}):",
							FooterFormat = String.Format("Type /find -command {0} {{0}} for more", match.Groups[2].Value),
							NothingToDisplayString = "No commands were found."
						});
					return;
				#endregion
				#region Item
				case "item":
					var items = new List<string>();


					for (int i = -48; i < 0; i++)
					{
						var item = new Item();
						item.netDefaults(i);
						if (item.Name.ToLower().Contains(match.Groups[2].Value.ToLower()))
							items.Add(String.Format("{0} (ID: {1})", item.Name, i));
					}
					for (int i = 0; i < Main.item.Length; i++)
					{
						if (Lang.GetItemNameValue(i).ToLower().Contains(match.Groups[2].Value.ToLower()))
							items.Add(String.Format("{0} (ID: {1})", Lang.GetItemNameValue(i), i));
					}

					PaginationTools.SendPage(args.Player, page, items,
						new PaginationTools.Settings
						{
							HeaderFormat = "Found Items ({0}/{1}):",
							FooterFormat = String.Format("Type /find -item {0} {{0}} for more", match.Groups[2].Value),
							NothingToDisplayString = "No items were found."
						});
					return;
				#endregion
				#region NPC
				case "npc":
					var npcs = new List<string>();

					for (int i = -65; i < 0; i++)
					{
						var npc = new NPC();
						npc.SetDefaults(i);
						if (Lang.GetNPCNameValue(i).ToLower().Contains(match.Groups[2].Value.ToLower()))
							npcs.Add(String.Format("{0} (ID: {1})", Lang.GetNPCNameValue(i), i));
					}
					for (int i = 0; i < Main.npc.Length; i++)
					{
						if (Lang.GetNPCNameValue(i).ToLower().Contains(match.Groups[2].Value.ToLower()))
							npcs.Add(String.Format("{0} (ID: {1})", Lang.GetNPCNameValue(i), i));
					}

					PaginationTools.SendPage(args.Player, page, npcs,
						new PaginationTools.Settings
						{
							HeaderFormat = "Found NPCs ({0}/{1}):",
							FooterFormat = String.Format("Type /find -npc {0} {{0}} for more", match.Groups[2].Value),
							NothingToDisplayString = "No NPCs were found.",
						});
					return;
				#endregion
				#region Tile
				case "tile":
					var tiles = new List<string>();

					foreach (FieldInfo fi in typeof(Terraria.ID.TileID).GetFields())
					{
						var sb = new StringBuilder();
						for (int i = 0; i < fi.Name.Length; i++)
						{
							if (Char.IsUpper(fi.Name[i]) && i > 0)
								sb.Append(" ").Append(fi.Name[i]);
							else
								sb.Append(fi.Name[i]);
						}

						string name = sb.ToString();
						if (name.ToLower().Contains(match.Groups[2].Value.ToLower()))
							tiles.Add(String.Format("{0} (ID: {1})", name, fi.GetValue(null)));
					}

					PaginationTools.SendPage(args.Player, page, tiles,
						new PaginationTools.Settings
						{
							HeaderFormat = "Found Tiles ({0}/{1}):",
							FooterFormat = String.Format("Type /find -tile {0} {{0}} for more", match.Groups[2].Value),
							NothingToDisplayString = "No tiles were found.",
						});
					return;
				#endregion
				#region Wall
				case "wall":
					var walls = new List<string>();

					foreach (FieldInfo fi in typeof(Terraria.ID.WallID).GetFields())
					{
						var sb = new StringBuilder();
						for (int i = 0; i < fi.Name.Length; i++)
						{
							if (Char.IsUpper(fi.Name[i]) && i > 0)
								sb.Append(" ").Append(fi.Name[i]);
							else
								sb.Append(fi.Name[i]);
						}

						string name = sb.ToString();
						if (name.ToLower().Contains(match.Groups[2].Value.ToLower()))
							walls.Add(String.Format("{0} (ID: {1})", name, fi.GetValue(null)));
					}

					PaginationTools.SendPage(args.Player, page, walls,
						new PaginationTools.Settings
						{
							HeaderFormat = "Found Walls ({0}/{1}):",
							FooterFormat = String.Format("Type /find -wall {0} {{0}} for more", match.Groups[2].Value),
							NothingToDisplayString = "No walls were found.",
						});
					return;
				case "buff":
					var buffs = new List<string>();
					for (int i = 0; i < Lang._buffNameCache.Length; i++)
					{
						if (Lang.GetBuffName(i) == null)
							continue;
						if (Lang.GetBuffName(i).ToLower().Contains(match.Groups[2].Value.ToLower()))
							buffs.Add($"{Lang.GetBuffName(i)} (ID: {i})");
					}
					PaginationTools.SendPage(args.Player, page, buffs,
						new PaginationTools.Settings
						{
							HeaderFormat = "Found Buffs ({0}/{1}):",
							FooterFormat = String.Format("Type /find -buff {0} {{0}} for more", match.Groups[2].Value),
							NothingToDisplayString = "No buffs were found.",
						});
					return;
				case "projectile":
				case "proj":
					var projs = new List<string>();
					for (int i = 0; i < Lang._projectileNameCache.Length; i++)
					{
						if (Lang.GetProjectileName(i) == null)
							continue;
						if (Lang.GetProjectileName(i).Value.ToLower().Contains(match.Groups[2].Value.ToLower()))
							projs.Add($"{Lang.GetProjectileName(i)} (ID: {i})");
					}
					PaginationTools.SendPage(args.Player, page, projs,
						new PaginationTools.Settings
						{
							HeaderFormat = "Found Projectiles ({0}/{1}):",
							FooterFormat = String.Format("Type /find -projectile {0} {{0}} for more", match.Groups[2].Value),
							NothingToDisplayString = "No buffs were found.",
						});
					return;
				case "prefix":
					var prefixes = new List<string>();
					for (int i = 0; i < Lang.prefix.Length; i++)
					{
						if (Lang.prefix[i] == null)
							continue;
						if (Lang.prefix[i].Value.ToLower().Contains(match.Groups[2].Value.ToLower()))
							prefixes.Add($"{Lang.prefix[i].Value} (ID: {i})");
					}
					PaginationTools.SendPage(args.Player, page, prefixes,
						new PaginationTools.Settings
						{
							HeaderFormat = "Found Prefixes ({0}/{1}):",
							FooterFormat = String.Format("Type /find -prefix {0} {{0}} for more", match.Groups[2].Value),
							NothingToDisplayString = "No buffs were found.",
						});
					return;
				#endregion
				default:
					args.Player.SendSuccessMessage("Valid {0}find switches:", TShock.Config.CommandSpecifier);
					args.Player.SendInfoMessage("-command, -item, -npc, -tile, -wall, -buff, -projectile, -prefix");
					return;
			}
		}
		#endregion

		#region MyHome
		private void CMDsethome(CommandArgs args)
		{
			/* Chek if the player is a real player */
			if (!args.Player.RealPlayer)
			{
				args.Player.SendErrorMessage("You must be a real player.");
				return;
			}
			/* Make sure the player is logged in */
			if (!args.Player.IsLoggedIn)
			{
				args.Player.SendErrorMessage("You must be logged in to do that.");
				return;
			}
			/* Make sure the player isn't in a SetHome Disabled region */
			if (!args.Player.Group.HasPermission("essentials.home.bypassdisabled"))
			{
				foreach (string r in Config.DisableSetHomeInRegions)
				{
					var region = TShock.Regions.GetRegionByName(r);
					if (region == null) continue;
					if (args.Player.CurrentRegion == region)
					{
						args.Player.SendErrorMessage("You cannot set your home in this region.");
						return;
					}
				}

				// Disable setting homes in protected regions
				if (args.Player.CurrentRegion != null)
				{
					if (!(args.Player.CurrentRegion.HasPermissionToBuildInRegion(args.Player) || args.Player.Group.HasPermission(Permissions.editregion) || args.Player.Group.HasPermission(Permissions.manageregion)))
					{
						args.Player.SendErrorMessage("You cannot set your home in this region.");
						return;
					}
				}
			}

			/* get a list of the player's homes */
			List<string> homes = EsSQL.GetNames(args.Player.User.ID, Main.worldID);
			/* how many homes is the user allowed to set */
			int CanSet = EsUtils.NoOfHomesCanSet(args.Player);

			if (homes.Count < 1)
			{
				/* the player doesn't have a home, Create one! */
				if (args.Parameters.Count < 1 || (args.Parameters.Count > 0 && CanSet == 1))
				{
					/* they dont specify a name OR they specify a name but they only have permission to set 1, use a default name */
					if (EsSQL.AddHome(args.Player.User.ID, args.Player.TileX, args.Player.TileY, "1", Main.worldID))
						args.Player.SendSuccessMessage("Set home.");
					else
						args.Player.SendErrorMessage("An error occurred while setting your home.");
				}
				else if (args.Parameters.Count == 1 && !args.Parameters[0].Contains(" ") && (CanSet == -1 || CanSet > 1))
				{
					/* they specify a name And have permission to specify more than 1 */
					string name = args.Parameters[0].ToLower();
					if (EsSQL.AddHome(args.Player.User.ID, args.Player.TileX, args.Player.TileY, name, Main.worldID))
						args.Player.SendSuccessMessage("Set home {0}.", name);
					else
						args.Player.SendErrorMessage("An error occurred while setting your home.");
				}
				else
				{
					/* homes cant have more than 1 word */
					args.Player.SendErrorMessage("Invalid Syntax: /sethome <name>");
				}
			}
			else if (homes.Count == 1)
			{
				/* If they only have 1 home */
				if (args.Parameters.Count == 1 && !args.Parameters[0].Contains(" ") && (1 < CanSet || CanSet == -1))
				{
					/* They Specify a name and can set more than 1  */
					string name = args.Parameters[0].ToLower();
					if (homes.Contains(name))
					{
						/* They want to update a home */
						if (EsSQL.UpdateHome(args.Player.TileX, args.Player.TileY, args.Player.User.ID, name, Main.worldID))
							args.Player.SendSuccessMessage("Updated home {0}.", name);
						else
							args.Player.SendErrorMessage("An error occurred while updating your home.");
					}
					else
					{
						/* They want to add a new home */
						if (EsSQL.AddHome(args.Player.User.ID, args.Player.TileX, args.Player.TileY, name, Main.worldID))
							args.Player.SendSuccessMessage("Set home {0}.", name);
						else
							args.Player.SendErrorMessage("An error occurred while setting your home.");
					}
				}
				else if (args.Parameters.Count < 1 && (1 < CanSet || CanSet == -1))
				{
					/* if they dont specify a name & can set more than 1  - add a new home*/
					if (EsSQL.AddHome(args.Player.User.ID, args.Player.TileX, args.Player.TileY, EsUtils.NextHome(homes), Main.worldID))
						args.Player.SendSuccessMessage("Set home.");
					else
						args.Player.SendErrorMessage("An error occurred while setting your home.");
				}
				else if (args.Parameters.Count > 0 && CanSet == 1)
				{
					/* They specify a name but can only set 1 home, update their current home */
					if (EsSQL.UpdateHome(args.Player.TileX, args.Player.TileY, args.Player.User.ID, homes[0], Main.worldID))
						args.Player.SendSuccessMessage("Updated home.");
					else
						args.Player.SendErrorMessage("An error occurred while updating your home.");
				}
				else
				{
					/* homes cant have more than 1 word */
					args.Player.SendErrorMessage("Error: Homes cannot contain spaces.");
				}
			}
			else
			{
				/* If they have more than 1 home */
				if (args.Parameters.Count < 1)
				{
					/* they didnt specify a name */
					if (homes.Count < CanSet || CanSet == -1)
					{
						/* They can set more homes */
						if (EsSQL.AddHome(args.Player.User.ID, args.Player.TileX, args.Player.TileY, EsUtils.NextHome(homes), Main.worldID))
							args.Player.SendSuccessMessage("Set home.");
						else
							args.Player.SendErrorMessage("An error occurred while setting your home.");
					}
					else
					{
						/* they cant set any more homes */
						args.Player.SendErrorMessage("You are only allowed to set {0} homes.", CanSet.ToString());
						args.Player.SendErrorMessage("Homes: {0}", string.Join(", ", homes));
					}
				}
				else if (args.Parameters.Count == 1 && !args.Parameters[0].Contains(" "))
				{
					/* they want to set / update another home and specified a name */
					string name = args.Parameters[0].ToLower();
					if (homes.Contains(name))
					{
						/* they want to update a home */
						if (EsSQL.UpdateHome(args.Player.TileX, args.Player.TileY, args.Player.User.ID, name, Main.worldID))
							args.Player.SendSuccessMessage("Updated home.");
						else
							args.Player.SendErrorMessage("An error occurred while updating your home.");
					}
					else
					{
						/* they want to add a new home */
						if (homes.Count < CanSet || CanSet == -1)
						{
							/* they can set more homes */
							if (EsSQL.AddHome(args.Player.User.ID, args.Player.TileX, args.Player.TileY, name, Main.worldID))
								args.Player.SendSuccessMessage("Set home {0}.", name);
							else
								args.Player.SendErrorMessage("An error occurred while setting your home.");
						}
						else
						{
							/* they cant set any more homes */
							args.Player.SendErrorMessage("You are only allowed to set {0} homes.", CanSet.ToString());
							args.Player.SendErrorMessage("Homes: {0}", string.Join(", ", homes));
						}
					}
				}
				else
				{
					/* homes cant have more than 1 word */
					args.Player.SendErrorMessage("Error: Homes cannot contain spaces.");
				}
			}
		}

		private void CMDmyhome(CommandArgs args)
		{
			/* Chek if the player is a real player */
			if (!args.Player.RealPlayer)
			{
				args.Player.SendErrorMessage("You must be a real player.");
				return;
			}
			/* Make sure the player is logged in */
			if (!args.Player.IsLoggedIn)
			{
				args.Player.SendErrorMessage("You must be logged in to do that.");
				return;
			}

			/* get a list of the player's homes */
			List<string> homes = EsSQL.GetNames(args.Player.User.ID, Main.worldID);
			/* Set home position variable */
			Point homePos = Point.Zero;

			if (homes.Count < 1)
			{
				/* they do not have a home */
				args.Player.SendErrorMessage("You have not set a home. type /sethome to set one.");
				return;
			}
			else if (homes.Count == 1)
			{
				/* they have 1 home */
				homePos = EsSQL.GetHome(args.Player.User.ID, homes[0], Main.worldID);
			}
			else if (homes.Count > 1)
			{
				/* they have more than 1 home */
				if (args.Parameters.Count < 1)
				{
					/* they didnt specify the name */
					args.Player.SendErrorMessage("Usage: /myhome <home>");
					args.Player.SendErrorMessage("Homes: {0}", string.Join(", ", homes));
					return;
				}
				else if (args.Parameters.Count == 1 && !args.Parameters[0].Contains(" "))
				{
					string name = args.Parameters[0].ToLower();
					if (homes.Contains(name))
					{
						homePos = EsSQL.GetHome(args.Player.User.ID, name, Main.worldID);
					}
					else
					{
						/* could not find the name */
						args.Player.SendErrorMessage("Usage: /myhome <home>");
						args.Player.SendErrorMessage("Homes: {0}", string.Join(", ", homes));
						return;
					}
				}
				else
				{
					/* could not find the name */
					args.Player.SendErrorMessage("Usage: /myhome <home>");
					args.Player.SendErrorMessage("Homes: {0}", string.Join(", ", homes));
					return;
				}
			}

			/* teleport home */
			if (homePos == Point.Zero)
			{
				args.Player.SendErrorMessage("There is an error with your home.");
				return;
			}

			esPlayer ePly = esPlayers[args.Player.Index];
			if (ePly != null)
			{
				ePly.LastBackX = args.Player.TileX;
				ePly.LastBackY = args.Player.TileY;
				ePly.LastBackAction = BackAction.TP;
			}

			args.Player.Teleport(homePos.X * 16F, homePos.Y * 16F);
			args.Player.SendSuccessMessage("Teleported home.");
		}

		private void CMDdelhome(CommandArgs args)
		{
			/* Chek if the player is a real player */
			if (!args.Player.RealPlayer)
			{
				args.Player.SendErrorMessage("You must be a real player.");
				return;
			}
			/* Make sure the player is logged in */
			if (!args.Player.IsLoggedIn)
			{
				args.Player.SendErrorMessage("You must be logged in to do that.");
				return;
			}

			/* get a list of the player's homes */
			List<string> homes = EsSQL.GetNames(args.Player.User.ID, Main.worldID);

			if (homes.Count < 1)
			{
				/* they do not have a home */
				args.Player.SendErrorMessage("You have not set a home. type /sethome to set one.");
			}
			else if (homes.Count == 1)
			{
				/* they have 1 home */
				if (EsSQL.RemoveHome(args.Player.User.ID, homes[0], Main.worldID))
					args.Player.SendSuccessMessage("Removed home.");
				else
					args.Player.SendErrorMessage("An error occurred while removing your home.");
			}
			else if (homes.Count > 1)
			{
				/* they have more than 1 home */
				if (args.Parameters.Count < 1)
				{
					/* they didnt specify the name */
					args.Player.SendErrorMessage("Usage: /delhome <home>");
					args.Player.SendErrorMessage("Homes: {0}", string.Join(", ", homes));
				}
				else if (args.Parameters.Count == 1 && !args.Parameters[0].Contains(" "))
				{
					string name = args.Parameters[0].ToLower();
					if (homes.Contains(name))
					{
						if (EsSQL.RemoveHome(args.Player.User.ID, name, Main.worldID))
							args.Player.SendSuccessMessage("Removed home {0}.", name);
						else
							args.Player.SendErrorMessage("An error occurred while removing your home.");
					}
					else
					{
						/* could not find the name */
						args.Player.SendErrorMessage("Usage: /delhome <home>");
						args.Player.SendErrorMessage("Homes: {0}", string.Join(", ", homes));
					}
				}
				else
				{
					/* could not find the name */
					args.Player.SendErrorMessage("Usage: /delhome <home>");
					args.Player.SendErrorMessage("Homes: {0}", string.Join(", ", homes));
				}
			}
		}
		#endregion

		#region essentials
		private void CMDessentials(CommandArgs args)
		{
			esConfig.ReloadConfig(args);
		}
		#endregion

		#region Team Unlock
		private void CMDteamunlock(CommandArgs args)
		{
			if (!Config.LockRedTeam && !Config.LockGreenTeam && !Config.LockBlueTeam && !Config.LockYellowTeam)
			{
				args.Player.SendErrorMessage("Teams are not locked.");
				return;
			}

			if (args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage("Usage: /teamunlock <color> <password>");
				return;
			}

			string team = args.Parameters[0].ToLower();

			args.Parameters.RemoveAt(0);
			string Password = string.Join(" ", args.Parameters);

			var ePly = esPlayers[args.Player.Index];

			switch (team)
			{
				case "red":
					{
						if (Config.LockRedTeam)
						{
							if (Password == Config.RedTeamPassword && Config.RedTeamPassword != "")
							{
								args.Player.SendSuccessMessage("You can now join red team.");
								ePly.RedPassword = Password;
							}
							else
								args.Player.SendErrorMessage("Incorrect Password.");
						}
						else
							args.Player.SendErrorMessage("The red team isn't locked.");
					}
					break;
				case "green":
					{
						if (Config.LockGreenTeam)
						{
							if (Password == Config.GreenTeamPassword && Config.GreenTeamPassword != "")
							{
								args.Player.SendSuccessMessage("You can now join green team.");
								ePly.GreenPassword = Password;
							}
							else
								args.Player.SendErrorMessage("Incorrect Password.");
						}
						else
							args.Player.SendErrorMessage("The green team isn't locked.");
					}
					break;
				case "blue":
					{
						if (Config.LockBlueTeam)
						{
							if (Password == Config.BlueTeamPassword && Config.BlueTeamPassword != "")
							{
								args.Player.SendSuccessMessage("You can now join blue team.");
								ePly.BluePassword = Password;
							}
							else
								args.Player.SendErrorMessage("Incorrect Password.");
						}
						else
							args.Player.SendErrorMessage("The blue team isn't lock.");
					}
					break;
				case "yellow":
					{
						if (Config.LockYellowTeam)
						{
							if (Password == Config.YellowTeamPassword && Config.YellowTeamPassword != "")
							{
								args.Player.SendSuccessMessage("You can now join yellow team.");
								ePly.YellowPassword = Password;
							}
							else
								args.Player.SendErrorMessage("Incorrect Password.");
						}
						else
							args.Player.SendErrorMessage("The yellow team isn't locked.");
					}
					break;
				default:
					args.Player.SendErrorMessage("Usage: /teamunlock <red/green/blue/yellow> <password>");
					break;
			}
		}
		#endregion

		#region Last Command
		private void CMDequals(CommandArgs args)
		{
			var ePly = esPlayers[args.Player.Index];

			if (ePly.LastCMD == "/=" || ePly.LastCMD.StartsWith("/= "))
			{
				args.Player.SendErrorMessage("Error with last command.");
				return;
			}

			if (ePly.LastCMD == string.Empty)
				args.Player.SendErrorMessage("You have not entered a command yet.");
			else
				Commands.HandleCommand(args.Player, ePly.LastCMD);
		}
		#endregion

		#region Kill Reason
		private void CMDkillr(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Usage: /killr <player> <reason>");
				return;
			}

			var PlayersFound = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (PlayersFound.Count != 1)
			{
				args.Player.SendErrorMessage(PlayersFound.Count < 1 ? "No players matched." : "More than one player matched.");
				return;
			}

			var Ply = PlayersFound[0];
			args.Parameters.RemoveAt(0); //remove player name
			string reason = " " + string.Join(" ", args.Parameters);

			NetMessage.SendPlayerHurt(Ply.Index, PlayerDeathReason.ByCustomReason(reason), 15000, 1, false, false, 0);

			//NetMessage.SendData(26, -1, -1, reason, Ply.Index, 0f, 15000);
			args.Player.SendSuccessMessage("You just killed {0}.", Ply.Name);
		}
		#endregion

		#region Disable
		private void CMDdisable(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Usage: /disable <player/-list> [reason]");
				return;
			}

			if (args.Parameters[0].ToLower() == "-list")
			{
				List<string> Names = new List<string>(Disabled.Keys);
				if (Disabled.Count < 1)
					args.Player.SendSuccessMessage("There are currently no players disabled.", Color.MediumSeaGreen);
				else
					args.Player.SendSuccessMessage("Disabled Players: {0}", string.Join(", ", Names));
				return;
			}

			var PlayersFound = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (PlayersFound.Count < 1)
			{
				foreach (var pair in Disabled)
				{
					if (pair.Key.ToLower().Contains(args.Parameters[0].ToLower()))
					{
						Disabled.Remove(pair.Key);
						args.Player.SendSuccessMessage("{0} is no longer disabled (even though they aren't online).", pair.Key);
						return;
					}
				}
				args.Player.SendErrorMessage("No players matched.");
			}
			else if (PlayersFound.Count > 1)
			{
				args.Player.SendErrorMessage("More than one player matched.");
				return;
			}

			var tPly = PlayersFound[0];
			var ePly = esPlayers[tPly.Index];

			if (!Disabled.ContainsKey(tPly.Name))
			{
				string Reason = string.Empty;
				if (args.Parameters.Count > 1)
				{
					args.Parameters.RemoveAt(0);
					Reason = string.Join(" ", args.Parameters);
				}
				ePly.DisabledX = tPly.TileX;
				ePly.DisabledY = tPly.TileY;
				ePly.Disabled = true;
				ePly.Disable();
				ePly.LastDisabledCheck = DateTime.UtcNow;
				int[] pos = new int[2];
				pos[0] = ePly.DisabledX;
				pos[1] = ePly.DisabledY;
				Disabled.Add(tPly.Name, pos);
				args.Player.SendSuccessMessage("You disabled {0}, They can not be enabled until you type \"/disable {0}\".", tPly.Name);
				if (Reason == string.Empty)
					tPly.SendSuccessMessage("You have been disabled by {0}.", args.Player.Name);
				else
					tPly.SendSuccessMessage("You have been disabled by {0} for {1}.", args.Player.Name, Reason);
			}
			else
			{
				ePly.Disabled = false;
				ePly.DisabledX = -1;
				ePly.DisabledY = -1;

				Disabled.Remove(tPly.Name);

				args.Player.SendSuccessMessage("{0} is no longer disabled.", tPly.Name);
				tPly.SendSuccessMessage("You are no longer disabled.");
			}
		}
		#endregion

		#region Top, Up and Down
		private void CMDtop(CommandArgs args)
		{
			int Y = EsUtils.GetTop(args.Player.TileX);
			if (Y == -1)
			{
				args.Player.SendErrorMessage("You are already on the top.");
				return;
			}
			esPlayer ePly = esPlayers[args.Player.Index];
			if (ePly != null)
			{
				ePly.LastBackX = args.Player.TileX;
				ePly.LastBackY = args.Player.TileY;
				ePly.LastBackAction = BackAction.TP;
			}
			args.Player.Teleport(args.Player.TileX * 16F, Y * 16F);
			args.Player.SendSuccessMessage("Teleported you to the top.");
		}
		private void CMDup(CommandArgs args)
		{
			int levels = 1;
			if (args.Parameters.Count > 0 && !int.TryParse(args.Parameters[0], out levels))
			{
				args.Player.SendErrorMessage("Usage: /up [No. levels]");
				return;
			}

			int Y = EsUtils.GetUp(args.Player.TileX, args.Player.TileY);
			if (Y == -1)
			{
				args.Player.SendErrorMessage("You are already on the top.");
				return;
			}
			bool limit = false;
			for (int i = 1; i < levels; i++)
			{
				int newY = EsUtils.GetUp(args.Player.TileX, Y);
				if (newY == -1)
				{
					levels = i;
					limit = true;
					break;
				}
				Y = newY;
			}

			esPlayer ePly = esPlayers[args.Player.Index];
			if (ePly != null)
			{
				ePly.LastBackX = args.Player.TileX;
				ePly.LastBackY = args.Player.TileY;
				ePly.LastBackAction = BackAction.TP;
			}
			args.Player.Teleport(args.Player.TileX * 16F, Y * 16F);
			args.Player.SendSuccessMessage("Teleported you up {0} level(s).{1}", levels, limit ? " You cant go up any further." : string.Empty);
		}
		private void CMDdown(CommandArgs args)
		{
			int levels = 1;
			if (args.Parameters.Count > 0 && !int.TryParse(args.Parameters[0], out levels))
			{
				args.Player.SendErrorMessage("Usage: /down [No. levels]");
				return;
			}

			int Y = EsUtils.GetDown(args.Player.TileX, args.Player.TileY);
			if (Y == -1)
			{
				args.Player.SendErrorMessage("You are already on the bottom.");
				return;
			}
			bool limit = false;
			for (int i = 1; i < levels; i++)
			{
				int newY = EsUtils.GetDown(args.Player.TileX, Y);
				if (newY == -1)
				{
					levels = i;
					limit = true;
					break;
				}
				Y = newY;
			}

			esPlayer ePly = esPlayers[args.Player.Index];
			if (ePly != null)
			{
				ePly.LastBackX = args.Player.TileX;
				ePly.LastBackY = args.Player.TileY;
				ePly.LastBackAction = BackAction.TP;
			}
			args.Player.Teleport(args.Player.TileX * 16F, Y * 16F);
			args.Player.SendSuccessMessage("Teleported you down {0} level(s).{1}", levels, limit ? " You can't go down any further." : string.Empty);
		}
		#endregion

		#region Left & Right
		private void CMDleft(CommandArgs args)
		{
			int levels = 1;
			if (args.Parameters.Count > 0 && !int.TryParse(args.Parameters[0], out levels))
			{
				args.Player.SendErrorMessage("Usage: /left [No. times]");
				return;
			}

			int X = EsUtils.GetLeft(args.Player.TileX, args.Player.TileY);
			if (X == -1)
			{
				args.Player.SendErrorMessage("You cannot go any further left.");
				return;
			}
			bool limit = false;
			for (int i = 1; i < levels; i++)
			{
				int newX = EsUtils.GetLeft(X, args.Player.TileY);
				if (newX == -1)
				{
					levels = i;
					limit = true;
					break;
				}
				X = newX;
			}

			esPlayer ePly = esPlayers[args.Player.Index];
			if (ePly != null)
			{
				ePly.LastBackX = args.Player.TileX;
				ePly.LastBackY = args.Player.TileY;
				ePly.LastBackAction = BackAction.TP;
			}
			args.Player.Teleport(X * 16F, args.Player.TileY * 16F);
			args.Player.SendSuccessMessage("Teleported you to the left {0} time(s).{1}", levels, limit ? " You can't go any further." : string.Empty);
		}
		private void CMDright(CommandArgs args)
		{
			int levels = 1;
			if (args.Parameters.Count > 0 && !int.TryParse(args.Parameters[0], out levels))
			{
				args.Player.SendErrorMessage("Usage: /right [No. times]");
				return;
			}

			int X = EsUtils.GetRight(args.Player.TileX, args.Player.TileY);
			if (X == -1)
			{
				args.Player.SendErrorMessage("You cannot go any further right.");
				return;
			}
			bool limit = false;
			for (int i = 1; i < levels; i++)
			{
				int newX = EsUtils.GetRight(X, args.Player.TileY);
				if (newX == -1)
				{
					levels = i;
					limit = true;
					break;
				}
				X = newX;
			}

			esPlayer ePly = esPlayers[args.Player.Index];
			if (ePly != null)
			{
				ePly.LastBackX = args.Player.TileX;
				ePly.LastBackY = args.Player.TileY;
				ePly.LastBackAction = BackAction.TP;
			}
			args.Player.Teleport(X * 16F, args.Player.TileY * 16F);
			args.Player.SendSuccessMessage("Teleported you to the right {0} time(s).{1}", levels, limit ? " You can't go any further." : string.Empty);
		}
		#endregion

		#region ptime
		private void CMDptime(CommandArgs args)
		{
			if (!args.Player.Group.HasPermission("essentials.playertime.setother") && args.Parameters.Count != 1)
			{
				args.Player.SendErrorMessage("Usage: /ptime <day/night/noon/midnight/reset>");
				return;
			}
			else if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("Usage: /ptime <day/night/noon/midnight/reset> [player]");
				return;
			}

			var Ply = args.Player;
			if (args.Player.Group.HasPermission("essentials.playertime.setother") && args.Parameters.Count == 2)
			{
				var PlayersFound = TShock.Utils.FindPlayer(args.Parameters[1]);

				if (PlayersFound.Count > 1)
				{
					List<string> matches = new List<string>();
					PlayersFound.ForEach(pl => { matches.Add(pl.Name); });
					TShock.Utils.SendMultipleMatchError(args.Player, matches);
					return;
				}

				Ply = PlayersFound[0];
			}

			esPlayer ePly = esPlayers[Ply.Index];
			string Time = args.Parameters[0].ToLower();

			switch (Time)
			{
				case "day":
					{
						ePly.ptDay = true;
						ePly.ptTime = 150.0;
						Ply.SendData(PacketTypes.TimeSet, "", 0, Main.sunModY, Main.moonModY);
					}
					break;
				case "night":
					{
						ePly.ptDay = false;
						ePly.ptTime = 0.0;
						Ply.SendData(PacketTypes.TimeSet, "", 0, Main.sunModY, Main.moonModY);
					}
					break;
				case "noon":
					{
						ePly.ptDay = true;
						ePly.ptTime = 27000.0;
						Ply.SendData(PacketTypes.TimeSet, "", 0, Main.sunModY, Main.moonModY);
					}
					break;
				case "midnight":
					{
						ePly.ptDay = false;
						ePly.ptTime = 16200.0;
						Ply.SendData(PacketTypes.TimeSet, "", 0, Main.sunModY, Main.moonModY);
					}
					break;
				case "reset":
					{
						ePly.ptTime = -1.0;
						Ply.SendData(PacketTypes.TimeSet, "", 0, Main.sunModY, Main.moonModY);
						args.Player.SendSuccessMessage("{0} time is the same as the server.", Ply == args.Player ? "Your" : string.Concat(Ply.Name, "'s"));
						if (Ply != args.Player)
							Ply.SendSuccessMessage("{0} Set your time to the server time.", args.Player.Name);
					}
					return;
				default:
					args.Player.SendErrorMessage("Usage: /ptime <day/night/dusk/noon/midnight/reset> [player]");
					return;
			}
			args.Player.SendSuccessMessage("Set {0} time to {1}.", args.Player == Ply ? "your" : string.Concat(Ply.Name, "'s"), Time);
			if (Ply != args.Player)
				Ply.SendSuccessMessage("{0} set your time to {1}.", args.Player.Name, Time);
		}
		#endregion

		#region Ping
		private void CMDping(CommandArgs args)
		{
			args.Player.SendMessage("pong.", Color.White);
		}
		#endregion

		#region sudo
		private void CMDsudo(CommandArgs args)
		{
			if (args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage("Usage: /sudo <player> <command>");
				return;
			}

			var PlayersFound = TShock.Utils.FindPlayer(args.Parameters[0]);
			if (PlayersFound.Count != 1)
			{
				args.Player.SendErrorMessage(PlayersFound.Count < 1 ? "No Players matched." : "More than one player matched.");
				return;
			}

			var Ply = PlayersFound[0];
			if (Ply.Group.HasPermission("essentials.sudo.immune"))
			{
				args.Player.SendErrorMessage("You cannot force {0} to do a command.", Ply.Name);
				return;
			}
			TShockAPI.Group OldGroup = null;
			if (args.Player.Group.HasPermission("essentials.sudo.super"))
			{
				OldGroup = Ply.Group;
				Ply.Group = new SuperAdminGroup();
			}

			args.Parameters.RemoveAt(0);
			string command = string.Join(" ", args.Parameters);
			if (!command.StartsWith("/"))
				command = string.Concat("/", command);

			Commands.HandleCommand(Ply, command);
			args.Player.SendSuccessMessage("Forced {0} to execute: {1}", Ply.Name, command);

			if (OldGroup != null)
				Ply.Group = OldGroup;
		}
		#endregion

		#region Near
		private void CMDnear(CommandArgs args)
		{
			var Players = new Dictionary<string, int>();
			foreach (var ePly in esPlayers)
			{
				if (ePly == null || ePly.Index == args.Player.Index) continue;
				int x = Math.Abs(args.Player.TileX - ePly.TSPlayer.TileX);
				int y = Math.Abs(args.Player.TileY - ePly.TSPlayer.TileY);
				int h = (int)Math.Sqrt((double)(Math.Pow(x, 2) + Math.Pow(y, 2)));
				Players.Add(ePly.TSPlayer.Name, h);
			}
			if (Players.Count == 0)
			{
				args.Player.SendSuccessMessage("No players found.");
				return;
			}
			List<string> Names = new List<string>();
			Players.OrderBy(pair => pair.Value).ForEach(pair => Names.Add(pair.Key));
			List<string> Results = new List<string>();
			var Line = new StringBuilder();
			int Added = 0;
			for (int i = 0; i < Names.Count; i++)
			{
				if (Line.Length == 0)
					Line.Append(string.Format("{0}({1}m)", Names[i], Players[Names[i]]));
				else
					Line.Append(string.Format(", {0}({1}m)", Names[i], Players[Names[i]]));
				Added++;
				if (Added == 5)
				{
					Results.Add(Line.ToString());
					Line.Clear();
					Added = 0;
				}
			}
			if (Results.Count <= 6)
			{
				args.Player.SendInfoMessage("Nearby Players:");
				foreach (var Result in Results)
				{
					args.Player.SendSuccessMessage(Result);
				}
			}
			else
			{
				int page = 1;
				if (args.Parameters.Count > 0 && !int.TryParse(args.Parameters[0], out page))
					page = 1;
				page--;
				const int pagelimit = 6;

				int pagecount = Results.Count / pagelimit;
				if (page > pagecount)
				{
					args.Player.SendErrorMessage("Page number exceeds pages ({0}/{1}).", page + 1, pagecount + 1);
					return;
				}

				args.Player.SendInfoMessage("Nearby Players - Page {0} of {1} | /near [page]", page + 1, pagecount + 1);
				for (int i = (page * pagelimit); (i < ((page * pagelimit) + pagelimit)) && i < Results.Count; i++)
					args.Player.SendSuccessMessage(Results[i]);
			}
		}
		#endregion

		#region Nick
		private void CMDnick(CommandArgs args)
		{
			if (args.Parameters.Count != 1 && !args.Player.Group.HasPermission("essentials.nick.setother"))
			{
				args.Player.SendErrorMessage("Usage: /nick <nickname / off>");
				return;
			}
			else if ((args.Parameters.Count < 1 || args.Parameters.Count > 2) && args.Player.Group.HasPermission("essentials.nick.setother"))
			{
				args.Player.SendErrorMessage("Usage: /nick [player] <nickname / off>");
				return;
			}

			TSPlayer NickPly = args.Player;
			if (args.Parameters.Count > 1 && args.Player.Group.HasPermission("essentials.nick.setother"))
			{
				var PlayersFound = TShock.Utils.FindPlayer(args.Parameters[0]);
				if (PlayersFound.Count != 1)
				{
					args.Player.SendErrorMessage(PlayersFound.Count < 1 ? "No players matched." : "More than one player matched.");
					return;
				}
				NickPly = PlayersFound[0];
			}

			esPlayer eNickPly = esPlayers[NickPly.Index];

			bool self = NickPly == args.Player;

			string nickname = args.Parameters[args.Parameters.Count - 1];

			if (nickname.ToLower() == "off")
			{
				if (eNickPly.HasNickName)
				{
					EsSQL.RemoveNickname(NickPly.Name);

					eNickPly.HasNickName = false;
					eNickPly.Nickname = string.Empty;

					args.Player.SendSuccessMessage("Removed {0} nickname.", self ? "your" : string.Concat(NickPly.Name, "'s"));
					if (!self)
						NickPly.SendSuccessMessage("Your nickname was removed by {0}.", args.Player.Name);
				}
				else
				{
					args.Player.SendErrorMessage("{0} not have a nickname to remove.", self ? "You do" : string.Concat(NickPly.Name, " does"));
				}
				return;
			}

			/*System.Text.RegularExpressions.Regex alphanumeric = new System.Text.RegularExpressions.Regex("^[a-zA-Z0-9_ ]*$");
			if (!alphanumeric.Match(nickname).Success)
			{
				args.Player.SendErrorMessage("Nicknames must be Alphanumeric.");
				return;
			}*/

			if (!eNickPly.HasNickName)
			{
				eNickPly.OriginalName = NickPly.Name;
				eNickPly.HasNickName = true;
			}

			eNickPly.Nickname = nickname;
			if (EsSQL.GetNickname(NickPly.Name, out string curNickname))
				EsSQL.UpdateNickname(NickPly.Name, nickname);
			else
				EsSQL.AddNickname(NickPly.Name, nickname);

			args.Player.SendSuccessMessage("Set {0} nickname to '{1}'.", self ? "your" : string.Concat(eNickPly.OriginalName), nickname);
			if (!self)
				NickPly.SendSuccessMessage("{0} set your nickname to '{1}'.", args.Player.Name, nickname);
		}
		#endregion

		#region realname
		private void CMDrealname(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Usage: /realname <player/-all>");
				return;
			}
			string search = args.Parameters[0].ToLower();
			if (search == "-all")
			{
				List<string> Nicks = new List<string>();
				foreach (var player in esPlayers)
				{
					if (player == null || !player.HasNickName) continue;
					Nicks.Add(string.Concat(Config.PrefixNicknamesWith, player.Nickname, "(", player.OriginalName, ")"));
				}

				if (Nicks.Count < 1)
					args.Player.SendErrorMessage("No players online have nicknames.");
				else
					args.Player.SendSuccessMessage(string.Join(", ", Nicks));
				return;
			}
			if (search.StartsWith(Config.PrefixNicknamesWith))
				search = search.Remove(0, Config.PrefixNicknamesWith.Length);

			List<esPlayer> PlayersFound = new List<esPlayer>();
			foreach (var player in esPlayers)
			{
				if (player == null || !player.HasNickName) continue;
				if (player.Nickname.ToLower() == search)
				{
					PlayersFound = new List<esPlayer> { player };
					break;
				}
				else if (player.Nickname.ToLower().Contains(search))
					PlayersFound.Add(player);
			}
			if (PlayersFound.Count != 1)
			{
				args.Player.SendErrorMessage(PlayersFound.Count < 1 ? "No players matched." : "More than one player matched.");
				return;
			}

			esPlayer ply = PlayersFound[0];

			args.Player.SendSuccessMessage("The user '{0}' has the nickname '{1}'.", ply.OriginalName, ply.Nickname);

		}
		#endregion

		#region Force Login
		private void CMDforcelogin(CommandArgs args)
		{
			if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("Usage: /forcelogin <account> [player]");
				return;
			}
			var user = TShock.Users.GetUserByName(args.Parameters[0]);
			if (user == null)
			{
				args.Player.SendErrorMessage("User {0} does not exist.", args.Parameters[0]);
				return;
			}
			var group = TShock.Utils.GetGroup(user.Group);

			var PlayersFound = new List<TSPlayer>() { args.Player };
			if (args.Parameters.Count == 2)
			{
				PlayersFound = TShock.Utils.FindPlayer(args.Parameters[1]);
				if (PlayersFound.Count != 1)
				{
					args.Player.SendErrorMessage(PlayersFound.Count < 1 ? "No players matched." : "More than one player matched.");
					return;
				}
			}

			var Player = PlayersFound[0];
			Player.Group = group;
			Player.User = user;
			Player.IsLoggedIn = true;
			Player.IgnoreActionsForInventory = "none";

			Player.SendSuccessMessage("{0} in as {1}.", Player != args.Player ? string.Concat(args.Player.Name, " Logged you") : "Logged", user.Name);
			if (Player != args.Player)
				args.Player.SendSuccessMessage("Logged {0} in as {1}.", Player.Name, user.Name);
			TShock.Log.ConsoleInfo(string.Format("{0} forced logged in {1}as user: {2}.", args.Player.Name, args.Player != Player ? string.Concat(Player.Name, " ") : string.Empty, user.Name));
		}
		#endregion

		#region Invsee
		private void CMDinvsee(CommandArgs args)
		{
			if (!TShock.ServerSideCharacterConfig.Enabled)
			{
				args.Player.SendErrorMessage("Server Side Character must be enabled.");
				return;
			}
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Usage: /invsee <player name / -restore>");
				return;
			}
			var ePly = esPlayers[args.Player.Index];
			if (args.Parameters.Count == 1 && args.Parameters[0].ToLower() == "-restore")
			{
				if (ePly.InvSee == null)
				{
					args.Player.SendErrorMessage("You are not viewing another player's inventory.");
					return;
				}
				ePly.InvSee.RestoreCharacter(args.Player);
				ePly.InvSee = null;
				args.Player.SendSuccessMessage("Restored your inventory.");
				return;
			}
			var PlayersFound = TShock.Utils.FindPlayer(string.Join(" ", args.Parameters));
			if (PlayersFound.Count != 1)
			{
				args.Player.SendErrorMessage(PlayersFound.Count < 1 ? "No Players matched." : "More than one player matched.");
				return;
			}

			var PlayerChar = new PlayerData(args.Player);
			PlayerChar.CopyCharacter(args.Player);
			if (ePly.InvSee == null)
			{
				ePly.InvSee = PlayerChar;
			}

			var CopyChar = new PlayerData(PlayersFound[0]);
			CopyChar.CopyCharacter(PlayersFound[0]);
			CopyChar.health = PlayerChar.health;
			CopyChar.maxHealth = PlayerChar.maxHealth;
			CopyChar.mana = PlayerChar.mana;
			CopyChar.maxMana = PlayerChar.maxMana;
			CopyChar.spawnX = PlayerChar.spawnX;
			CopyChar.spawnY = PlayerChar.spawnY;
			CopyChar.RestoreCharacter(args.Player);

			args.Player.SendSuccessMessage("Copied {0}'s inventory", PlayersFound[0].Name);
		}
		#endregion

		#region Whois
		private void CMDwhois(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Usage: /whois [-eui] player name");
				args.Player.SendErrorMessage("Flags: ");
				args.Player.SendErrorMessage("-e Exact name search");
				args.Player.SendErrorMessage("-u User ID search");
				args.Player.SendErrorMessage("-i IP address search");
				return;
			}
			string Type = "\b";
			string Query = string.Empty;
			List<User_Obj> Results = new List<User_Obj>();
			switch (args.Parameters[0].ToUpper())
			{
				case "-E":
					{
						Type = "Exact name";
						args.Parameters.RemoveAt(0);
						Query = string.Join(" ", args.Parameters);
						var user = TShock.Users.GetUserByName(Query);
						if (user != null)
						{
							var iplist = JsonConvert.DeserializeObject<List<string>>(user.KnownIps);
							Results.Add(new User_Obj(-1, user.Name, iplist[0]));
						}
					}
					break;
				case "-U":
					{
						Type = "User ID";
						Query = args.Parameters[1];
						int id;
						if (!int.TryParse(Query, out id))
						{
							args.Player.SendWarningMessage("ID must be an integer.");
							return;
						}
						foreach (var tPly in TShock.Players)
						{
							if (tPly != null && tPly.User.ID == id)
							{
								Results.Add(new User_Obj(id, tPly.Name, tPly.IP));
							}
						}
					}
					break;
				case "-I":
					{
						Type = "IP";
						Query = args.Parameters[1];
						var ipUser = TShock.Users.GetUsers().Find(user => user.KnownIps.Contains(Query));
						if (ipUser != null)
						{
							var iplist = JsonConvert.DeserializeObject<List<string>>(ipUser.KnownIps);
							Results.Add(new User_Obj(-1, ipUser.Name, iplist[0]));
						}
					}
					break;
				default:
					{
						Query = string.Join(" ", args.Parameters);
						//Results = TShock.Utils.FindPlayer(Query);
					}
					break;
			}
			if (Results.Count < 1)
			{
				args.Player.SendErrorMessage("No matches for {0}", Query);
			}
			else if (Results.Count == 1)
			{
				args.Player.SendInfoMessage("Details of {0} search: {1}", Type, Query);

				List<TSPlayer> res = TShock.Utils.FindPlayer(Results[0].name);



				if (res.Count == 1)
				{
					var ply = res[0];

					args.Player.SendSuccessMessage("UserID: {0}, Registered Name: {1}",
					Results[0].UserID, Results[0].name);

					if (esPlayers[ply.Index].HasNickName)
						args.Player.SendSuccessMessage("Nickname: {0}{1}", Config.PrefixNicknamesWith,
							esPlayers[ply.Index].Nickname);

					args.Player.SendSuccessMessage("IP: {0}", Results[0].IP);
				}

				else
				{
					args.Player.SendSuccessMessage("Registered Name: {0}", Results[0].name);
					args.Player.SendSuccessMessage("IP: {0}", Results[0].IP);
				}

			}
			else
			{
				args.Player.SendWarningMessage("Matches: ({0}):", Results.Count);

				Results.ForEach(r =>
				{
					TShock.Players.ForEach(pl =>
					{
						if (r.name == pl.Name)
						{
							args.Player.SendInfoMessage("Online matches:");
							args.Player.SendSuccessMessage("({0}){1} (IP:{3})", pl.User.ID, pl.Name, pl.IP);
							Results.RemoveAll(result => result == r);
						}
					});

					args.Player.SendInfoMessage("Offline matches:");
					args.Player.SendSuccessMessage("{1} (IP:{3})", r.name, r.IP);
				});
			}
		}
		#endregion
	}

	public class User_Obj
	{
		public int UserID;
		public string name;
		public string IP;

		public User_Obj(int u, string n, string i)
		{
			UserID = u;
			name = n;
			IP = i;
		}
	}
}
