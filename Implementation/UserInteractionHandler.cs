﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DPoint = System.Drawing.Point;

using Terraria.Plugins.Common;

using TShockAPI;

namespace Terraria.Plugins.CoderCow.Protector {
  public class UserInteractionHandler: UserInteractionHandlerBase, IDisposable {
    #region [Property: PluginInfo]
    private readonly PluginInfo pluginInfo;

    protected PluginInfo PluginInfo {
      get { return this.pluginInfo; }
    }
    #endregion

    #region [Property: Config]
    private Configuration config;

    protected Configuration Config {
      get { return this.config; }
    }
    #endregion

    #region [Property: ServerMetadataHandler]
    private readonly ServerMetadataHandler serverMetadataHandler;

    protected ServerMetadataHandler ServerMetadataHandler {
      get { return this.serverMetadataHandler; }
    }
    #endregion

    #region [Property: WorldMetadata]
    private readonly WorldMetadata worldMetadata;

    protected WorldMetadata WorldMetadata {
      get { return this.worldMetadata; }
    }
    #endregion

    #region [Property: ProtectionManager]
    private readonly ProtectionManager protectionManager;

    protected ProtectionManager ProtectionManager {
      get { return this.protectionManager; }
    }
    #endregion

    #region [Property: PluginCooperationHandler]
    private readonly PluginCooperationHandler pluginCooperationHandler;

    public PluginCooperationHandler PluginCooperationHandler {
      get { return this.pluginCooperationHandler; }
    }
    #endregion

    #region [Property: ReloadConfigurationCallback]
    private Func<Configuration> reloadConfigurationCallback;

    protected Func<Configuration> ReloadConfigurationCallback {
      get { return this.reloadConfigurationCallback; }
    }
    #endregion

    
    #region [Method: Constructor]
    public UserInteractionHandler(
      PluginTrace trace, PluginInfo pluginInfo, Configuration config, ServerMetadataHandler serverMetadataHandler, 
      WorldMetadata worldMetadata, ProtectionManager protectionManager, PluginCooperationHandler pluginCooperationHandler, 
      Func<Configuration> reloadConfigurationCallback
    ): base(trace) {
      Contract.Requires<ArgumentNullException>(trace != null);
      Contract.Requires<ArgumentException>(!pluginInfo.Equals(PluginInfo.Empty));
      Contract.Requires<ArgumentNullException>(config != null);
      Contract.Requires<ArgumentNullException>(serverMetadataHandler != null);
      Contract.Requires<ArgumentNullException>(worldMetadata != null);
      Contract.Requires<ArgumentNullException>(protectionManager != null);
      Contract.Requires<ArgumentNullException>(pluginCooperationHandler != null);
      Contract.Requires<ArgumentNullException>(reloadConfigurationCallback != null);

      this.pluginInfo = pluginInfo;
      this.config = config;
      this.serverMetadataHandler = serverMetadataHandler;
      this.worldMetadata = worldMetadata;
      this.protectionManager = protectionManager;
      this.pluginCooperationHandler = pluginCooperationHandler;
      this.reloadConfigurationCallback = reloadConfigurationCallback;

      #region Command Setup
      base.RegisterCommand(
        new[] { "protector" }, this.RootCommand_Exec, this.RootCommand_HelpCallback
      );
      base.RegisterCommand(
        new[] { "protect", "pt" },
        this.ProtectCommand_Exec, this.ProtectCommand_HelpCallback, ProtectorPlugin.ManualProtect_Permission
      );
      base.RegisterCommand(
        new[] { "deprotect", "dp" },
        this.DeprotectCommand_Exec, this.DeprotectCommand_HelpCallback, ProtectorPlugin.ManualDeprotect_Permission
      );
      base.RegisterCommand(
        new[] { "protectioninfo", "pinfo", "pi" }, this.ProtectionInfoCommand_Exec, this.ProtectionInfoCommand_HelpCallback
      );
      base.RegisterCommand(
        new[] { "share" }, this.ShareCommand_Exec, this.ShareCommandHelpCallback
      );
      base.RegisterCommand(
        new[] { "unshare" }, this.UnshareCommand_Exec, this.UnshareCommand_HelpCallback
      );
      base.RegisterCommand(
        new[] { "sharepublic" }, this.SharePublicCommand_Exec, this.SharePublicCommandHelpCallback
      );
      base.RegisterCommand(
        new[] { "unsharepublic" }, this.UnsharePublicCommand_Exec, this.UnsharePublicCommand_HelpCallback
      );
      base.RegisterCommand(
        new[] { "sharegroup" }, this.ShareGroupCommand_Exec, this.ShareGroupCommand_HelpCallback, 
        ProtectorPlugin.ShareWithGroups_Permission
      );
      base.RegisterCommand(
        new[] { "unsharegroup" }, this.UnshareGroupCommand_Exec, this.UnshareGroup_HelpCallback, 
        ProtectorPlugin.ShareWithGroups_Permission
      );
      base.RegisterCommand(
        new[] { "lockchest", "lchest" },
        this.LockChestCommand_Exec, this.LockChestCommand_HelpCallback, ProtectorPlugin.Utility_Permission
      );
      base.RegisterCommand(
        new[] { "refillchest", "rchest" },
        this.RefillChestCommand_Exec, this.RefillChestCommand_HelpCallback, ProtectorPlugin.SetRefillChests_Permission
      );
      base.RegisterCommand(
        new[] { "refillchestmany", "rchestmany" },
        this.RefillChestManyCommand_Exec, this.RefillChestManyCommand_HelpCallback, ProtectorPlugin.Utility_Permission
      );
      base.RegisterCommand(
        new[] { "bankchest", "bchest" },
        this.BankChestCommand_Exec, this.BankChestCommand_HelpCallback, ProtectorPlugin.SetBankChests_Permission
      );
      #endregion
    }
    #endregion

    #region [Command Handling /protector]
    private void RootCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;
      
      base.StopInteraction(args.Player);

      if (args.Parameters.Count >= 1) {
        string subCommand = args.Parameters[0].ToLowerInvariant();

        if (this.TryExecuteSubCommand(subCommand, args))
          return;
      }

      args.Player.SendMessage(this.PluginInfo.ToString(), Color.White);
      args.Player.SendMessage(this.PluginInfo.Description, Color.White);
      args.Player.SendMessage(string.Empty, Color.Yellow);

      int playerProtectionCount = 0;
      lock (this.WorldMetadata.Protections) {
        foreach (KeyValuePair<DPoint,ProtectionEntry> protection in this.WorldMetadata.Protections) {
          if (protection.Value.Owner == args.Player.UserID)
            playerProtectionCount++;
        }
      }

      string statsMessage = string.Format(
        "You've created {0} of {1} possible protections so far.", playerProtectionCount, 
        this.Config.MaxProtectionsPerPlayerPerWorld
      );
      args.Player.SendMessage(statsMessage, Color.Yellow);
      args.Player.SendMessage("Type \"/protector commands\" to get a list of available commands.", Color.Yellow);
      args.Player.SendMessage("To get more general information about this plugin type \"/protector help\".", Color.Yellow);
    }

    private bool TryExecuteSubCommand(string commandNameLC, CommandArgs args) {
      switch (commandNameLC) {
        case "commands":
        case "cmds": {
          int pageNumber = 1;
          if (args.Parameters.Count > 1 && (!int.TryParse(args.Parameters[1], out pageNumber) || pageNumber < 1)) {
            args.Player.SendErrorMessage(string.Format("\"{0}\" is not a valid page number.", args.Parameters[1]));
            return true;
          }

          List<string> terms = new List<string>();
          if (args.Player.Group.HasPermission(ProtectorPlugin.ManualProtect_Permission))
            terms.Add("/protect");
          if (args.Player.Group.HasPermission(ProtectorPlugin.ManualDeprotect_Permission))
            terms.Add("/deprotect");
          terms.Add("/protectioninfo ");
          if (
            args.Player.Group.HasPermission(ProtectorPlugin.ChestSharing_Permission) ||
            args.Player.Group.HasPermission(ProtectorPlugin.SwitchSharing_Permission) ||
            args.Player.Group.HasPermission(ProtectorPlugin.OtherSharing_Permission)
          ) {
            terms.Add("/share");
            terms.Add("/unshare");
            terms.Add("/sharepublic");
            terms.Add("/unsharepublic");

            if (args.Player.Group.HasPermission(ProtectorPlugin.ShareWithGroups_Permission)) {
              terms.Add("/sharegroup");
              terms.Add("/unsharegroup");
            }
          }
          if (args.Player.Group.HasPermission(ProtectorPlugin.SetRefillChests_Permission)) {
            terms.Add("/refillchest");
            if (args.Player.Group.HasPermission(ProtectorPlugin.Utility_Permission))
              terms.Add("/refillchestmany");
          }
          if (args.Player.Group.HasPermission(ProtectorPlugin.SetBankChests_Permission))
            terms.Add("/bankchest");
          if (args.Player.Group.HasPermission(ProtectorPlugin.Utility_Permission)) {
            terms.Add("/lockchest");
            terms.Add("/protector removeemptychests");
            terms.Add("/protector summary");
          }
          if (args.Player.Group.HasPermission(ProtectorPlugin.Cfg_Permission)) {
            terms.Add("/protector importinfinitechests");
            terms.Add("/protector importinfinitesigns");
            terms.Add("/protector reloadconfig");
          }

          List<string> lines = PaginationUtil.BuildLinesFromTerms(terms);
          PaginationUtil.SendPage(args.Player, pageNumber, lines, new PaginationUtil.Settings {
            HeaderFormat = "Protector Commands (Page {0} of {1})",
            LineTextColor = Color.LightGray,
          });

          return true;
        }
        case "removeemptychests":
        case "cleanupchests": {
          if (!args.Player.Group.HasPermission(ProtectorPlugin.Utility_Permission)) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector removeemptychests (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector removeemptychests|cleanupchests", Color.White);
            args.Player.SendMessage("Removes all empty and unprotected chests from the world.", Color.LightGray);
            return true;
          }

          int cleanedUpChestsCount = 0;
          int cleanedUpInvalidChestDataCount = 0;
          for (int i = 0; i < Main.chest.Length; i++) {
            Chest tChest = Main.chest[i];
            if (tChest == null)
              continue;

            bool isEmpty = true;
            for (int j = 0; j < tChest.item.Length; j++) {
              if (tChest.item[j].stack > 0) {
                isEmpty = false;
                break;
              }
            }

            if (!isEmpty)
              continue;

            bool isInvalidEntry = false;
            DPoint chestLocation = new DPoint(tChest.x, tChest.y);
            if (TerrariaUtils.Tiles[chestLocation].active && TerrariaUtils.Tiles[chestLocation].type == (int)BlockType.Chest) {
              chestLocation = TerrariaUtils.Tiles.MeasureObject(chestLocation).OriginTileLocation;
              lock (this.WorldMetadata.Protections) {
                if (this.WorldMetadata.Protections.ContainsKey(chestLocation))
                  continue;
              }
            } else {
              Main.chest[i] = null;
              isInvalidEntry = true;
            }

            if (!isInvalidEntry) {
              WorldGen.KillTile(chestLocation.X, chestLocation.Y, false, false, true);
              TSPlayer.All.SendTileSquare(chestLocation, 4);
              cleanedUpChestsCount++;
            } else {
              cleanedUpInvalidChestDataCount++;
            }
          }

          args.Player.SendSuccessMessage(string.Format(
            "{0} empty and unprotected chests were removed. {1} invalid chest entries were removed.", 
            cleanedUpChestsCount, cleanedUpInvalidChestDataCount
          ));

          return true;
        }
        case "summary":
        case "stats": {
          if (!args.Player.Group.HasPermission(ProtectorPlugin.Cfg_Permission)) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector summary (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector summary|stats", Color.White);
            args.Player.SendMessage("Measures global world information regarding chests, signs, protections and bank chests.", Color.LightGray);
            return true;
          }

          int chestCount = Main.chest.Count(chest => chest != null);
          int signCount = Main.sign.Count(sign => sign != null);
          int protectionsCount = this.WorldMetadata.Protections.Count;
          int sharedProtectionsCount = this.WorldMetadata.Protections.Values.Count(p => p.IsShared);
          int refillChestsCount = this.WorldMetadata.Protections.Values.Count(p => p.RefillChestData != null);

          Dictionary<int,int> userProtectionCounts = new Dictionary<int,int>(100);
          lock (this.WorldMetadata.Protections) {
            foreach (ProtectionEntry protection in this.WorldMetadata.Protections.Values) {
              if (!userProtectionCounts.ContainsKey(protection.Owner))
                userProtectionCounts.Add(protection.Owner, 1);
              else
                userProtectionCounts[protection.Owner]++;
            }
          }
          int usersWhoReachedProtectionLimitCount = userProtectionCounts.Values.Count(
            protectionCount => protectionsCount == this.Config.MaxProtectionsPerPlayerPerWorld
          );

          int bankChestCount = this.ServerMetadataHandler.EnqueueGetBankChestCount().Result;
          int bankChestInstancesCount;
          lock (this.WorldMetadata.Protections) {
            bankChestInstancesCount = this.WorldMetadata.Protections.Values.Count(
              p => p.BankChestKey != BankChestDataKey.Invalid
            );
          }
          
          args.Player.SendInfoMessage(string.Format(
            "There are {0} of {1} chests and {2} of {3} signs in this world.", 
            chestCount, Main.chest.Length, signCount, Sign.maxSigns
          ));
          args.Player.SendInfoMessage(string.Format(
            "{0} protections are intact, {1} of them are shared with other players,",
            protectionsCount, sharedProtectionsCount
          ));
          args.Player.SendInfoMessage(string.Format(
            "{0} refill chests have been set up and {1} users reached their protection limit.",
            refillChestsCount, usersWhoReachedProtectionLimitCount
          ));
          args.Player.SendInfoMessage(string.Format(
            "The database holds {0} bank chests, {1} of them are instanced in this world.",
            bankChestCount, bankChestInstancesCount
          ));

          return true;
        }
        case "importinfinitechests": {
          if (!args.Player.Group.HasPermission(ProtectorPlugin.Cfg_Permission)) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector importinfinitechests (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector importinfinitechests", Color.White);
            args.Player.SendMessage("Attempts to import all chest data from the InfiniteChests' database.", Color.LightGray);
            args.Player.SendMessage("The InfiniteChests plugin must not be installed for this.", Color.LightGray);
            args.Player.SendMessage("Existing chest data will be overwritten, imported refill chests will", Color.LightGray);
            args.Player.SendMessage("loose their timer.", Color.LightGray);
            return true;
          }

          args.Player.SendInfoMessage("Importing InfiniteChests data...");
          this.PluginTrace.WriteLineInfo("Importing InfiniteChests data...");

          int importedChests;
          int overwrittenChests;
          int protectFailures;
          try {
            this.PluginCooperationHandler.InfiniteChests_ChestDataImport(
              this.ProtectionManager, out importedChests, out overwrittenChests, out protectFailures
            );
          } catch (FileNotFoundException ex) {
            args.Player.SendErrorMessage(string.Format("The \"{0}\" database file was not found.", ex.FileName));
            return true;
          }

          args.Player.SendSuccessMessage(string.Format(
            "Imported {0} chests. {1} chests were overwritten. Failed to protect {2} chests.", 
            importedChests, overwrittenChests, protectFailures
          ));
          args.Player.SendInfoMessage("If refill chests were imported they will have their timer removed and refill instantly now.");
          args.Player.SendInfoMessage("You might want to change that by using the /refillchest command.");
          args.Player.SendInfoMessage("Note that Protector can not take over the chest handling as long as Infinite Chests is installed.");

          return true;
        }
        case "importinfinitesigns": {
          if (!args.Player.Group.HasPermission(ProtectorPlugin.Cfg_Permission)) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector importinfinitesigns (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector importinfinitesigns", Color.White);
            args.Player.SendMessage("Attempts to import all sign data from the InfiniteSigns' database.", Color.LightGray);
            args.Player.SendMessage("The InfiniteSigns plugin must not be installed for this.", Color.LightGray);
            args.Player.SendMessage("Existing sign data will be overwritten.", Color.LightGray);
            return true;
          }

          args.Player.SendInfoMessage("Importing InfiniteSigns data...");
          this.PluginTrace.WriteLineInfo("Importing InfiniteSigns data...");

          int importedSigns;
          int protectFailures;
          try {
            this.PluginCooperationHandler.InfiniteSigns_SignDataImport(
              this.ProtectionManager, out importedSigns, out protectFailures
            );
          } catch (FileNotFoundException ex) {
            args.Player.SendErrorMessage(string.Format("The \"{0}\" database file was not found.", ex.FileName));
            return true;
          }

          args.Player.SendSuccessMessage(string.Format(
            "Imported {0} signs. Failed to protect {1} signs.", importedSigns, protectFailures
          ));

          return true;
        }
        case "reloadconfiguration":
        case "reloadconfig":
        case "reloadcfg": {
          if (!args.Player.Group.HasPermission(ProtectorPlugin.Cfg_Permission)) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector reloadconfiguration (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector reloadconfiguration|reloadconfig|reloadcfg", Color.White);
            args.Player.SendMessage("Reloads Protector's configuration file and applies all new settings.", Color.LightGray);
            args.Player.SendMessage("If the limit of bank chests was decreased then existing bank chests going", Color.LightGray);
            args.Player.SendMessage("over this limit will still be accessible until the server is restarted.", Color.LightGray);
            return true;
          }

          this.PluginTrace.WriteLineInfo("Reloading configuration file.");
          try {
            this.config = this.ReloadConfigurationCallback();
            this.PluginTrace.WriteLineInfo("Configuration file successfully reloaded.");

            if (args.Player != TSPlayer.Server)
              args.Player.SendSuccessMessage("Configuration file successfully reloaded.");
          } catch (Exception ex) {
            this.PluginTrace.WriteLineError(
              "Reloading the configuration file failed. Keeping old configuration. Exception details:\n{0}", ex
            );
          }

          return true;
        }
      }

      return false;
    }

    private void RootCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Protector Overview (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("This plugin provides players on TShock driven Terraria servers the possibility", Color.LightGray);
          args.Player.SendMessage("of taking ownership of certain objects or blocks, so that other players can not ", Color.LightGray);
          args.Player.SendMessage("change or use them.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("The content of a protected chest can not be altered by other players, protected ", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("switches can not be hit by other players, signs can not be edited, beds can not ", Color.LightGray);
          args.Player.SendMessage("be used, doors not used and even plants in protected clay pots can not be ", Color.LightGray);
          args.Player.SendMessage("harvested without owning the clay pot.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("For more information and support visit Protector's thread on the TShock forums.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Command Handling /protect]
    private void ProtectCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("+p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /protect [+p]");
          args.Player.SendInfoMessage("Type /protect help to get more help to this command.");
          return;
        }
      }

      PlayerCommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType == TileEditType.TileKill || 
          editType == TileEditType.TileKillNoItem || 
          editType == TileEditType.PlaceWire || 
          editType == TileEditType.DestroyWire
        ) {
          this.TryCreateProtection(playerLocal, location);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        } else if (editType == TileEditType.DestroyWall) {
          playerLocal.SendErrorMessage("Walls can not be protected.");

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        }

        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      Func<TSPlayer,DPoint,CommandInteractionResult> usageCallbackFunc = (playerLocal, location) => {
        this.TryCreateProtection(playerLocal, location);
        playerLocal.SendTileSquare(location, 3);

        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.SignReadCallback += usageCallbackFunc;
      interaction.ChestOpenCallback += usageCallbackFunc;
      interaction.HitSwitchCallback += usageCallbackFunc;
      interaction.SignEditCallback += (playerLocal, signIndex, location, newText) => {
        this.TryCreateProtection(playerLocal, location);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendErrorMessage("Waited too long. The next hit object or block will not be protected.");
      };

      args.Player.SendInfoMessage("Hit or use an object or block to protect it.");
    }

    private void ProtectCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /protect (Page 1 of 1)", Color.Lime);
          args.Player.SendMessage("/protect|pt [+p]", Color.White);
          args.Player.SendMessage("Protects the selected object or block.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("+p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Command Handling /deprotect]
    private void DeprotectCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("+p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /deprotect [+p]");
          args.Player.SendInfoMessage("Type /deprotect help to get more help to this command.");
          return;
        }
      }

      PlayerCommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType == TileEditType.TileKill || 
          editType == TileEditType.TileKillNoItem || 
          editType == TileEditType.PlaceWire || 
          editType == TileEditType.DestroyWire
        ) {
          this.TryRemoveProtection(playerLocal, location);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        } else if (editType == TileEditType.DestroyWall) {
          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
        }

        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      Func<TSPlayer,DPoint,CommandInteractionResult> usageCallbackFunc = (playerLocal, location) => {
        this.TryRemoveProtection(playerLocal, location);
        playerLocal.SendTileSquare(location, 3);

        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.SignReadCallback += usageCallbackFunc;
      interaction.ChestOpenCallback += usageCallbackFunc;
      interaction.HitSwitchCallback += usageCallbackFunc;
      interaction.SignEditCallback += (playerLocal, signIndex, location, newText) => {
        this.TryGetProtectionInfo(playerLocal, location);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendMessage("Waited too long. The next hit object or block will not be deprotected anymore.", Color.Red);
      };

      args.Player.SendInfoMessage("Hit or use a protected object or block to deprotect it.");
    }

    private void DeprotectCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /deprotect (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/deprotect|dp [+p]", Color.White);
          args.Player.SendMessage("Deprotects the selected object or block.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("+p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other Protector command is entered.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("Only the owner or an administrator can remove a protection. If the selected object", Color.LightGray);
          args.Player.SendMessage("is a bank chest, this bank chest instance will be removed from the world so that", Color.LightGray);
          args.Player.SendMessage("it might be instanced again.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Command Handling /protectioninfo]
    private void ProtectionInfoCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("+p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /protectioninfo [+p]");
          args.Player.SendInfoMessage("Type /protectioninfo help to get more help to this command.");
          return;
        }
      }

      PlayerCommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType == TileEditType.TileKill || 
          editType == TileEditType.TileKillNoItem || 
          editType == TileEditType.PlaceWire || 
          editType == TileEditType.DestroyWire
        ) {
          this.TryGetProtectionInfo(playerLocal, location);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        } else if (editType == TileEditType.DestroyWall) {
          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
        }

        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      Func<TSPlayer,DPoint,CommandInteractionResult> usageCallbackFunc = (playerLocal, location) => {
        this.TryGetProtectionInfo(playerLocal, location);
        playerLocal.SendTileSquare(location, 3);

        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.SignReadCallback += usageCallbackFunc;
      interaction.ChestOpenCallback += usageCallbackFunc;
      interaction.HitSwitchCallback += usageCallbackFunc;
      interaction.SignEditCallback += (playerLocal, signIndex, location, newText) => {
        this.TryGetProtectionInfo(playerLocal, location);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendMessage("Waited too long. No protection info for the next object or block being hit will be shown.", Color.Red);
      };
      
      args.Player.SendInfoMessage("Hit or use a protected object or block to get some info about it.");
    }

    private void ProtectionInfoCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /protectioninfo (Page 1 of 1)", Color.Lime);
          args.Player.SendMessage("/protectioninfo|pinfo|pi [+p]", Color.White);
          args.Player.SendMessage("Displays some information about the selected protection.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("+p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other Protector command is entered.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Command Handling /share]
    private void ShareCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (args.Parameters.Count < 1) {
        args.Player.SendErrorMessage("Proper syntax: /share <player name> [+p]");
        args.Player.SendInfoMessage("Type /share help to get more help to this command.");
        return;
      }

      bool persistentMode;
      string playerName;
      if (args.Parameters[args.Parameters.Count - 1].Equals("+p", StringComparison.InvariantCultureIgnoreCase)) {
        persistentMode = true;
        playerName = args.ParamsToSingleString(0, 1);
      } else {
        persistentMode = false;
        playerName = args.ParamsToSingleString();
      }
      
      TShockAPI.DB.User tsUser;
      if (!TShockEx.MatchUserByPlayerName(playerName, out tsUser, args.Player))
        return;

      this.StartShareCommandInteraction(args.Player, persistentMode, true, false, false, tsUser.ID, tsUser.Name);
    }

    private void ShareCommandHelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /share (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/share <player name> [+p]", Color.White);
          args.Player.SendMessage("Adds a player share to the selected protection.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("player name = The name of the player to be added. Can either be an exact user", Color.LightGray);
          args.Player.SendMessage("name or part of the name of a player being currently online.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("+p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Command Handling /unshare]
    private void UnshareCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (args.Parameters.Count < 1) {
        args.Player.SendErrorMessage("Proper syntax: /unshare <player name>");
        args.Player.SendErrorMessage("Type /unshare help to get more help to this command.");
        return;
      }

      bool persistentMode;
      string playerName;
      if (args.Parameters[args.Parameters.Count - 1].Equals("+p", StringComparison.InvariantCultureIgnoreCase)) {
        persistentMode = true;
        playerName = args.ParamsToSingleString(0, 1);
      } else {
        persistentMode = false;
        playerName = args.ParamsToSingleString();
      }

      TShockAPI.DB.User tsUser;
      if (!TShockEx.MatchUserByPlayerName(playerName, out tsUser, args.Player))
        return;

      this.StartShareCommandInteraction(args.Player, persistentMode, false, false, false, tsUser.ID, tsUser.Name);
    }

    private void UnshareCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /unshare (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/unshare <player name>", Color.White);
          args.Player.SendMessage("Removes a player share from the selected protection.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("+p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other Protector command is entered.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("player name = The name of the player to be added. Can either be an exact user", Color.LightGray);
          args.Player.SendMessage("name or part of the name of a player being currently online.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Command Handling /sharepublic]
    private void SharePublicCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("+p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /sharepublic [+p]");
          args.Player.SendInfoMessage("Type /sharepublic help to get more help to this command.");
          return;
        }
      }

      this.StartShareCommandInteraction(args.Player, persistentMode, true, false, true);
    }

    private void SharePublicCommandHelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /sharepublic (Page 1 of 1)", Color.Lime);
          args.Player.SendMessage("/sharepublic [+p]", Color.White);
          args.Player.SendMessage("Allows everyone to use the selected object.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("+p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Command Handling /unsharepublic]
    private void UnsharePublicCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("+p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /unsharepublic [+p]");
          args.Player.SendInfoMessage("Type /unsharepublic help to get more help to this command.");
          return;
        }
      }

      this.StartShareCommandInteraction(args.Player, persistentMode, false, false, true);
    }

    private void UnsharePublicCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /unsharepublic (Page 1 of 1)", Color.Lime);
          args.Player.SendMessage("/unsharepublic [+p]", Color.White);
          args.Player.SendMessage("Revokes the permission for everyone to use the selected object.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("+p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Command Handling /sharegroup]
    private void ShareGroupCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (args.Parameters.Count < 1) {
        args.Player.SendErrorMessage("Proper syntax: /sharegroup <group name>");
        args.Player.SendErrorMessage("Type /sharegroup help to get more help to this command.");
        return;
      }

      bool persistentMode;
      string groupName;
      if (args.Parameters[args.Parameters.Count - 1].Equals("+p", StringComparison.InvariantCultureIgnoreCase)) {
        persistentMode = true;
        groupName = args.ParamsToSingleString(0, 1);
      } else {
        persistentMode = false;
        groupName = args.ParamsToSingleString();
      }

      if (TShock.Utils.GetGroup(groupName) == null) {
        args.Player.SendErrorMessage(string.Format("The group \"{0}\" does not exist.", groupName));

        return;
      }

      this.StartShareCommandInteraction(args.Player, persistentMode, true, true, false, groupName, groupName);
    }

    private void ShareGroupCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /sharegroup (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/sharegroup <group name> [+p]", Color.White);
          args.Player.SendMessage("Adds a group share to the selected protection.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("group name = The name of the TShock group to be added.", Color.LightGray);
          args.Player.SendMessage("+p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          break;
        case 2:
          args.Player.SendMessage("     out or any other Protector command is entered.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Command Handling /unsharegroup]
    private void UnshareGroupCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (args.Parameters.Count < 1) {
        args.Player.SendErrorMessage("Proper syntax: /unsharegroup <groupname>");
        args.Player.SendErrorMessage("Type /unsharegroup help to get more help to this command.");
        return;
      }

      bool persistentMode;
      string groupName;
      if (args.Parameters[args.Parameters.Count - 1].Equals("+p", StringComparison.InvariantCultureIgnoreCase)) {
        persistentMode = true;
        groupName = args.ParamsToSingleString(0, 1);
      } else {
        persistentMode = false;
        groupName = args.ParamsToSingleString();
      }

      this.StartShareCommandInteraction(args.Player, persistentMode, false, true, false, groupName, groupName);
    }

    private void UnshareGroup_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /unsharegroup (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/unsharegroup <group name> [+p]", Color.White);
          args.Player.SendMessage("Removes a group share from the selected protection.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("group name = The name of the TShock group to be removed.", Color.LightGray);
          args.Player.SendMessage("+p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          break;
        case 2:
          args.Player.SendMessage("     out or any other Protector command is entered.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Method: StartShareCommandInteraction]
    private void StartShareCommandInteraction(
      TSPlayer player, bool isPersistent, bool isShareOrUnshare, bool isGroup, bool isShareAll, 
      object shareTarget = null, string shareTargetName = null
    ) {
      PlayerCommandInteraction interaction = this.StartOrResetCommandInteraction(player);
      interaction.DoesNeverComplete = isPersistent;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType == TileEditType.TileKill || 
          editType == TileEditType.TileKillNoItem || 
          editType == TileEditType.PlaceWire || 
          editType == TileEditType.DestroyWire
        ) {
          this.TryAlterProtectionShare(playerLocal, location, isShareOrUnshare, isGroup, isShareAll, shareTarget, shareTargetName);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        } else if (editType == TileEditType.DestroyWall) {
          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
        }

        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      Func<TSPlayer,DPoint,CommandInteractionResult> usageCallbackFunc = (playerLocal, location) => {
        this.TryAlterProtectionShare(playerLocal, location, isShareOrUnshare, isGroup, isShareAll, shareTarget, shareTargetName);
        playerLocal.SendTileSquare(location, 3);

        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.SignReadCallback += usageCallbackFunc;
      interaction.ChestOpenCallback += usageCallbackFunc;
      interaction.HitSwitchCallback += usageCallbackFunc;
      interaction.SignEditCallback += (playerLocal, signIndex, location, newText) => {
        this.TryAlterProtectionShare(playerLocal, location, isShareOrUnshare, isGroup, isShareAll, shareTarget, shareTargetName);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };

      interaction.TimeExpiredCallback += (playerLocal) => {
        if (isShareOrUnshare)
          playerLocal.SendMessage("Waited too long. No protection will be shared.", Color.Red);
        else
          playerLocal.SendMessage("Waited too long. No protection will be unshared.", Color.Red);
      };

      if (isShareOrUnshare)
        player.SendInfoMessage("Hit or use the protected object or block you want to share.");
      else
        player.SendInfoMessage("Hit or use the protected object or block you want to unshare.");
    }
    #endregion

    #region [Command Handling /lockchest]
    private void LockChestCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("+p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /lockchest [+p]");
          args.Player.SendInfoMessage("Type /lockchest help to get more help to this command.");
          return;
        }
      }

      PlayerCommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType == TileEditType.TileKill || 
          editType == TileEditType.TileKillNoItem || 
          editType == TileEditType.PlaceWire || 
          editType == TileEditType.DestroyWire
        ) {
          this.TryLockChest(playerLocal, location);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        }

        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      interaction.ChestOpenCallback += (playerLocal, location) => {
        this.TryLockChest(playerLocal, location);
        playerLocal.SendTileSquare(location, 3);

        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendErrorMessage("Waited too long. The next hit or opened chest will not be locked.");
      };

      args.Player.SendInfoMessage("Hit or open a chest to lock it.");
    }

    private void LockChestCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /lockchest (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/lockchest|/lchest [+p]", Color.White);
          args.Player.SendMessage("Locks the selected chest so that a key is needed to open it.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("+p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("Note that only gold- and shadow chests can be locked.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Command Handling /refillchest]
    private void RefillChestCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      bool? oneLootPerPlayer = null;
      int? lootLimit = null;
      TimeSpan? refillTime = null;
      bool invalidSyntax = (args.Parameters.Count == 0);
      if (!invalidSyntax) {
        int timeParameters = 0;
        for (int i = 0; i < args.Parameters.Count; i++) {
          string param = args.Parameters[i];
          if (param.Equals("+p", StringComparison.InvariantCultureIgnoreCase))
            persistentMode = true;
          else if (param.Equals("+ot", StringComparison.InvariantCultureIgnoreCase))
            oneLootPerPlayer = true;
          else if (param.Equals("-ot", StringComparison.InvariantCultureIgnoreCase))
            oneLootPerPlayer = false;
          else if (param.Equals("-ll", StringComparison.InvariantCultureIgnoreCase))
            lootLimit = -1;
          else if (param.Equals("+ll", StringComparison.InvariantCultureIgnoreCase)) {
            if (args.Parameters.Count - 1 == i) {
              invalidSyntax = true;
              break;
            }

            int lootTimeAmount;
            if (!int.TryParse(args.Parameters[i + 1], out lootTimeAmount) || lootTimeAmount < 0) {
              invalidSyntax = true;
              break;
            }

            lootLimit = lootTimeAmount;
            i++;
          } else {
            timeParameters++;
          }
        }

        if (!invalidSyntax && timeParameters > 0) {
          if (!TimeSpanEx.TryParseShort(
            args.ParamsToSingleString(0, args.Parameters.Count - timeParameters), out refillTime
          )) {
            invalidSyntax = true;
          }
        }
      }

      if (invalidSyntax) {
        args.Player.SendErrorMessage("Proper syntax: /refillchest [time] [+ot|-ot] [+ll amount|-ll] [+p]");
        args.Player.SendErrorMessage("Type /refillchest help to get more help to this command.");
        return;
      }

      PlayerCommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType == TileEditType.TileKill || 
          editType == TileEditType.TileKillNoItem || 
          editType == TileEditType.PlaceWire || 
          editType == TileEditType.DestroyWire
        ) {
          this.TrySetUpRefillChest(playerLocal, location, refillTime, oneLootPerPlayer, lootLimit);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        } else if (editType == TileEditType.DestroyWall) {
          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
        }

        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      interaction.ChestOpenCallback += (playerLocal, location) => {
        this.TrySetUpRefillChest(playerLocal, location, refillTime, oneLootPerPlayer, lootLimit);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendMessage("Waited too long. No refill chest will be created.", Color.Red);
      };

      args.Player.SendInfoMessage("Open a chest to convert it into a refill chest.");
    }

    private void RefillChestCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /refillchest (Page 1 of 5)", Color.Lime);
          args.Player.SendMessage("/refillchest|/rchest [time] [+ot|-ot] [+ll amount|-ll] [+p]", Color.White);
          args.Player.SendMessage("Converts a chest to a special chest which can automatically refill its content.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("time = Examples: 2h, 2h30m, 2h30m10s, 1d6h etc.", Color.LightGray);
          args.Player.SendMessage("+ot = The chest can only be looted a single time by each player.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("+ll amount = The chest can only be looted the given amount of times in total.", Color.LightGray);
          args.Player.SendMessage("+p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("If +ot or +ll is applied, a player must be registered with the server to loot it.", Color.LightGray);
          break;
        case 3:
          args.Player.SendMessage("To remove a feature from an existing refill chest, put a '-' before it:", Color.LightGray);
          args.Player.SendMessage("  /refillchest -ot", Color.White);
          args.Player.SendMessage("Removes the 'ot' feature from the selected chest.", Color.LightGray);
          args.Player.SendMessage("To remove the timer, simply leave the time parameter away.", Color.LightGray);
          args.Player.SendMessage("Example #1: Make a chest refill its contents after one hour and 30 minutes:", Color.LightGray);
          break;
        case 4:
          args.Player.SendMessage("  /refillchest 1h30m", Color.White);
          args.Player.SendMessage("Example #2: Make a chest one time lootable per player without a refill timer:", Color.LightGray);
          args.Player.SendMessage("  /refillchest +ot", Color.White);
          args.Player.SendMessage("Example #3: Make a chest one time lootable per player with a 30 minutes refill timer:", Color.LightGray);
          args.Player.SendMessage("  /refillchest 30m +ot", Color.White);
          break;
        case 5:
          args.Player.SendMessage("Example #4: Make a chest one time lootable per player and 10 times lootable in total:", Color.LightGray);
          args.Player.SendMessage("  /refillchest +ot +ll 10", Color.White);
          break;
      }
    }
    #endregion

    #region [Command Handling /refillchestmany]
    private void RefillChestManyCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (!args.Player.Group.HasPermission(ProtectorPlugin.SetRefillChests_Permission)) {
        args.Player.SendErrorMessage("You do not have the permission to set up refill chests.");
        return;
      }

      bool? oneLootPerPlayer = null;
      int? lootLimit = null;
      TimeSpan? refillTime = null;
      string selector = null;
      bool invalidSyntax = (args.Parameters.Count <= 1);
      if (!invalidSyntax) {
        selector = args.Parameters[0].ToLowerInvariant();

        int timeParameters = 0;
        for (int i = 1; i < args.Parameters.Count; i++) {
          string param = args.Parameters[i];
          if (param.Equals("+ot", StringComparison.InvariantCultureIgnoreCase))
            oneLootPerPlayer = true;
          else if (param.Equals("-ot", StringComparison.InvariantCultureIgnoreCase))
            oneLootPerPlayer = false;
          else if (param.Equals("-ll", StringComparison.InvariantCultureIgnoreCase))
            lootLimit = -1;
          else if (param.Equals("+ll", StringComparison.InvariantCultureIgnoreCase)) {
            if (args.Parameters.Count - 1 == i) {
              invalidSyntax = true;
              break;
            }

            int lootTimeAmount;
            if (!int.TryParse(args.Parameters[i + 1], out lootTimeAmount) || lootTimeAmount < 0) {
              invalidSyntax = true;
              break;
            }

            lootLimit = lootTimeAmount;
            i++;
          } else {
            timeParameters++;
          }
        }

        if (!invalidSyntax && timeParameters > 0) {
          if (!TimeSpanEx.TryParseShort(
            args.ParamsToSingleString(1, args.Parameters.Count - timeParameters - 1), out refillTime
          )) {
            invalidSyntax = true;
          }
        }
      }

      if (!invalidSyntax) {
        ChestKind chestKindToSelect = ChestKind.Unknown;
        switch (selector) {
          case "dungeon":
            chestKindToSelect = ChestKind.DungeonChest;
            break;
          case "sky":
            chestKindToSelect = ChestKind.SkyIslandChest;
            break;
          case "ocean":
            chestKindToSelect = ChestKind.OceanChest;
            break;
          case "shadow":
            chestKindToSelect = ChestKind.HellShadowChest;
            break;
          default:
            invalidSyntax = true;
            break;
        }

        if (!invalidSyntax && chestKindToSelect != ChestKind.Unknown) {
          int createdChestsCounter = 0;
          for (int i = 0; i < Main.chest.Length; i++) {
            Chest chest = Main.chest[i];
            if (chest == null)
              continue;

            DPoint chestLocation = new DPoint(chest.x, chest.y);
            if (!TerrariaUtils.Tiles[chestLocation].active || TerrariaUtils.Tiles[chestLocation].type != (int)BlockType.Chest)
              continue;

            if (TerrariaUtils.Tiles.GuessChestKind(chestLocation) != chestKindToSelect)
              continue;

            try {
              ProtectionEntry protection = this.ProtectionManager.CreateProtection(args.Player, chestLocation, false);
              protection.IsSharedWithAll = this.Config.AutoShareRefillChests;
            } catch (Exception ex) {
              Debug.WriteLine("Failed creating protection: " + ex);
            }

            try {
              this.ProtectionManager.SetUpRefillChest(args.Player, chestLocation, refillTime, oneLootPerPlayer, lootLimit);
              createdChestsCounter++;
            } catch (Exception ex) {
              Debug.WriteLine("Failed creating refill chest: " + ex);
            }
          }

          args.Player.SendSuccessMessage(string.Format("{0} refill chests were created.", createdChestsCounter));
        }
      }

      if (invalidSyntax) {
        args.Player.SendErrorMessage("Proper syntax: /refillchestmany <selector> [time] [[+ot]|[-ot]] [[+ll [amount]]|-ll]");
        args.Player.SendErrorMessage("Type /refillchestmany help to get more help to this command.");
        return;
      }
    }

    private void RefillChestManyCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /refillchestmany (Page 1 of 3)", Color.Lime);
          args.Player.SendMessage("/refillchestmany|/rchestmany <selector> [time] [+ot|-ot] [+ll amount|-ll]", Color.White);
          args.Player.SendMessage("Converts all selected chests to refill chests or alters them.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("selector = dungeon, sky, ocean or shadow", Color.LightGray);
          args.Player.SendMessage("time = Examples: 2h, 2h30m, 2h30m10s, 1d6h etc.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("+ot = The chest can only be looted a single time by each player.", Color.LightGray);
          args.Player.SendMessage("+ll = The chest can only be looted the given amount of times in total.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("This command is expected to be used on a fresh world, the specified selector might", Color.LightGray);
          args.Player.SendMessage("also select player chests. This is how chest kinds are distinguished:", Color.LightGray);
          break;
        case 3:
          args.Player.SendMessage("Dungeon = Locked gold chest with natural dungeon walls behind.", Color.LightGray);
          args.Player.SendMessage("Sky = Locked gold chest above surface level.", Color.LightGray);
          args.Player.SendMessage("Ocean = Unlocked submerged gold chest in the ocean biome.", Color.LightGray);
          args.Player.SendMessage("Shadow = Locked shadow chest in the world's last seventh.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("For more information about refill chests and their parameters type /help rchest.", Color.LightGray);
          break;
      }
    }
    #endregion

    #region [Command Handling /bankchest]
    private void BankChestCommand_Exec(CommandArgs args) {
      if (args.Parameters.Count < 1) {
        args.Player.SendErrorMessage("Proper syntax: /bankchest <number>");
        args.Player.SendErrorMessage("Type /bankchest help to get more help to this command.");
        return;
      }

      int chestIndex;
      if (!int.TryParse(args.Parameters[0], out chestIndex)) {
        args.Player.SendErrorMessage("The given prameter is not a valid number.");
        return;
      }

      bool hasNoBankChestLimits = args.Player.Group.HasPermission(ProtectorPlugin.NoBankChestLimits_Permision);
      if (
        chestIndex < 1 || (chestIndex > this.Config.MaxBankChestsPerPlayer && !hasNoBankChestLimits)
      ) {
        string messageFormat;
        if (!hasNoBankChestLimits)
          messageFormat = "The bank chest number must be between 1 to {0}.";
        else
          messageFormat = "The bank chest number must be greater than 1.";

        args.Player.SendErrorMessage(string.Format(messageFormat, this.Config.MaxBankChestsPerPlayer));
        return;
      }

      PlayerCommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType == TileEditType.TileKill || 
          editType == TileEditType.TileKillNoItem || 
          editType == TileEditType.PlaceWire || 
          editType == TileEditType.DestroyWire
        ) {
          this.TrySetUpBankChest(playerLocal, location, chestIndex);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        } else if (editType == TileEditType.DestroyWall) {
          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
        }

        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      interaction.ChestOpenCallback += (playerLocal, location) => {
        this.TrySetUpBankChest(playerLocal, location, chestIndex);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendMessage("Waited too long. No bank chest will be created.", Color.Red);
      };

      args.Player.SendInfoMessage("Open a chest to convert it into a bank chest.");
    }

    private void BankChestCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      int pageNumber;
      if (!PaginationUtil.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /bankchest (Page 1 of 5)", Color.Lime);
          args.Player.SendMessage("/bankchest|/bchest <number>", Color.White);
          args.Player.SendMessage("Converts a protected chest into a bank chest instance. bank chests store their content in a separate", Color.LightGray);
          args.Player.SendMessage("non world related database - their content remains the same, no matter what world they are instanced in.", Color.LightGray);
          args.Player.SendMessage("They are basically like piggy banks, but server sided.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("number = A 1-based number to uniquely identify the bank chest.", Color.LightGray);
          args.Player.SendMessage("Usually, the number '1' is assigned to the first created bank chest, '2' for the next etc.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("In order to be converted to a bank chest, a chest must be protected and the player has to own it.", Color.LightGray);
          args.Player.SendMessage("Also, if this is the first instance of a bank chest ever created, the content of the chest will", Color.LightGray);
          break;
        case 3:
          args.Player.SendMessage("be considered as the new bank chest content. If the bank chest with that number was already instanced", Color.LightGray);
          args.Player.SendMessage("before though, then the chest has to be empty so that it can safely be overwirrten by the bank chest's", Color.LightGray);
          args.Player.SendMessage("actual content.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("To remove a bank chest instance, simply /deprotect it.", Color.LightGray);
          break;
        case 4:
          args.Player.SendMessage("The amount of bank chests a player can own is usually limited by configuration, also an additional permission", Color.LightGray);
          args.Player.SendMessage("is required to share a bank chest with other players.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("Only one bank chest instance with the same number shall be present in one and the same world.", Color.White);
          break;
        case 5:
          args.Player.SendMessage("Example #1: Create a bank chest with the number 1:", Color.LightGray);
          args.Player.SendMessage("  /bankchest 1", Color.White);
          break;
      }
    }
    #endregion


    #region [Hook Handlers]
    public override bool HandleTileEdit(
      TSPlayer player, TileEditType editType, BlockType blockType, DPoint location, int objectStyle
    ) {
      if (this.IsDisposed)
        return false;
      if (base.HandleTileEdit(player, editType, blockType, location, objectStyle))
        return true;
      
      switch (editType) {
        case TileEditType.TileKill:
        case TileEditType.TileKillNoItem: {
          // Is the tile really going to be destroyed or just hit?
          if (blockType != 0)
            break;

          Tile protectedTile = null;

          foreach (ProtectionEntry protection in this.ProtectionManager.EnumerateProtectionEntries(location)) {
            if (
              protection.Owner == player.UserID || (
                this.Config.AutoDeprotectEverythingOnDestruction &&
                player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)
              )
            ) {
              Tile tileToDeprotect = TerrariaUtils.Tiles[protection.TileLocation];
              try {
                this.ProtectionManager.RemoveProtection(player, protection.TileLocation, false);
              } catch (InvalidBlockTypeException) {
                player.SendErrorMessage(string.Format(
                  "Protections for blocks of type {0} are not removeable.", TerrariaUtils.Tiles.GetBlockTypeName((BlockType)tileToDeprotect.type)
                ));
              }

              if (this.Config.NotifyAutoDeprotections) {
                player.SendWarningMessage(string.Format(
                  "The {0} is not protected anymore.", TerrariaUtils.Tiles.GetBlockTypeName((BlockType)tileToDeprotect.type)
                ));
              }
            } else {
              protectedTile = TerrariaUtils.Tiles[protection.TileLocation];
            }
          }

          if (protectedTile != null) {
            player.SendErrorMessage(string.Format(
              "The {0} is protected.", TerrariaUtils.Tiles.GetBlockTypeName((BlockType)protectedTile.type)
            ));

            player.SendTileSquare(location);
            return true;
          }
          
          break;
        }
        case TileEditType.PlaceWire:
        case TileEditType.DestroyWire:
          if (this.Config.AllowWiringProtectedBlocks)
            break;

          if (this.CheckProtected(player, location, false)) {
            player.SendTileSquare(location);
            return true;
          }

          break;
        case TileEditType.PlaceTile:
          // Fix: We do not allow chests to be placed on active stone to prevent players from using the chest duplication bugs.
          // Fix2: Don't allow on ice blocks either, you never know.
          if (blockType == BlockType.Chest) {
            for (int x = 0; x < 2; x++) {
              DPoint tileBeneathLocation = location.OffsetEx(x, 1);
              if (
                TerrariaUtils.Tiles[tileBeneathLocation].active && (
                  TerrariaUtils.Tiles[tileBeneathLocation].type == (int)BlockType.ActiveStone ||
                  TerrariaUtils.Tiles[tileBeneathLocation].type == (int)BlockType.IceBlock
                )
              ) {
                TSPlayer.All.SendData(PacketTypes.Tile, string.Empty, 0, location.X, location.Y);

                bool dummy;
                ChestStyle chestStyle = TerrariaUtils.Tiles.GetChestStyle(objectStyle, out dummy);
                int itemType = (int)TerrariaUtils.Tiles.GetItemTypeFromChestType(chestStyle);
                Item.NewItem(location.X * TerrariaUtils.TileSize, location.Y * TerrariaUtils.TileSize, 32, 32, itemType);

                player.SendErrorMessage("Chests can not be placed on active stone or ice blocks.");

                return true;
              }
            }
          }

          break;
      }
      
      return false;
    }

    // Called after (probably) all other plugin's tile edit handlers.
    public virtual bool HandlePostTileEdit(
      TSPlayer player, TileEditType editType, BlockType blockType, DPoint location, int objectStyle
    ) {
      if (this.IsDisposed || editType != TileEditType.PlaceTile)
        return false;
      if (!this.Config.AutoProtectedTiles[(int)blockType])
        return false;

      Task.Factory.StartNew(() => {
        Thread.Sleep(150);

        Tile tile = TerrariaUtils.Tiles[location];
        if (!tile.active)
          return;

        try {
          this.ProtectionManager.CreateProtection(player, location, false);
              
          if (this.Config.NotifyAutoProtections)
            player.SendSuccessMessage(string.Format("This {0} has been protected.", TerrariaUtils.Tiles.GetBlockTypeName((BlockType)tile.type)));
        } catch (PlayerNotLoggedInException) {
          player.SendWarningMessage(string.Format(
            "This {0} will not be protected because you're not logged in.", TerrariaUtils.Tiles.GetBlockTypeName((BlockType)tile.type)
          ));
        } catch (LimitEnforcementException) {
          player.SendWarningMessage(string.Format(
            "This {0} will not be protected because you've reached the protection limit.", TerrariaUtils.Tiles.GetBlockTypeName((BlockType)tile.type)
          ));
        } catch (TileProtectedException) {
          this.PluginTrace.WriteLineError("Error: A block was tried to be auto protected where tile placement should not be possible.");
        } catch (AlreadyProtectedException) {
          this.PluginTrace.WriteLineError("Error: A block was tried to be auto protected on the same position of an existing protection.");
        } catch (Exception ex) {
          this.PluginTrace.WriteLineError("Unexpected exception was thrown during auto protection: \n" + ex);
        }
      }, TaskCreationOptions.PreferFairness);

      return false;
    }

    public override bool HandleChestGetContents(TSPlayer player, DPoint location) {
      if (this.IsDisposed)
        return false;
      if (base.HandleChestGetContents(player, location))
        return true;
      if (!TerrariaUtils.Tiles[location].active)
        return true;
      if (this.Config.LoginRequiredForChestUsage && !player.IsLoggedIn) {
        player.SendErrorMessage("You have to be logged in to use chests.");
        return true;
      }
      
      ProtectionEntry protection = null;
      // Only need the first enumerated entry as we don't need the protections of adjacent blocks.
      foreach (ProtectionEntry enumProtection in this.ProtectionManager.EnumerateProtectionEntries(location)) {
        protection = enumProtection;
        break;
      }

      bool playerHasAccess = true;
      if (protection != null)
        playerHasAccess = this.ProtectionManager.CheckProtectionAccess(protection, player, false);

      if (!playerHasAccess)
        return true;
      
      DPoint chestLocation = TerrariaUtils.Tiles.MeasureObject(location).OriginTileLocation;
      int tChestIndex = Chest.FindChest(chestLocation.X, chestLocation.Y);
      if (tChestIndex == -1) {
        player.SendErrorMessage("The data record of this chest is missing. This world's data might be corrupted.");
        return true;
      }

      if (Chest.UsingChest(tChestIndex) != -1) {
        player.SendErrorMessage("Another player is already viewing the content of this chest.");
        return true;
      }
      
      if (protection != null && protection.RefillChestData != null) {
        RefillChestMetadata refillChest = protection.RefillChestData;
        if (this.CheckRefillChestLootability(refillChest, player)) {
          if (refillChest.OneLootPerPlayer)
            player.SendMessage("You can loot this chest one single time only.", Color.OrangeRed);
        } else {
          return true; 
        }

        if (refillChest.RefillTime != TimeSpan.Zero) {
          // TODO: Bad code, RefillTimers shouldn't be public at all.
          lock (this.ProtectionManager.RefillTimers) {
            if (this.ProtectionManager.RefillTimers.IsTimerRunning(refillChest.RefillTimer)) {
              TimeSpan timeLeft = (refillChest.RefillStartTime + refillChest.RefillTime) - DateTime.Now;
              player.SendMessage(string.Format("This chest will refill in {0}.", timeLeft.ToLongString()), Color.OrangeRed);
            }
          }
        } else {
          player.SendMessage("This chest will refill its content.", Color.OrangeRed);
        }
      }
      
      return false;
    }

    public virtual bool HandleChestModifySlot(TSPlayer player, short chestIndex, byte slotIndex, ItemMetadata newItem) {
      if (this.IsDisposed)
        return false;

      Chest chest = Main.chest[chestIndex];
      if (chest == null)
        return true;

      DPoint location = new DPoint(chest.x, chest.y);

      ProtectionEntry protection = null;
      // Only need the first enumerated entry as we don't need the protections of adjacent blocks.
      foreach (ProtectionEntry enumProtection in this.ProtectionManager.EnumerateProtectionEntries(location)) {
        protection = enumProtection;
        break;
      }

      bool playerHasAccess = true;
      if (protection != null)
        playerHasAccess = this.ProtectionManager.CheckProtectionAccess(protection, player, false);

      if (!playerHasAccess)
        return true;

      if (protection != null && protection.RefillChestData != null) {
        RefillChestMetadata refillChest = protection.RefillChestData;
        // The player who set up the refill chest or masters shall modify its contents.
        if (refillChest.Owner == player.UserID || player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)) {
          refillChest.RefillItems[slotIndex] = newItem;

          this.ProtectionManager.TryRefillChest(location, refillChest);

          if (refillChest.RefillTime == TimeSpan.Zero) {
            player.SendSuccessMessage("The content of this refill chest was updated.");
          } else {
            // TODO: Bad code, RefillTimers shouldn't be public at all.
            lock (this.ProtectionManager.RefillTimers) {
              if (this.ProtectionManager.RefillTimers.IsTimerRunning(refillChest.RefillTimer))
                this.ProtectionManager.RefillTimers.RemoveTimer(refillChest.RefillTimer);
            }

            player.SendSuccessMessage("The content of this refill chest was updated and the timer was reset.");
          }

          return false;
        }

        if (refillChest.OneLootPerPlayer || refillChest.RemainingLoots > 0) {
          Contract.Assert(refillChest.Looters != null);
          if (!refillChest.Looters.Contains(player.UserID)) {
            refillChest.Looters.Add(player.UserID);

            if (refillChest.RemainingLoots > 0)
              refillChest.RemainingLoots--;
          }
        }

        // As the first item is taken out, we start the refill timer.
        ItemMetadata oldItem = ItemMetadata.FromItem(chest.item[slotIndex]);
        if (newItem.Type == ItemType.None || (newItem.Type == oldItem.Type && newItem.StackSize <= oldItem.StackSize)) {
          // TODO: Bad code, refill timers shouldn't be public at all.
          lock (this.ProtectionManager.RefillTimers) {
            this.ProtectionManager.RefillTimers.StartTimer(refillChest.RefillTimer);
          }
        } else {
          player.SendErrorMessage("You can not put items into this chest.");
          return true;
        }
      } else if (protection != null && protection.BankChestKey != BankChestDataKey.Invalid) {
        BankChestDataKey bankChestKey = protection.BankChestKey;
        this.ServerMetadataHandler.EnqueueUpdateBankChestItem(bankChestKey, slotIndex, newItem);
      }

      return false;
    }

    public virtual bool HandleChestUnlock(TSPlayer player, DPoint chestLocation) {
      if (this.IsDisposed)
        return false;

      ProtectionEntry protection = null;
      // Only need the first enumerated entry as we don't need the protections of adjacent blocks.
      foreach (ProtectionEntry enumProtection in this.ProtectionManager.EnumerateProtectionEntries(chestLocation)) {
        protection = enumProtection;
        break;
      }
      if (protection == null)
        return false;

      bool undoUnlock = false;
      if (!this.ProtectionManager.CheckProtectionAccess(protection, player, false)) {
        player.SendErrorMessage("This chest is protected, you can't unlock it.");
        undoUnlock = true;
      }
      if (protection.RefillChestData != null && !this.CheckRefillChestLootability(protection.RefillChestData, player))
        undoUnlock = true;

      if (undoUnlock) {
        bool dummy;
        if (TerrariaUtils.Tiles.GetChestStyle(TerrariaUtils.Tiles[chestLocation], out dummy) == ChestStyle.GoldChest) {
          int itemIndex = Item.NewItem( 
            chestLocation.X * TerrariaUtils.TileSize, chestLocation.Y * TerrariaUtils.TileSize, 14, 20, (int)ItemType.GoldenKey
          );
          player.SendData(PacketTypes.ItemDrop, string.Empty, itemIndex);
        }

        player.SendTileSquare(chestLocation, 3);
        return true;
      }

      return false;
    }

    public override bool HandleSignEdit(TSPlayer player, short signIndex, DPoint location, string newText) {
      if (this.IsDisposed)
        return false;
      if (base.HandleSignEdit(player, signIndex, location, newText))
        return true;

      return this.CheckProtected(player, location, false);
    }

    public override bool HandleHitSwitch(TSPlayer player, DPoint location) {
      if (this.IsDisposed)
        return false;
      if (base.HandleHitSwitch(player, location))
        return true;
      
      if (this.CheckProtected(player, location, false)) {
        player.SendTileSquare(location, 3);
        return true;
      }

      return false;
    }

    public virtual bool HandleDoorUse(TSPlayer player, DPoint location, bool isOpening, Direction direction) {
      if (this.IsDisposed)
        return false;
      if (this.CheckProtected(player, location, false)) {
        player.SendTileSquare(location, 5);
        return true;
      }

      return false;
    }

    public virtual bool HandlePlayerSpawn(TSPlayer player, DPoint spawnTileLocation) {
      if (this.IsDisposed)
        return false;
      if (this.Config.EnableBedSpawnProtection) {
        DPoint bedTileLocation = new DPoint(spawnTileLocation.X, spawnTileLocation.Y - 1);
        
        Tile spawnTile = TerrariaUtils.Tiles[bedTileLocation];
        if (!spawnTile.active || spawnTile.type != (int)BlockType.Bed)
          return false;

        if (this.CheckProtected(player, bedTileLocation, false)) {
          player.SendErrorMessage("The bed you have set spawn at is protected, you can not teleport there.");
          player.SendErrorMessage("You were transported to your last valid spawn location instead.");

          if (player.TPlayer.SpawnX == -1 && player.TPlayer.SpawnY == -1)
            player.Teleport(Main.spawnTileX, Main.spawnTileY);
          else
            player.Teleport(player.TPlayer.SpawnX, player.TPlayer.SpawnY);

          return true;
        }
      }

      return false;
    }
    #endregion

    #region [Methods: TryCreateProtection, TryAlterProtectionShare, TryRemoveProtection, TryGetProtectionInfo, CheckProtected]
    private bool TryCreateProtection(TSPlayer player, DPoint tileLocation, bool sendFailureMessages = true) {
      if (!player.IsLoggedIn) {
        if (sendFailureMessages) {
          player.SendErrorMessage("Your character has to be registered with the server in order to create");
          player.SendErrorMessage("protections.");
        }

        return false;
      }

      try {
        this.ProtectionManager.CreateProtection(player, tileLocation);

        BlockType blockType = (BlockType)TerrariaUtils.Tiles[tileLocation].type;
        player.SendSuccessMessage(string.Format("This {0} is now protected.", TerrariaUtils.Tiles.GetBlockTypeName(blockType)));

        return true;
      } catch (ArgumentException ex) {
        if (ex.ParamName == "tileLocation") {
          if (sendFailureMessages)
            player.SendErrorMessage("Nothing to protect here.");

          return false;
        }

        throw;
      } catch (InvalidBlockTypeException ex) {
        if (sendFailureMessages) {
          string messageFormat;
          if (TerrariaUtils.Tiles.IsSolidBlockType(ex.BlockType, true))
            messageFormat = "Blocks of type {0} can not be protected.";
          else
            messageFormat = "Objects of type {0} can not be protected.";
        
          player.SendErrorMessage(string.Format(messageFormat, TerrariaUtils.Tiles.GetBlockTypeName(ex.BlockType)));
        }

        return false;
      } catch (LimitEnforcementException) {
        if (sendFailureMessages) {
          player.SendErrorMessage(
            string.Format("You can't create new protections because you've reached the maximum number of protections: {0}.", 
            this.Config.MaxProtectionsPerPlayerPerWorld)
          );
        }

        return false;
      } catch (AlreadyProtectedException) {
        if (sendFailureMessages) {
          BlockType blockType = (BlockType)TerrariaUtils.Tiles[tileLocation].type;
          player.SendErrorMessage(string.Format("This {0} is already protected.", TerrariaUtils.Tiles.GetBlockTypeName(blockType)));
        }

        return false;
      } catch (TileProtectedException) {
        if (sendFailureMessages) {
          BlockType blockType = (BlockType)TerrariaUtils.Tiles[tileLocation].type;
          player.SendErrorMessage(string.Format("This {0} is protected by someone else or is inside of a protected region.", TerrariaUtils.Tiles.GetBlockTypeName(blockType)));
        }

        return false;
      } catch (Exception ex) {
        player.SendErrorMessage("An unexpected internal error occured.");
        this.PluginTrace.WriteLineError("Error on creating protection: ", ex.ToString());

        return false;
      }
    }

    private bool TryAlterProtectionShare(
      TSPlayer player, DPoint tileLocation, bool isShareOrUnshare, bool isGroup, bool isShareAll, 
      object shareTarget, string shareTargetName, bool sendFailureMessages = true
    ) {
      if (!player.IsLoggedIn) {
        if (sendFailureMessages) {
          player.SendErrorMessage("Your character has to be registered with the server in order to alter");
          player.SendErrorMessage("protections.");
        }

        return false;
      }

      try {
        BlockType blockType = (BlockType)TerrariaUtils.Tiles[tileLocation].type;
        if (isShareAll) {
          this.ProtectionManager.ProtectionShareAll(player, tileLocation, isShareOrUnshare, true);

          if (isShareOrUnshare) {
            player.SendSuccessMessage(string.Format(
              "This {0} is now shared with everyone.", TerrariaUtils.Tiles.GetBlockTypeName(blockType)
            ));
          } else {
            player.SendSuccessMessage(string.Format(
              "This {0} is not shared with everyone anymore.", TerrariaUtils.Tiles.GetBlockTypeName(blockType)
            ));
          }
        } else if (!isGroup) {
          this.ProtectionManager.ProtectionShareUser(player, tileLocation, (int)shareTarget, isShareOrUnshare, true);

          if (isShareOrUnshare) {
            player.SendSuccessMessage(string.Format(
              "This {0} is now shared with player \"{1}\".", 
              TerrariaUtils.Tiles.GetBlockTypeName(blockType), shareTargetName
            ));
          } else {
            player.SendSuccessMessage(string.Format(
              "This {0} is not shared with player \"{1}\" anymore.", 
              TerrariaUtils.Tiles.GetBlockTypeName(blockType), shareTargetName
            ));
          }
        } else {
          this.ProtectionManager.ProtectionShareGroup(player, tileLocation, (string)shareTarget, isShareOrUnshare, true);

          if (isShareOrUnshare) {
            player.SendSuccessMessage(string.Format(
              "This {0} is now shared with group \"{1}\".", 
              TerrariaUtils.Tiles.GetBlockTypeName(blockType), shareTargetName
            ));
          } else {
            player.SendSuccessMessage(string.Format(
              "This {0} is not shared with group \"{1}\" anymore.", 
              TerrariaUtils.Tiles.GetBlockTypeName(blockType), shareTargetName
            ));
          }
        }

        return true;
      } catch (ProtectionAlreadySharedException) {
        string blockName = TerrariaUtils.Tiles.GetBlockTypeName((BlockType)TerrariaUtils.Tiles[tileLocation].type);

        if (isShareAll) {
          player.SendErrorMessage(string.Format("This {0} is already shared with everyone.", blockName));
        } else if (!isGroup) {
          player.SendErrorMessage(string.Format("This {0} is already shared with {1}.", blockName, shareTargetName));
        } else {
          player.SendErrorMessage(string.Format(
            "This {0} is already shared with group {1}.", blockName, shareTargetName
          ));
        }

        return false;
      } catch (InvalidBlockTypeException ex) {
        if (sendFailureMessages) {
          string messageFormat;
          if (TerrariaUtils.Tiles.IsSolidBlockType(ex.BlockType, true))
            messageFormat = "Protections of {0} blocks are not shareable.";
          else
            messageFormat = "Protections of {0} objects are not shareable.";
        
          player.SendErrorMessage(string.Format(messageFormat, TerrariaUtils.Tiles.GetBlockTypeName(ex.BlockType)));
        }

        return false;
      } catch (MissingPermissionException ex) {
        BlockType blockType = (BlockType)TerrariaUtils.Tiles[tileLocation].type;
        if (sendFailureMessages) {
          if (ex.Permission == ProtectorPlugin.BankChestShare_Permission) {
            player.SendErrorMessage("You're not allowed to share bank chests.");
          } else {
            player.SendErrorMessage(string.Format(
              "You're not allowed to share {0} objects.", TerrariaUtils.Tiles.GetBlockTypeName(blockType)
            ));
          }
        }
        
        return false;
      } catch (NoProtectionException) {
        BlockType blockType = (BlockType)TerrariaUtils.Tiles[tileLocation].type;
        if (sendFailureMessages) {
          player.SendErrorMessage(string.Format(
            "This {0} is not protected by Protector at all.", TerrariaUtils.Tiles.GetBlockTypeName(blockType)
          ));
        }
        
        return false;
      } catch (TileProtectedException) {
        if (sendFailureMessages)
          player.SendErrorMessage("You have to be the owner in order to alter shares of the protection.");

        return false;
      }
    }

    private bool TryRemoveProtection(TSPlayer player, DPoint tileLocation, bool sendFailureMessages = true) {
      if (!player.IsLoggedIn) {
        if (sendFailureMessages) {
          player.SendErrorMessage("Your character has to be registered with the server in order to alter");
          player.SendErrorMessage("protections.");
        }

        return false;
      }

      try {
        this.ProtectionManager.RemoveProtection(player, tileLocation);

        BlockType blockType = (BlockType)TerrariaUtils.Tiles[tileLocation].type;
        player.SendSuccessMessage(
          string.Format("This {0} is not protected anymore.", TerrariaUtils.Tiles.GetBlockTypeName(blockType))
        );

        return true;
      } catch (InvalidBlockTypeException ex) {
        if (sendFailureMessages) {
          string messageFormat;
          if (TerrariaUtils.Tiles.IsSolidBlockType(ex.BlockType, true))
            messageFormat = "Deprotecting {0} blocks is not allowed.";
          else
            messageFormat = "Deprotecting {0} objects is not allowed.";
        
          player.SendErrorMessage(string.Format(messageFormat, TerrariaUtils.Tiles.GetBlockTypeName(ex.BlockType)));
        }

        return false;
      } catch (NoProtectionException) {
        BlockType blockType = (BlockType)TerrariaUtils.Tiles[tileLocation].type;
        if (sendFailureMessages) {
          player.SendErrorMessage(string.Format(
            "This {0} is not protected by Protector at all.", TerrariaUtils.Tiles.GetBlockTypeName(blockType)
          ));
        }
        
        return false;
      } catch (TileProtectedException) {
        BlockType blockType = (BlockType)TerrariaUtils.Tiles[tileLocation].type;
        player.SendErrorMessage(string.Format(
          "This {0} is owned by someone else, you can't deprotect it.", TerrariaUtils.Tiles.GetBlockTypeName(blockType)
        ));

        return false;
      }
    }

    private bool TryGetProtectionInfo(TSPlayer player, DPoint tileLocation, bool sendFailureMessages = true) {
      Tile tile = TerrariaUtils.Tiles[tileLocation];
      if (!tile.active)
        return false;

      ProtectionEntry protection = null;
      // Only need the first enumerated entry as we don't need the protections of adjacent blocks.
      foreach (ProtectionEntry enumProtection in this.ProtectionManager.EnumerateProtectionEntries(tileLocation)) {
        protection = enumProtection;
        break;
      }

      BlockType blockType = (BlockType)TerrariaUtils.Tiles[tileLocation].type;
      if (protection == null) {
        if (sendFailureMessages) {
          player.SendErrorMessage(string.Format(
            "This {0} is not protected by Protector at all.", TerrariaUtils.Tiles.GetBlockTypeName(blockType)
          ));
        }
        
        return false;
      }

      bool canViewExtendedInfo = (
        player.Group.HasPermission(ProtectorPlugin.ViewAllProtections_Permission) ||
        protection.Owner == player.UserID ||
        protection.IsSharedWithPlayer(player)
      );
      
      if (!canViewExtendedInfo) {
        player.SendMessage(string.Format(
          "This {0} is protected and not shared with you.", TerrariaUtils.Tiles.GetBlockTypeName(blockType)
        ), Color.LightGray);

        player.SendWarningMessage("You are not permitted to get more information about this protection.");
        return true;
      }

      string ownerName;
      if (protection.Owner == -1) {
        ownerName = "{Server}";
      } else {
        TShockAPI.DB.User tsUser = TShock.Users.GetUserByID(protection.Owner);
        if (tsUser != null)
          ownerName = tsUser.Name;
        else
          ownerName = string.Concat("{deleted user id: ", protection.Owner, "}");
      }

      player.SendMessage(string.Format(
        "This {0} is protected. The owner is {1}.", TerrariaUtils.Tiles.GetBlockTypeName(blockType), ownerName
      ), Color.LightGray);

      player.SendMessage(
        string.Format(
          CultureInfo.InvariantCulture, "Protection created On: {0:MM/dd/yy, h:mm tt} UTC ({1} ago)", 
          protection.TimeOfCreation, (DateTime.UtcNow - protection.TimeOfCreation).ToLongString()
        ), 
        Color.LightGray
      );
      
      if (blockType == BlockType.Chest) {
        if (protection.RefillChestData != null) {
          RefillChestMetadata refillChest = protection.RefillChestData;
          if (refillChest.RefillTime != TimeSpan.Zero)
            player.SendMessage(string.Format("This is a refill chest with a timer set to {0}.", refillChest.RefillTime.ToLongString()), Color.LightGray);
          else
            player.SendMessage("This is a refill chest without a timer.", Color.LightGray);

          if (refillChest.OneLootPerPlayer || refillChest.RemainingLoots != -1) {
            StringBuilder messageBuilder = new StringBuilder();
            if (refillChest.OneLootPerPlayer)
              messageBuilder.Append("one time by each player");
            if (refillChest.RemainingLoots != -1) {
              if (messageBuilder.Length > 0)
                messageBuilder.Append(" and ");

              messageBuilder.Append(refillChest.RemainingLoots);
              messageBuilder.Append(" more times in total");
            }
            if (refillChest.Looters != null) {
              messageBuilder.Append(" and was looted ");
              messageBuilder.Append(refillChest.Looters.Count);
              messageBuilder.Append(" times until now");
            }

            messageBuilder.Insert(0, "It can only be looted ");
            messageBuilder.Append('.');

            player.SendMessage(messageBuilder.ToString(), Color.LightGray);
          }
        } else if (protection.BankChestKey != BankChestDataKey.Invalid) {
          BankChestDataKey bankChestKey = protection.BankChestKey;
          player.SendMessage(
            string.Format("This is a bank chest instance with the number {0}.", bankChestKey.BankChestIndex), Color.LightGray
          );
        }
      }
      
      if (ProtectionManager.IsShareableBlockType(blockType)) {
        if (protection.IsSharedWithAll) {
          player.SendMessage("Protection is shared with everyone.", Color.LightGray);
          return true;
        }

        StringBuilder sharedListBuilder = new StringBuilder();
        if (protection.SharedUsers != null) {
          for (int i = 0; i < protection.SharedUsers.Count; i++) {
            if (i > 0)
              sharedListBuilder.Append(", ");

            TShockAPI.DB.User tsUser = TShock.Users.GetUserByID(protection.SharedUsers[i]);
            if (tsUser != null)
              sharedListBuilder.Append(tsUser.Name);
          }
        }

        if (sharedListBuilder.Length == 0 && protection.SharedGroups == null) {
          player.SendMessage("Protection is not shared with users or groups.", Color.LightGray);
          return true;
        }
        
        if (sharedListBuilder.Length > 0)
          player.SendMessage("Shared with users: " + sharedListBuilder, Color.LightGray);
        else
          player.SendMessage("Protection is not shared with users.", Color.LightGray);

        if (protection.SharedGroups != null)
          player.SendMessage("Shared with groups: " + protection.SharedGroups.ToString(), Color.LightGray);
        else
          player.SendMessage("Protection is not shared with groups.", Color.LightGray);
      }

      return true;
    }

    private bool CheckProtected(TSPlayer player, DPoint tileLocation, bool fullAccessRequired) {
      if (!TerrariaUtils.Tiles[tileLocation].active)
        return false;

      ProtectionEntry protection;
      if (this.ProtectionManager.CheckBlockAccess(player, tileLocation, fullAccessRequired, out protection))
        return false;

      Tile protectedTile = TerrariaUtils.Tiles[protection.TileLocation];
      player.SendErrorMessage(string.Format(
        "This {0} is protected.", TerrariaUtils.Tiles.GetBlockTypeName((BlockType)protectedTile.type)
      ));

      return true;
    }
    #endregion

    #region [Methods: TryLockChest, TrySetUpRefillChest, TrySetUpBankChest]
    public bool TryLockChest(TSPlayer player, DPoint anyChestTileLocation, bool sendMessages = true) {
      try {
        TerrariaUtils.Tiles.LockChest(anyChestTileLocation);
        return true;
      } catch (ArgumentException) {
        player.SendErrorMessage("There is no chest here.");
        return false;
      } catch (InvalidChestStyleException) {
        player.SendErrorMessage("The chest must be an unlocked gold- or shadow chest.");
        return false;
      }
    }

    public bool TrySetUpRefillChest(
      TSPlayer player, DPoint tileLocation, TimeSpan? refillTime, bool? oneLootPerPlayer, int? lootLimit, 
      bool sendMessages = true
    ) {
      if (!player.IsLoggedIn) {
        if (sendMessages) {
          player.SendErrorMessage("Your character has to be registered with the server in order to set up");
          player.SendErrorMessage("refill chests.");
        }

        return false;
      }

      try {
        if (this.ProtectionManager.SetUpRefillChest(
          player, tileLocation, refillTime, oneLootPerPlayer, lootLimit, 
          true)
        ) {
          if (sendMessages) {
            player.SendSuccessMessage("Refill chest successfully set up.");
            player.SendSuccessMessage("As you are the owner of it, you may still freely modify its contents.");
          }
        } else {
          if (sendMessages) {
            if (refillTime != null) {
              if (refillTime != TimeSpan.Zero)
                player.SendSuccessMessage(string.Format("Set the refill timer of this chest to {0}.", refillTime.Value.ToLongString()));
              else
                player.SendSuccessMessage("This chest will now refill instantly.");
            }
            if (oneLootPerPlayer != null) {
              if (oneLootPerPlayer.Value)
                player.SendSuccessMessage("This chest can now be looted one single time by each player.");
              else
                player.SendSuccessMessage("This chest can now be looted freely.");
            }
            if (lootLimit != null) {
              if (lootLimit.Value != -1)
                player.SendSuccessMessage(string.Format("This chest can now be looted only {0} more times.", lootLimit));
              else
                player.SendSuccessMessage("This chest can now be looted endlessly.");
            }
          }
        }

        if (this.Config.AutoShareRefillChests) {
          foreach (ProtectionEntry protection in this.ProtectionManager.EnumerateProtectionEntries(tileLocation)) {
            protection.IsSharedWithAll = true;
            break;
          }
        }

        return true;
      } catch (ArgumentException ex) {
        if (ex.ParamName == "tileLocation") {
          if (sendMessages)
            player.SendErrorMessage("There is no chest here.");

          return false;
        }

        throw;
      } catch (MissingPermissionException) {
        if (sendMessages)
          player.SendErrorMessage("You are not allowed to define refill chests.");

        return false;
      } catch (NoProtectionException) {
        if (sendMessages)
          player.SendErrorMessage("The chest needs to be protected to be converted to a refill chest.");

        return false;
      } catch (TileProtectedException) {
        if (sendMessages)
          player.SendErrorMessage("You do not own the protection of this chest.");

        return false;
      } catch (ChestIncompatibilityException) {
        if (sendMessages)
          player.SendErrorMessage("A chest can not be a refill- and bank chest at the same time.");

        return false;
      } catch (NoChestDataException) {
        if (sendMessages) {
          player.SendErrorMessage("Error: There are no chest data for this chest available. This world's data might be");
          player.SendErrorMessage("corrupted.");
        }

        return false;
      }
    }
    
    public bool TrySetUpBankChest(TSPlayer player, DPoint tileLocation, int bankChestIndex, bool sendMessages = true) {
      if (!player.IsLoggedIn) {
        if (sendMessages) {
          player.SendErrorMessage("Your character has to be registered with the server in order to set up");
          player.SendErrorMessage("bank chests.");
        }
        
        return false;
      }
      
      try {
        this.ProtectionManager.SetUpBankChest(player, tileLocation, bankChestIndex, true);

        player.SendSuccessMessage(string.Format(
          "This chest is now an instance of your bank chest with the number {0}.", bankChestIndex
        ));

        return true;
      } catch (ArgumentException ex) {
        if (ex.ParamName == "tileLocation") {
          if (sendMessages)
            player.SendErrorMessage("There is no chest here.");

          return false;
        } else if (ex.ParamName == "bankChestIndex") {
          if (sendMessages) {
            string messageFormat;
            if (!player.Group.HasPermission(ProtectorPlugin.NoBankChestLimits_Permision))
              messageFormat = "The bank chest number must be between 1 to {0}.";
            else
              messageFormat = "The bank chest number must be greater than 1.";

            player.SendErrorMessage(string.Format(messageFormat, this.Config.MaxBankChestsPerPlayer));
          }

          return false;
        }

        throw;
      } catch (MissingPermissionException) {
        if (sendMessages)
          player.SendErrorMessage("You are not allowed to define bank chests.");

        return false;
      } catch (NoProtectionException) {
        if (sendMessages)
          player.SendErrorMessage("The chest needs to be protected to be converted to a bank chest.");

        return false;
      } catch (TileProtectedException) {
        if (sendMessages)
          player.SendErrorMessage("You do not own the protection of this chest.");

        return false;
      } catch (ChestNotEmptyException) {
        if (sendMessages)
          player.SendErrorMessage("The chest has to be empty in order to restore a bank chest here.");

        return false;
      } catch (ChestTypeAlreadyDefinedException) {
        if (sendMessages)
          player.SendErrorMessage("The chest is already a bank chest.");

        return false;
      } catch (ChestIncompatibilityException) {
        if (sendMessages)
          player.SendErrorMessage("A chest can not be a Bank- and Refill- or One Time Loot chest at the same time.");

        return false;
      } catch (NoChestDataException) {
        if (sendMessages) {
          player.SendErrorMessage("Error: There are no chest data for this chest available. This world's data might be");
          player.SendErrorMessage("corrupted.");
        }

        return false;
      } catch (BankChestAlreadyInstancedException) {
        if (sendMessages) {
          player.SendErrorMessage(string.Format("There is already an instance of your bank chest with the index {0} in", bankChestIndex));
          player.SendErrorMessage("this world.");
        }

        return false;
      }
    }
    #endregion

    #region [Method: EnsureProtectionData]
    public void EnsureProtectionData(TSPlayer player) {
      int invalidProtectionsCount;
      int invalidRefillChestCount;
      int invalidBankChestCount;

      this.ProtectionManager.EnsureProtectionData(
        out invalidProtectionsCount, out invalidRefillChestCount, out invalidBankChestCount
      );

      if (invalidProtectionsCount > 0)
        this.PluginTrace.WriteLineWarning(string.Format("{0} invalid protections removed.", invalidProtectionsCount));
      if (invalidRefillChestCount > 0)
        this.PluginTrace.WriteLineWarning(string.Format("{0} invalid refill chests removed.", invalidRefillChestCount));
      if (invalidBankChestCount > 0)
        this.PluginTrace.WriteLineWarning(string.Format("{0} invalid bank chest instances removed.", invalidBankChestCount));

      this.PluginTrace.WriteLineInfo("Finished ensuring protection data.");
    }
    #endregion

    #region [Method: CheckRefillChestLootability]
    public bool CheckRefillChestLootability(RefillChestMetadata refillChest, TSPlayer player, bool sendReasonMessages = true) {
      if (!player.IsLoggedIn && (refillChest.OneLootPerPlayer || refillChest.RemainingLoots != -1)) {
        if (sendReasonMessages)
          player.SendErrorMessage("You have to be registered in order to use this chest.");

        return false;
      }

      if (player.UserID != refillChest.Owner && !player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)) {
        if (refillChest.RemainingLoots == 0) {
          if (sendReasonMessages)
            player.SendErrorMessage("This chest has a loot limit attached to it and can not be looted anymore.");

          return false;
        }

        if (refillChest.OneLootPerPlayer) {
          Contract.Assert(refillChest.Looters != null);

          if (refillChest.Looters.Contains(player.UserID)) {
            if (sendReasonMessages)
              player.SendErrorMessage("This chest can only be looted on single time per player and you have looted it already.");

            return false;
          }
        }
      }

      return true;
    }
    #endregion

    #region [IDisposable Implementation]
    protected override void Dispose(bool isDisposing) {
      if (this.IsDisposed)
        return;
      
      if (isDisposing)
        this.reloadConfigurationCallback = null;

      base.Dispose(isDisposing);
    }
    #endregion
  }
}