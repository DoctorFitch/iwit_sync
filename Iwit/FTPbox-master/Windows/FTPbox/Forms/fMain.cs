/* License
 * This file is part of iwitSync - Copyright (C) 2015-2016
 * iwitSync is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published 
 * by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed 
 * in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU General Public License for more details. You should have received a copy of the GNU General Public License along with this program. 
 * If not, see <http://www.gnu.org/licenses/>.
 */
/* fMain.cs
 * The main form of the application (options form)
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using Microsoft.Win32;
using System.IO.Pipes;
using FTPboxLib;
using SystemTrayNotification;
using IWshRuntimeLibrary;
using System.Resources;
using iwitSync.Properties;
using System.Globalization;

namespace FTPbox.Forms
{
    public partial class fMain : Form
    {
        public bool gotpaths = false;                       //if the paths have been set or checked
        private bool changedfromcheck = true;
        //Form instances
        private Setup fSetup;
        private Translate ftranslate;
        private fSelectiveSync fSelective;
        private fTrayForm fTrayForm;
        private TrayTextNotificationArgs _lastTrayStatus = new TrayTextNotificationArgs { AssossiatedFile = null, MessageType = MessageType.AllSynced };
        
        private System.Threading.Timer tRetry;

        //Links
        public string link = string.Empty;                  //The web link of the last-changed file
        public string locLink = string.Empty;               //The local path to the last-changed file

        public fMain()
        {
            InitializeComponent();
            PopulateLanguages();
            cAuto.Checked = true;
        }

        /// <summary>
        /// Permet de faire apparaitre le controle de maintenance apres combinaison de touches (CTRL + SHIFT + M + L)
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Shift | Keys.M | Keys.L))
            {
                tabMaintenance.Visible = true;
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        private void fMain_Load(object sender, EventArgs e)
        {
            NetworkChange.NetworkAddressChanged += OnNetworkChange;

            //TODO: Should this stay?
            Program.Account.LoadLocalFolders();

            if (!Log.DebugEnabled && FTPboxLib.Settings.General.EnableLogging)
                Log.DebugEnabled = true;

            Notifications.NotificationReady += (o, n) =>
                {
                    link = Program.Account.LinkToRecent();
                    tray.ShowBalloonTip(100, n.Title, n.Text, ToolTipIcon.Info);
                };

            Program.Account.Client.ConnectionClosed += (o, n) => Log.Write(l.Warning, "Connexion fermée: {0}", n.Text ?? string.Empty);

            Program.Account.Client.ReconnectingFailed += (o, n) => Log.Write(l.Warning, "Echec de la reconnexion"); //TODO: Use this...

            Program.Account.Client.ValidateCertificate += CheckCertificate;

            Program.Account.WebInterface.UpdateFound += (o, n) =>
                {
                    const string msg = "Une nouvelle version d'iwit sync est disponible, voulez vous la télécharger ?";
                    if (MessageBox.Show(msg, "iwit sync - iwit sync Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        Program.Account.WebInterface.UpdatePending = true;
                        Program.Account.WebInterface.Update();
                    }
                };
            Program.Account.WebInterface.InterfaceRemoved += (o, n) =>
                {
                    this.Invoke(new MethodInvoker(() =>
                    {
                        chkWebInt.Enabled = true;
                        labViewInBrowser.Enabled = false;
                    }));
                    link = string.Empty;
                };
            Program.Account.WebInterface.InterfaceUploaded += (o, n) =>
                {
                    this.Invoke(new MethodInvoker(() =>
                    {
                        chkWebInt.Enabled = true;
                        labViewInBrowser.Enabled = true;
                    }));
                    link = Program.Account.WebInterfaceLink;
                };

            Notifications.TrayTextNotification += (o, n) => this.Invoke(new MethodInvoker(() => SetTray(o, n)));

            fSetup = new Setup { Tag = this };
            ftranslate = new Translate { Tag = this };
            fSelective = new fSelectiveSync();
            fTrayForm = new fTrayForm() { Tag = this };

            if (!string.IsNullOrEmpty(FTPboxLib.Settings.General.Language))
                Set_Language(FTPboxLib.Settings.General.Language);

            StartUpWork();

            CheckForUpdate();
        }

        /// <summary>
        /// Work done at the application startup. 
        /// Checks the saved account info, updates the form controls and starts syncing if syncing is automatic.
        /// If there's no internet connection, puts the program to offline mode.
        /// </summary>
        private void StartUpWork()
        {
            Log.Write(l.Debug, "Connexion internet disponible: {0}", ConnectedToInternet().ToString());
            OfflineMode = false;

            if (ConnectedToInternet())
            {
                CheckAccount();

                this.Invoke(new MethodInvoker(UpdateDetails));

                if (OfflineMode) return;

                Log.Write(l.Debug, "Compte: OK");

                CheckPaths();
                Log.Write(l.Debug, "Paths: OK");

                this.Invoke(new MethodInvoker(UpdateDetails));

                if (!FTPboxLib.Settings.IsNoMenusMode)
                {
                    RunServer();
                }
            }
            else
            {
                OfflineMode = true;
                SetTray(null, new TrayTextNotificationArgs { MessageType = MessageType.Offline });
            }
        }

        /// <summary>
        /// checks if account's information used the last time has changed
        /// </summary>
        private void CheckAccount()
        {
            if (!Program.Account.isAccountSet || Program.Account.isPasswordRequired)
            {
                Log.Write(l.Info, "Vas ouvrir une nouvelle fenetre.");
                Setup.JustPassword = Program.Account.isPasswordRequired;

                fSetup.ShowDialog();

                Log.Write(l.Info, "Done");

                this.Show();
            }
            else if (Program.Account.isAccountSet)
                try
                {
                    Program.Account.Client.Connect();

                    this.Invoke(new MethodInvoker(() =>
                    {
                        this.ShowInTaskbar = false;
                        this.Hide();
                        this.ShowInTaskbar = true;
                    }));
                }
                catch (Exception ex)
                {
                    Log.Write(l.Warning, "Connexion echouée, nous allons réesayer dans 30 secondes...");
                    Common.LogError(ex);

                    OfflineMode = true;
                    SetTray(null, new TrayTextNotificationArgs { MessageType = MessageType.Offline });

                    tRetry = new System.Threading.Timer(state => this.StartUpWork(), null, 30000, 0);
                }
        }

        /// <summary>
        /// checks if paths used the last time still exist
        /// </summary>
        public void CheckPaths()
        {
            if (!Program.Account.isPathsSet)
            {
                fSetup.ShowDialog();
                this.Show();

                if (!gotpaths)
                {
                    Log.Write(l.Debug, "bb cruel world");
                    KillTheProcess();
                }
            }
            else
                gotpaths = true;

            Program.Account.LoadLocalFolders();
        }

        /// <summary>
        /// Updates the form's labels etc
        /// </summary>
        public void UpdateDetails()
        {
            Log.Write(l.Debug, "Updating the form details");

            chkStartUp.Checked = CheckStartup();

            chkShowNots.Checked = FTPboxLib.Settings.General.Notifications;
            chkEnableLogging.Checked = FTPboxLib.Settings.General.EnableLogging;
            chkShellMenus.Checked = FTPboxLib.Settings.General.AddContextMenu;

            if (FTPboxLib.Settings.General.TrayAction == TrayAction.OpenInBrowser)
                rOpenInBrowser.Checked = true;
            else if (FTPboxLib.Settings.General.TrayAction == TrayAction.CopyLink)
                rCopy2Clipboard.Checked = true;
            else
                rOpenLocal.Checked = true;

            //  Account Tab     //

            cProfiles.Items.Clear();
            cProfiles.Items.AddRange(FTPboxLib.Settings.ProfileTitles);
            cProfiles.SelectedIndex = FTPboxLib.Settings.General.DefaultProfile;

            if (Program.Account.Account.SyncDirection == SyncDirection.Both)
                rBothWaySync.Checked = true;
            else if (Program.Account.Account.SyncDirection == SyncDirection.Remote)
                rLocalToRemoteOnly.Checked = true;
            else
                rRemoteToLocalOnly.Checked = true;

            tTempPrefix.Text = Program.Account.Account.TempFilePrefix;

            //  About Tab       //



            //   Filters Tab    //

            cIgnoreDotfiles.Checked = Program.Account.IgnoreList.IgnoreDotFiles;
            cIgnoreTempFiles.Checked = Program.Account.IgnoreList.IgnoreTempFiles;

            //  Bandwidth tab   //

            nSyncFrequency.Value = Convert.ToDecimal(Program.Account.Account.SyncFrequency);
            if (nSyncFrequency.Value == 0) nSyncFrequency.Value = 10;

            if (Program.Account.Account.SyncMethod == SyncMethod.Automatic)
                cAuto.Checked = true;
            else if (Program.Account.Account.SyncMethod == SyncMethod.Manual)
                cManually.Checked = true;

            if (Program.Account.Account.Protocol != FtpProtocol.SFTP)
            {
                if (LimitUpSpeed())
                    nUpLimit.Value = Convert.ToDecimal(FTPboxLib.Settings.General.UploadLimit);
                if (LimitDownSpeed())
                    nDownLimit.Value = Convert.ToDecimal(FTPboxLib.Settings.General.DownloadLimit);
            }
            else
                gLimits.Visible = false;

            Set_Language(FTPboxLib.Settings.General.Language);

            // Disable the following in offline mode
            chkWebInt.Enabled = !OfflineMode;
            SyncToolStripMenuItem.Enabled = !OfflineMode;

            if (OfflineMode || !gotpaths) return;

            bool e = Program.Account.WebInterface.Exists;
            chkWebInt.Checked = e;
            labViewInBrowser.Enabled = e;
            changedfromcheck = false;

            Program.Account.FolderWatcher.Setup();

            // in a separate thread...
            new Thread(() =>
            {
                // ...check local folder for changes
                string cpath = Program.Account.GetCommonPath(Program.Account.Paths.Local, true);
                Program.Account.SyncQueue.Add(new SyncQueueItem(Program.Account)
                    {
                        Item = new ClientItem
                            {
                                FullPath = Program.Account.Paths.Local,
                                Name = Common._name(cpath),
                                Type = ClientItemType.Folder,
                                Size = 0x0,
                                LastWriteTime = DateTime.MinValue
                            },
                        ActionType = ChangeAction.changed,
                        SyncTo = SyncTo.Remote
                    });
            }).Start();
        }

        /// <summary>
        /// Fill the combo-box of available translations.
        /// </summary>
        private void PopulateLanguages()
        {
            cLanguages.Items.Clear();
            cLanguages.Items.AddRange(Common.FormattedLanguageList);
            // Default to English
            cLanguages.SelectedIndex = Common.SelectedLanguageIndex;

            cLanguages.SelectedIndexChanged += cLanguages_SelectedIndexChanged;
        }

        /// <summary>
        /// Kills the current process. Called from the tray menu.
        /// </summary>
        public void KillTheProcess()
        {
            if (!FTPboxLib.Settings.IsNoMenusMode)
                RemoveFTPboxMenu();

            ExitedFromTray = true;
            Log.Write(l.Info, "Tuage du processus en cours...");

            try
            {
                tray.Visible = false;
                Process.GetCurrentProcess().Kill();
            }
            catch
            {
                Application.Exit();
            }
        }

        #region translations

        /// <summary>
        /// Translate all controls and stuff to the given language.
        /// </summary>
        /// <param name="lan">The language to translate to in 2-letter format</param>
        private void Set_Language(string lan)
        {
            FTPboxLib.Settings.General.Language = lan;
            Log.Write(l.Debug, "Changement du langage en : {0}", lan);

            this.Text = "iwit sync | " + Common.Languages[UiControl.Options];
            //general tab
            tabGeneral.Text = Common.Languages[UiControl.General];
            gLinks.Text = Common.Languages[UiControl.Links];
            labLinkClicked.Text = Common.Languages[UiControl.WhenRecentFileClicked];
            rOpenInBrowser.Text = Common.Languages[UiControl.OpenUrl];
            rCopy2Clipboard.Text = Common.Languages[UiControl.CopyUrl];
            rOpenLocal.Text = Common.Languages[UiControl.OpenLocal];

            chkShowNots.Text = Common.Languages[UiControl.ShowNotifications];
            chkStartUp.Text = Common.Languages[UiControl.StartOnStartup];
            chkEnableLogging.Text = Common.Languages[UiControl.EnableLogging];
            bBrowseLogs.Text = Common.Languages[UiControl.ViewLog];
            chkShellMenus.Text = Common.Languages[UiControl.AddShellMenu];

            //account tab
            bAddAccount.Text = Common.Languages[UiControl.Add];
            bRemoveAccount.Text = Common.Languages[UiControl.Remove];
            bConfigureAccount.Text = Common.Languages[UiControl.Details];
            chkWebInt.Text = Common.Languages[UiControl.UseWebUi];
            labViewInBrowser.Text = Common.Languages[UiControl.ViewInBrowser];
            rLocalToRemoteOnly.Text = Common.Languages[UiControl.LocalToRemoteSync];
            rRemoteToLocalOnly.Text = Common.Languages[UiControl.RemoteToLocalSync];
            rBothWaySync.Text = Common.Languages[UiControl.BothWaysSync];
            labTempPrefix.Text = Common.Languages[UiControl.TempNamePrefix];

            //filters tab
            gFileFilters.Text = Common.Languages[UiControl.Filters];
            bConfigureSelectiveSync.Text = Common.Languages[UiControl.Configure];
            bConfigureExtensions.Text = Common.Languages[UiControl.Configure];
            labSelectiveSync.Text = Common.Languages[UiControl.SelectiveSync];
            labSelectExtensions.Text = Common.Languages[UiControl.IgnoredExtensions];
            labAlsoIgnore.Text = Common.Languages[UiControl.AlsoIgnore];
            cIgnoreDotfiles.Text = Common.Languages[UiControl.Dotfiles];
            cIgnoreTempFiles.Text = Common.Languages[UiControl.TempFiles];
            cIgnoreOldFiles.Text = Common.Languages[UiControl.FilesModifiedBefore];
            //bandwidth tab

            gSyncing.Text = Common.Languages[UiControl.SyncFrequency];
            cAuto.Text = Common.Languages[UiControl.AutoEvery];
            labSeconds.Text = Common.Languages[UiControl.Seconds];
            cManually.Text = Common.Languages[UiControl.Manually];
            gLimits.Text = Common.Languages[UiControl.SpeedLimits];
            labDownSpeed.Text = Common.Languages[UiControl.DownLimit];
            labUpSpeed.Text = Common.Languages[UiControl.UpLimit];
            //language tab
            gLanguage.Text = Common.Languages[UiControl.Language];
            //about tab
            tabAbout.Text = Common.Languages[UiControl.About];


            labSite.Text = Common.Languages[UiControl.Website];
            labContact.Text = Common.Languages[UiControl.Contact];








            labSupportMail.Text = "iwit@contact.fr";
            //tray
            optionsToolStripMenuItem.Text = Common.Languages[UiControl.Options];
            aboutToolStripMenuItem.Text = Common.Languages[UiControl.About];
            SyncToolStripMenuItem.Text = Common.Languages[UiControl.StartSync];
            exitToolStripMenuItem.Text = Common.Languages[UiControl.Exit];

            SetTray(null, _lastTrayStatus);

            fTrayForm.Set_Language();

            // Is this a right-to-left language?
            RightToLeftLayout = Common.RtlLanguages.Contains(lan);

            // Save
            FTPboxLib.Settings.General.Language = lan;
            FTPboxLib.Settings.SaveGeneral();
        }

        /// <summary>
        /// When the user changes to another language, translate every label etc to that language.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cLanguages_SelectedIndexChanged(object sender, EventArgs e)
        {
            string lan = cLanguages.SelectedItem.ToString().Substring(cLanguages.SelectedItem.ToString().IndexOf("(") + 1);
            lan = lan.Substring(0, lan.Length - 1);
            try
            {
                Set_Language(lan);
            }
            catch { }
        }

        #endregion

        #region check internet connection

        private bool OfflineMode = false;
        [DllImport("wininet.dll")]
        private extern static bool InternetGetConnectedState(out int Description, int ReservedValue);

        public void OnNetworkChange(object sender, EventArgs e)
        {
            try
            {
                if (NetworkInterface.GetIsNetworkAvailable())
                {
                    if (OfflineMode)
                    {
                        while (!ConnectedToInternet())
                            Thread.Sleep(5000);
                        StartUpWork();
                    }
                    OfflineMode = false;
                }
                else
                {
                    if (!OfflineMode)
                    {
                        Program.Account.Client.Disconnect();
                        fswFiles.Dispose();
                        fswFolders.Dispose();
                    }
                    OfflineMode = true;
                    SetTray(null, new TrayTextNotificationArgs { MessageType = MessageType.Offline });
                }
            }
            catch { }
        }

        /// <summary>
        /// Check if internet connection is available
        /// </summary>
        /// <returns></returns>
        public static bool ConnectedToInternet()
        {
            int Desc;
            return InternetGetConnectedState(out Desc, 0);
        }

        #endregion

        #region Update System

        /// <summary>
        /// checks for an update
        /// called on each start-up of FTPbox.
        /// </summary>
        private void CheckForUpdate()
        {
            try
            {
                WebClient wc = new WebClient();
                wc.DownloadStringCompleted += (o, e) =>
                {
                    if (e.Cancelled || e.Error != null) return;

                    var json = (Dictionary<string, string>)Newtonsoft.Json.JsonConvert.DeserializeObject(e.Result, typeof(Dictionary<string, string>));
                    string version = json["NewVersion"];

                    //  Check that the downloaded file has the correct version format, using regex.
                    if (System.Text.RegularExpressions.Regex.IsMatch(version, @"[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+"))
                    {
                        Log.Write(l.Debug, "Current Version: {0} Installed Version: {1}", version, Application.ProductVersion);

                        if (version == Application.ProductVersion) return;

                        // show dialog box for  download now, learn more and remind me next time
                        newversion nvform = new newversion() { Tag = this };
                        newversion.newvers = json["NewVersion"];
                        newversion.downLink = json["DownloadLink"];
                        nvform.ShowDialog();
                        this.Show();
                    }
                };
                // Find out what the latest version is
                wc.DownloadStringAsync(new Uri(@"http://www.iwit-exchange.fr"));
            }
            catch (Exception ex)
            {
                Log.Write(l.Debug, "Error with version checking");
                Common.LogError(ex);
            }
        }

        #endregion

        #region Start on Windows Start-Up

        private void chkStartUp_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                SetStartup();
            }
            catch (Exception ex) { Common.LogError(ex); }
        }

        /// <summary>
        /// run iwit sync on windows startup
        /// <param name="enable"><c>true</c> to add it to system startup, <c>false</c> to remove it</param>
        /// </summary>
        private void SetStartup()
        {
            const string runKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

            RegistryKey startupKey = Registry.CurrentUser.OpenSubKey(runKey);

            if (startupKey.GetValue("iwitSync") == null)
            {
                startupKey = Registry.CurrentUser.OpenSubKey(runKey, true);
                startupKey.SetValue("iwitSync", Application.ExecutablePath);
                startupKey.Close();
            }
            /*
            else
            {
                // remove startup
                startupKey = Registry.CurrentUser.OpenSubKey(runKey, true);
                startupKey.DeleteValue("iwitSync", false);
                startupKey.Close();
            }
            */
        }
        

        /// <summary>
        /// returns true if iwitSync is set to start on windows startup
        /// </summary>
        /// <returns></returns>
        private bool CheckStartup()
        {
            const string runKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

            RegistryKey startupKey = Registry.CurrentUser.OpenSubKey(runKey);

            return startupKey.GetValue("iwitSync") != null;
        }

        #endregion

        #region Speed Limits

        private bool LimitUpSpeed()
        {
            return FTPboxLib.Settings.General.UploadLimit > 0;
        }

        private bool LimitDownSpeed()
        {
            return FTPboxLib.Settings.General.DownloadLimit > 0;
        }

        #endregion

        #region context menus

        private void AddContextMenu()
        {
            Log.Write(l.Info, "Adding registry keys for context menus");
            string reg_path = "Software\\Classes\\*\\Shell\\iwitSync";
            RegistryKey key = Registry.CurrentUser;
            key.CreateSubKey(reg_path);
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            string icon_path = string.Format("\"{0}\"", Path.Combine(Application.StartupPath, "iwit.ico"));
            string applies_to = getAppliesTo(false);
            string command;

            //Add the parent menu
            key.SetValue("MUIVerb", "iwitSync");
            key.SetValue("Icon", icon_path);
            key.SetValue("SubCommands", "");

            //The 'Copy link' child item
            reg_path = "Software\\Classes\\*\\Shell\\iwitSync\\Shell\\Copy";
            Registry.CurrentUser.CreateSubKey(reg_path);
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            key.SetValue("MUIVerb", "Copier le liens HTTP");
            key.CreateSubKey("Command");
            reg_path += "\\Command";
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            command = string.Format("\"{0}\" \"%1\" \"{1}\"", Application.ExecutablePath, "copy");
            key.SetValue("", command);

            //the 'Open in browser' child item
            reg_path = "Software\\Classes\\*\\Shell\\iwitSync\\Shell\\Open";
            Registry.CurrentUser.CreateSubKey(reg_path);
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            key.SetValue("MUIVerb", "Ouvrir ce fichier dans une fenetre");
            key.SetValue("AppliesTo", applies_to);
            key.CreateSubKey("Command");
            reg_path += "\\Command";
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            command = string.Format("\"{0}\" \"%1\" \"{1}\"", Application.ExecutablePath, "open");
            key.SetValue("", command);

            //the 'Synchronize this file' child item
            reg_path = "Software\\Classes\\*\\Shell\\iwitSync\\Shell\\Sync";
            Registry.CurrentUser.CreateSubKey(reg_path);
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            key.SetValue("MUIVerb", "Synchroniser ce fichier");
            key.SetValue("AppliesTo", applies_to);
            key.CreateSubKey("Command");
            reg_path += "\\Command";
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            command = string.Format("\"{0}\" \"%1\" \"{1}\"", Application.ExecutablePath, "sync");
            key.SetValue("", command);

            //the 'Move to iwitSync folder' child item
            reg_path = "Software\\Classes\\*\\Shell\\iwitSync\\Shell\\Move";
            Registry.CurrentUser.CreateSubKey(reg_path);
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            key.SetValue("MUIVerb", "Déplacer vers le dossier iwit sync");
            key.SetValue("AppliesTo", getAppliesTo(true));
            key.CreateSubKey("Command");
            reg_path += "\\Command";
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            command = string.Format("\"{0}\" \"%1\" \"{1}\"", Application.ExecutablePath, "move");
            key.SetValue("", command);

            #region same keys for the Folder menus
            reg_path = "Software\\Classes\\Directory\\Shell\\iwitSync";
            key = Registry.CurrentUser;
            key.CreateSubKey(reg_path);
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);

            //Add the parent menu
            key.SetValue("MUIVerb", "iwitSync");
            key.SetValue("Icon", icon_path);
            key.SetValue("SubCommands", "");

            //The 'Copy link' child item
            reg_path = "Software\\Classes\\Directory\\Shell\\iwitSync\\Shell\\Copy";
            Registry.CurrentUser.CreateSubKey(reg_path);
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            key.SetValue("MUIVerb", "Copier le liens HTTP");
            key.SetValue("AppliesTo", applies_to);
            key.CreateSubKey("Command");
            reg_path += "\\Command";
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            command = string.Format("\"{0}\" \"%1\" \"{1}\"", Application.ExecutablePath, "copy");
            key.SetValue("", command);

            //the 'Open in browser' child item
            reg_path = "Software\\Classes\\Directory\\Shell\\iwitSync\\Shell\\Open";
            Registry.CurrentUser.CreateSubKey(reg_path);
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            key.SetValue("MUIVerb", "Ouvrir le dossier dans une nouvelle fenêtre");
            key.SetValue("AppliesTo", applies_to);
            key.CreateSubKey("Command");
            reg_path += "\\Command";
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            command = string.Format("\"{0}\" \"%1\" \"{1}\"", Application.ExecutablePath, "open");
            key.SetValue("", command);

            //the 'Synchronize this folder' child item
            reg_path = "Software\\Classes\\Directory\\Shell\\iwitSync\\Shell\\Sync";
            Registry.CurrentUser.CreateSubKey(reg_path);
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            key.SetValue("MUIVerb", "Synchroniser ce dossier");
            key.SetValue("AppliesTo", applies_to);
            key.CreateSubKey("Command");
            reg_path += "\\Command";
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            command = string.Format("\"{0}\" \"%1\" \"{1}\"", Application.ExecutablePath, "sync");
            key.SetValue("", command);

            //the 'Move to iwitSync folder' child item
            reg_path = "Software\\Classes\\Directory\\Shell\\iwitSync\\Shell\\Move";
            Registry.CurrentUser.CreateSubKey(reg_path);
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            key.SetValue("MUIVerb", "Déplacer vers le dossier iwit sync");
            key.SetValue("AppliesTo", "NOT " + applies_to);
            key.CreateSubKey("Command");
            reg_path += "\\Command";
            key = Registry.CurrentUser.OpenSubKey(reg_path, true);
            command = string.Format("\"{0}\" \"%1\" \"{1}\"", Application.ExecutablePath, "move");
            key.SetValue("", command);

            #endregion

            key.Close();
        }

        /// <summary>
        /// Remove the iwitSync context menu (delete the registry files). 
        /// Called when application is exiting.
        /// </summary>
        private void RemoveFTPboxMenu()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Classes\\*\\Shell\\", true);
            key.DeleteSubKeyTree("iwitSync", false);
            key.Close();

            key = Registry.CurrentUser.OpenSubKey("Software\\Classes\\Directory\\Shell\\", true);
            key.DeleteSubKeyTree("iwitSync", false);
            key.Close();
        }

        /// <summary>
        /// Gets the value of the AppliesTo String Value that will be put to registry and determine on which files' right-click menus each iwitSync menu item will show.
        /// If the local path is inside a library folder, it has to check for another path (short_path), because System.ItemFolderPathDisplay will, for example, return 
        /// Documents\iwitSync instead of C:\Users\Username\Documents\iwitSync
        /// </summary>
        /// <param name="isForMoveItem">If the AppliesTo value is for the Move-to-FTPbox item, it adds 'NOT' to make sure it shows anywhere but in the local syncing folder.</param>
        /// <returns></returns>
        private string getAppliesTo(bool isForMoveItem)
        {
            string path = Program.Account.Paths.Local;
            string applies_to = (isForMoveItem) ? string.Format("NOT System.ItemFolderPathDisplay:~< \"{0}\"", path) : string.Format("System.ItemFolderPathDisplay:~< \"{0}\"", path);
            string short_path = null;
            var Libraries = new[] { Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyVideos };
            string userpath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\";

            if (path.StartsWith(userpath))
                foreach (Environment.SpecialFolder s in Libraries)
                    if (path.StartsWith(Environment.GetFolderPath(s)))
                        if (s != Environment.SpecialFolder.UserProfile) //TODO: is this ok?
                            short_path = path.Substring(userpath.Length);

            if (short_path == null) return applies_to;

            applies_to += (isForMoveItem) ? string.Format(" AND NOT System.ItemFolderPathDisplay: \"*{0}*\"", short_path) : string.Format(" OR System.ItemFolderPathDisplay: \"*{0}*\"", short_path);

            return applies_to;
        }

        private void RunServer()
        {
            var _tServer = new Thread(RunServerThread);
            _tServer.SetApartmentState(ApartmentState.STA);
            _tServer.Start();
        }

        private void RunServerThread()
        {
            int i = 1;
            Log.Write(l.Client, "Started the named-pipe server, waiting for clients (if any)");

            var server = new Thread(ServerThread);
            server.SetApartmentState(ApartmentState.STA);
            server.Start();

            Thread.Sleep(250);

            while (i > 0)
                if (server != null)
                    if (server.Join(250))
                    {
                        Log.Write(l.Client, "named-pipe server thread finished");
                        server = null;
                        i--;
                    }
            Log.Write(l.Client, "named-pipe server thread exiting...");

            RunServer();
        }

        public void ServerThread()
        {
            var pipeServer = new NamedPipeServerStream("iwitSync Server", PipeDirection.InOut, 5);
            int threadID = Thread.CurrentThread.ManagedThreadId;

            pipeServer.WaitForConnection();

            Log.Write(l.Client, "Client connected, id: {0}", threadID);

            try
            {
                StreamString ss = new StreamString(pipeServer);

                ss.WriteString("iwitSync");
                string args = ss.ReadString();

                ReadMessageSent fReader = new ReadMessageSent(ss, "All done!");

                Log.Write(l.Client, "Reading file: \n {0} \non thread [{1}] as user {2}.", args, threadID, pipeServer.GetImpersonationUserName());

                CheckClientArgs(ReadCombinedParameters(args).ToArray());

                pipeServer.RunAsClient(fReader.Start);
            }
            catch (IOException e)
            {
                Common.LogError(e);
            }
            pipeServer.Close();
        }

        private List<string> ReadCombinedParameters(string args)
        {
            List<string> r = new List<string>(args.Split('"'));
            while (r.Contains(""))
                r.Remove("");

            return r;
        }

        private void CheckClientArgs(string[] args)
        {
            var list = new List<string>(args);
            string param = list[0];
            list.RemoveAt(0);

            switch (param)
            {
                case "copy":
                    CopyArgLinks(list.ToArray());
                    break;
                case "sync":
                    SyncArgItems(list.ToArray());
                    break;
                case "open":
                    OpenArgItemsInBrowser(list.ToArray());
                    break;
                case "move":
                    MoveArgItems(list.ToArray());
                    break;
            }
        }

        private DateTime dtLastContextAction = DateTime.Now;
        /// <summary>
        /// Called when 'Copy HTTP link' is clicked from the context menus
        /// </summary>
        /// <param name="args"></param>
        private void CopyArgLinks(string[] args)
        {
            string c = null;
            int i = 0;
            foreach (string s in args)
            {
                if (!s.StartsWith(Program.Account.Paths.Local))
                {
                    MessageBox.Show("Vous ne pouvez pas utiliser ces fichiers car ils ne sont pas dans le dossier iwit sync.", "iwit sync - Fichier invalide", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                i++;
                //if (File.Exists(s))
                c += Program.Account.GetHttpLink(s);
                if (i < args.Count())
                    c += Environment.NewLine;
            }

            if (c == null) return;

            try
            {
                if ((DateTime.Now - dtLastContextAction).TotalSeconds < 2)
                    Clipboard.SetText(Clipboard.GetText() + Environment.NewLine + c);
                else
                    Clipboard.SetText(c);
                //SetTray(null, new FTPboxLib.TrayTextNotificationArgs { MessageType = FTPboxLib.MessageType.LinkCopied });
            }
            catch (Exception e)
            {
                Common.LogError(e);
            }
            dtLastContextAction = DateTime.Now;
        }

        /// <summary>
        /// Called when 'Synchronize this file/folder' is clicked from the context menus
        /// </summary>
        /// <param name="args"></param>
        private void SyncArgItems(string[] args)
        {
            foreach (string s in args)
            {
                Log.Write(l.Info, "Syncing local item: {0}", s);
                if (!s.StartsWith(Program.Account.Paths.Local))
                {
                    MessageBox.Show("Vous ne pouvez pas utiliser un fichier qui ne se trouvent pas dans le dossier de synchronisation iwitSync.", "iwit sync - Fichier invalide", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }
                var cpath = Program.Account.GetCommonPath(s, true);
                bool exists = Program.Account.Client.Exists(cpath);

                if (Common.PathIsFile(s) && System.IO.File.Exists(s))
                {
                    Program.Account.SyncQueue.Add(new SyncQueueItem(Program.Account)
                    {
                        Item = new ClientItem
                        {
                            FullPath = s,
                            Name = Common._name(cpath),
                            Type = ClientItemType.File,
                            Size = exists ? Program.Account.Client.SizeOf(cpath) : new FileInfo(s).Length,
                            LastWriteTime = exists ? Program.Account.Client.GetLwtOf(cpath) : System.IO.File.GetLastWriteTime(s)
                        },
                        ActionType = ChangeAction.changed,
                        SyncTo = exists ? SyncTo.Local : SyncTo.Remote
                    });
                }
                else if (!Common.PathIsFile(s) && Directory.Exists(s))
                {
                    var di = new DirectoryInfo(s);
                    Program.Account.SyncQueue.Add(new SyncQueueItem(Program.Account)
                    {
                        Item = new ClientItem
                        {
                            FullPath = di.FullName,
                            Name = di.Name,
                            Type = ClientItemType.Folder,
                            Size = 0x0,
                            LastWriteTime = DateTime.MinValue
                        },
                        ActionType = ChangeAction.changed,
                        SyncTo = exists ? SyncTo.Local : SyncTo.Remote,
                        SkipNotification = true
                    });
                }
            }
        }

        /// <summary>
        /// Called when 'Open in browser' is clicked from the context menus
        /// </summary>
        /// <param name="args"></param>
        private void OpenArgItemsInBrowser(string[] args)
        {
            foreach (string s in args)
            {
                if (!s.StartsWith(Program.Account.Paths.Local))
                {
                    MessageBox.Show("Vous ne pouvez pas utiliser ces fichiers car ils ne sont pas dans le dossier iwit sync.", "iwit sync - Fichier invalide", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                string link = Program.Account.GetHttpLink(s);
                try
                {
                    Process.Start(link);
                }
                catch (Exception e)
                {
                    Common.LogError(e);
                }
            }

            dtLastContextAction = DateTime.Now;
        }

        /// <summary>
        /// Called when 'Move to iwitSync folder' is clicked from the context menus
        /// </summary>
        /// <param name="args"></param>
        private void MoveArgItems(string[] args)
        {
            foreach (string s in args)
            {
                if (!s.StartsWith(Program.Account.Paths.Local + "\\" + Program.Account.Account.Username))
                {
                    if (System.IO.File.Exists(s))
                    {
                        FileInfo fi = new FileInfo(s);
                        System.IO.File.Copy(s, Path.Combine(Program.Account.Paths.Local + "\\" + Program.Account.Account.Username, fi.Name));
                    }
                    else if (Directory.Exists(s))
                    {
                        foreach (string dir in Directory.GetDirectories(s, "*", SearchOption.AllDirectories))
                        {
                            string name = dir.Substring(s.Length);
                            Directory.CreateDirectory(Path.Combine(Program.Account.Paths.Local + "\\" + Program.Account.Account.Username + "\\", name));
                        }
                        foreach (string file in Directory.GetFiles(s, "*", SearchOption.AllDirectories))
                        {
                            string name = file.Substring(s.Length);
                            System.IO.File.Copy(file, Path.Combine(Program.Account.Paths.Local + "\\" + Program.Account.Account.Username, name));
                        }
                    }
                }
            }
        }

        #endregion

        #region General Tab - Event Handlers

        private void rOpenInBrowser_CheckedChanged(object sender, EventArgs e)
        {
            if (rOpenInBrowser.Checked)
            {
                FTPboxLib.Settings.General.TrayAction = TrayAction.OpenInBrowser;
                FTPboxLib.Settings.SaveGeneral();
            }
        }

        private void rCopy2Clipboard_CheckedChanged(object sender, EventArgs e)
        {
            if (rCopy2Clipboard.Checked)
            {
                FTPboxLib.Settings.General.TrayAction = TrayAction.CopyLink;
                FTPboxLib.Settings.SaveGeneral();
            }
        }

        private void rOpenLocal_CheckedChanged(object sender, EventArgs e)
        {
            if (rOpenLocal.Checked)
            {
                FTPboxLib.Settings.General.TrayAction = TrayAction.OpenLocalFile;
                FTPboxLib.Settings.SaveGeneral();
            }
        }

        private void chkShowNots_CheckedChanged(object sender, EventArgs e)
        {
            FTPboxLib.Settings.General.Notifications = chkShowNots.Checked;
            FTPboxLib.Settings.SaveGeneral();
        }

        private void chkWebInt_CheckedChanged(object sender, EventArgs e)
        {
            if (!Program.Account.Client.isConnected) return;

            if (!changedfromcheck)
            {
                if (chkWebInt.Checked)
                    Program.Account.WebInterface.UpdatePending = true;
                else
                    Program.Account.WebInterface.DeletePending = true;

                chkWebInt.Enabled = false;

                if (!Program.Account.SyncQueue.Running)
                    Program.Account.WebInterface.Update();
            }
            changedfromcheck = false;
        }

        private void labViewInBrowser_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(Program.Account.WebInterfaceLink);
        }

        private void chkEnableLogging_CheckedChanged(object sender, EventArgs e)
        {
            FTPboxLib.Settings.General.EnableLogging = chkEnableLogging.Checked;
            FTPboxLib.Settings.SaveGeneral();

            Log.DebugEnabled = chkEnableLogging.Checked || FTPboxLib.Settings.IsDebugMode;
        }

        private void bBrowseLogs_Click(object sender, EventArgs e)
        {
            string logFile = Path.Combine(Common.AppdataFolder, "Debug.html");

            if (System.IO.File.Exists(logFile))
                Process.Start("explorer.exe", logFile);

        }

        private void chkShellMenus_CheckedChanged(object sender, EventArgs e)
        {
            FTPboxLib.Settings.General.AddContextMenu = chkShellMenus.Checked;
            FTPboxLib.Settings.SaveGeneral();

            if (chkShellMenus.Checked)
            {
                AddContextMenu();
            }
            else
            {
                RemoveFTPboxMenu();
            }
        }

        #endregion

        #region Account Tab - Event Handlers

        private void bRemoveAccount_Click(object sender, EventArgs e)
        {
            string msg = string.Format("Etes vous sur de vouloir supprimer ce compte : {0}?",
                   FTPboxLib.Settings.ProfileTitles[FTPboxLib.Settings.General.DefaultProfile]);
            if (MessageBox.Show(msg, "Confirmer la suppression du compte", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                FTPboxLib.Settings.RemoveCurrentProfile();

                //  Restart
                Process.Start(Application.ExecutablePath);
                KillTheProcess();
            }
        }

        private void bAddAccount_Click(object sender, EventArgs e)
        {
            FTPboxLib.Settings.General.DefaultProfile = FTPboxLib.Settings.Profiles.Count;
            FTPboxLib.Settings.SaveGeneral();

            //  Restart
            Process.Start(Application.ExecutablePath);
            KillTheProcess();
        }

        private void cProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cProfiles.SelectedIndex == FTPboxLib.Settings.General.DefaultProfile) return;

            var msg = string.Format("Changer pour {0} ?", FTPboxLib.Settings.ProfileTitles[cProfiles.SelectedIndex]);
            if (MessageBox.Show(msg, "Confirmer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                FTPboxLib.Settings.General.DefaultProfile = cProfiles.SelectedIndex;
                FTPboxLib.Settings.SaveGeneral();

                //  Restart
                Process.Start(Application.ExecutablePath);
                KillTheProcess();
            }
            else
                cProfiles.SelectedIndex = FTPboxLib.Settings.General.DefaultProfile;
        }

        private void bConfigureAccount_Click(object sender, EventArgs e)
        {
            new fAccountDetails().ShowDialog();
        }

        private void rWayOfSync_CheckedChanged(object sender, EventArgs e)
        {
            if (rLocalToRemoteOnly.Checked)
                Program.Account.Account.SyncDirection = SyncDirection.Remote;
            else if (rRemoteToLocalOnly.Checked)
                Program.Account.Account.SyncDirection = SyncDirection.Local;
            else if (rBothWaySync.Checked)
                Program.Account.Account.SyncDirection = SyncDirection.Both;
            // Save changes
            FTPboxLib.Settings.SaveProfile();
        }

        private void tTempPrefix_TextChanged(object sender, EventArgs e)
        {
            var val = tTempPrefix.Text;
            if (string.IsNullOrWhiteSpace(val) || !Common.IsAllowedFilename(val))
                return;
            // Save new prefix
            Program.Account.Account.TempFilePrefix = val;
            FTPboxLib.Settings.SaveProfile();
        }

        private void tTempPrefix_Leave(object sender, EventArgs e)
        {
            var val = tTempPrefix.Text;
            // Reset if the inserted value is empty or not allowed
            if (string.IsNullOrWhiteSpace(val) || !Common.IsAllowedFilename(val))
                tTempPrefix.Text = Program.Account.Account.TempFilePrefix;
        }

        #endregion

        #region Filters Tab - Event Handlers

        private void bConfigureSelectiveSync_Click(object sender, EventArgs e)
        {
            fSelective.ShowDialog();
        }

        private void bConfigureExtensions_Click(object sender, EventArgs e)
        {
            var fExtensions = new fIgnoredExtensions();
            fExtensions.ShowDialog();
        }

        private void cIgnoreTempFiles_CheckedChanged(object sender, EventArgs e)
        {
            Program.Account.IgnoreList.IgnoreTempFiles = cIgnoreTempFiles.Checked;
            Program.Account.IgnoreList.Save();
        }

        private void cIgnoreDotfiles_CheckedChanged(object sender, EventArgs e)
        {
            Program.Account.IgnoreList.IgnoreDotFiles = cIgnoreDotfiles.Checked;
            Program.Account.IgnoreList.Save();
        }

        private void cIgnoreOldFiles_CheckedChanged(object sender, EventArgs e)
        {
            dtpLastModTime.Enabled = cIgnoreOldFiles.Checked;
            Program.Account.IgnoreList.IgnoreOldFiles = cIgnoreOldFiles.Checked;
            Program.Account.IgnoreList.LastModifiedMinimum = (cIgnoreOldFiles.Checked) ? dtpLastModTime.Value : DateTime.MinValue;
            Program.Account.IgnoreList.Save();
        }

        private void dtpLastModTime_ValueChanged(object sender, EventArgs e)
        {
            Program.Account.IgnoreList.IgnoreOldFiles = cIgnoreOldFiles.Checked;
            Program.Account.IgnoreList.LastModifiedMinimum = (cIgnoreOldFiles.Checked) ? dtpLastModTime.Value : DateTime.MinValue;
            Program.Account.IgnoreList.Save();
        }

        #endregion

        #region Bandwidth Tab - Event Handlers

        private void cAuto_CheckedChanged(object sender, EventArgs e)
        {
            SyncToolStripMenuItem.Enabled = !cAuto.Checked || !Program.Account.SyncQueue.Running;
            Program.Account.Account.SyncMethod = (!cAuto.Checked) ? SyncMethod.Manual : SyncMethod.Automatic;
            FTPboxLib.Settings.SaveProfile();

            if (Program.Account.Account.SyncMethod == SyncMethod.Automatic)
            {
                Program.Account.Account.SyncFrequency = Convert.ToInt32(nSyncFrequency.Value);
                nSyncFrequency.Enabled = true;
            }
            else
            {
                nSyncFrequency.Enabled = false;
                //TODO: dispose timer?
            }
        }

        private void cManually_CheckedChanged(object sender, EventArgs e)
        {
            SyncToolStripMenuItem.Enabled = cManually.Checked || !Program.Account.SyncQueue.Running;
            Program.Account.Account.SyncMethod = (cManually.Checked) ? SyncMethod.Manual : SyncMethod.Automatic;
            FTPboxLib.Settings.SaveProfile();

            if (Program.Account.Account.SyncMethod == SyncMethod.Automatic)
            {
                Program.Account.Account.SyncFrequency = Convert.ToInt32(nSyncFrequency.Value);
                nSyncFrequency.Enabled = true;
            }
            else
            {
                nSyncFrequency.Enabled = false;
                //TODO: dispose timer?
            }
        }
        

        private void nSyncFrequency_ValueChanged(object sender, EventArgs e)
        {
            Program.Account.Account.SyncFrequency = Convert.ToInt32(nSyncFrequency.Value);

            if (Program.Account.Account.SyncFrequency == 0)
            {
                Program.Account.Account.SyncFrequency = (Int32)10;
                nSyncFrequency.Value = (Int32)10;
            }
            FTPboxLib.Settings.SaveProfile();
        }

        private void nDownLimit_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                FTPboxLib.Settings.General.DownloadLimit = Convert.ToInt32(nDownLimit.Value);
                FTPboxLib.Settings.SaveGeneral();
            }
            catch { }
        }

        private void nUpLimit_ValueChanged(object sender, EventArgs e)
        {
            try
            {
                FTPboxLib.Settings.General.UploadLimit = Convert.ToInt32(nUpLimit.Value);
                FTPboxLib.Settings.SaveGeneral();
            }
            catch { }
        }

        #endregion

        #region About Tab - Event Handlers

        private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(@"http://www.iwit-systems.fr/");
        }

        private void linkLabel4_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(@"http://www.iwit-systems.fr/");
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(@"http://www.iwit-systems.fr/");
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(@"http://www.iwit-systems.fr/");
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Process.Start(@"http://www.iwit-systems.fr/");
        }

        private void pictureBox1_Click_1(object sender, EventArgs e)
        {
            Process.Start(@"http://www.iwit-systems.fr/");
        }

        #endregion

        #region Tray Menu - Event Handlers

        private void tray_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                Process.Start("explorer.exe", Program.Account.Paths.Local);
        }

        private void tray_MouseClick(object sender, MouseEventArgs e)
        {
            if (!fTrayForm.Visible && e.Button == MouseButtons.Left)
            {
                var mouse = MousePosition;
                // Show the tray form
                fTrayForm.Show();
                // Make sure tray form gets focus
                fTrayForm.Activate();
                // Move the form to the correct position
                fTrayForm.PositionProperly(mouse);
            }
        }

        private void SyncToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Program.Account.Client.isConnected) return;

            StartRemoteSync(".");
        }

        public bool ExitedFromTray = false;
        private void fMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!ExitedFromTray && e.CloseReason != CloseReason.WindowsShutDown)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KillTheProcess();
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
            tabControl1.SelectedTab = tabAbout;

        }

        private void tray_BalloonTipClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(link)) return;

            if (link.EndsWith("webint"))
                Process.Start(link);
            else
            {
                if ((MouseButtons & MouseButtons.Right) != MouseButtons.Right)
                {
                    if (FTPboxLib.Settings.General.TrayAction == TrayAction.OpenInBrowser)
                    {
                        try
                        {
                            Process.Start(Program.Account.LinkToRecent());
                        }
                        catch
                        {
                            //Gotta catch 'em all 
                        }
                    }
                    else if (FTPboxLib.Settings.General.TrayAction == TrayAction.CopyLink)
                    {
                        try
                        {
                            Clipboard.SetText(Program.Account.LinkToRecent());
                        }
                        catch
                        {
                            //Gotta catch 'em all 
                        }
                        SetTray(null, new TrayTextNotificationArgs { MessageType = MessageType.LinkCopied });
                    }
                    else
                    {
                        try
                        {
                            Process.Start(Program.Account.PathToRecent());
                        }
                        catch
                        {
                            //Gotta catch 'em all
                        }
                    }
                }
            }
        }

        #endregion

        private void bTranslate_Click(object sender, EventArgs e)
        {
            ftranslate.ShowDialog();
        }


        public void SetTray(object o, TrayTextNotificationArgs e)
        {
            try
            {
                // Save latest tray status
                _lastTrayStatus = e;

                switch (e.MessageType)
                {
                    case MessageType.Connecting:
                    case MessageType.Reconnecting:
                    case MessageType.Syncing:
                        //tray.Icon = iwitSync.Properties.Resources.syncing;
                        tray.Icon = iwitSync.Properties.Resources.syncing;
                        tray.Text = Common.Languages[e.MessageType];
                        break;
                    case MessageType.Uploading:
                    case MessageType.Downloading:
                        tray.Icon = iwitSync.Properties.Resources.syncing;
                        tray.Text = Common.Languages[MessageType.Syncing];
                        break;
                    case MessageType.AllSynced:
                    case MessageType.Ready:
                        tray.Icon = iwitSync.Properties.Resources.AS;
                        tray.Text = Common.Languages[e.MessageType];
                        break;
                    case MessageType.Offline:
                    case MessageType.Disconnected:
                        tray.Icon = iwitSync.Properties.Resources.offline1;
                        tray.Text = Common.Languages[e.MessageType];
                        break;
                    case MessageType.Listing:
                        tray.Icon = iwitSync
                            .Properties.Resources.AS;
                        tray.Text = (Program.Account.Account.SyncMethod == SyncMethod.Automatic) ? Common.Languages[MessageType.AllSynced] : Common.Languages[MessageType.Listing];
                        break;
                    case MessageType.Nothing:
                        tray.Icon = iwitSync.Properties.Resources.ftpboxnew;
                        tray.Text = Common.Languages[e.MessageType];
                        break;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex);
            }
        }

        /// <summary>
        /// Starts the remote-to-local syncing on the root folder.
        /// Called from the timer, when remote syncing is automatic.
        /// </summary>
        /// <param name="state"></param>
        public void StartRemoteSync(object state)
        {
            if (Program.Account.Account.SyncMethod == SyncMethod.Automatic) SyncToolStripMenuItem.Enabled = false;
            Log.Write(l.Debug, "Début de la synchronisation à distance...");
            Program.Account.SyncQueue.Add(new SyncQueueItem(Program.Account)
            {
                Item = new ClientItem
                {
                    FullPath = (string)state,
                    Name = (string)state,
                    Type = ClientItemType.Folder,
                    Size = 0x0,
                    LastWriteTime = DateTime.Now
                },
                ActionType = ChangeAction.changed,
                SyncTo = SyncTo.Local,
                SkipNotification = true
            });
        }

        /// <summary>
        /// Display a messagebox with the certificate details, ask user to approve/decline it.
        /// </summary>
        public static void CheckCertificate(object sender, ValidateCertificateEventArgs n)
        {
            var msg = string.Empty;
            // Add certificate info
            if (Program.Account.Account.Protocol == FtpProtocol.SFTP)
                msg += string.Format("{0,-8}\t {1}\n{2,-8}\t {3}\n", "Clé:", n.Key, "Taille de la clé:", n.KeySize);
            else
                msg += string.Format("{0,-25}\t {1}\n{2,-25}\t {3}\n{4,-25}\t {5}\n{6,-25}\t {7}\n\n",
                    "Valide du:", n.ValidFrom, "Valide jusqu'au:", n.ValidTo, "Numéro de serie:", n.SerialNumber, "Algorithme:", n.Algorithm);

            msg += string.Format("Fingerprint: {0}\n\n", n.Fingerprint);
            msg += "Croire ce certificat et continuer ?";

            // Do we trust the server's certificate?
            bool certificate_trusted = MessageBox.Show(msg, "Avez-vous confiance en ce certificat ?", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes;
            n.IsTrusted = certificate_trusted;

            if (certificate_trusted)
            {
                FTPboxLib.Settings.TrustedCertificates.Add(n.Fingerprint);
                FTPboxLib.Settings.SaveCertificates();
            }
        }

        private void fMain_RightToLeftLayoutChanged(object sender, EventArgs e)
        {
            RightToLeft = RightToLeftLayout ? RightToLeft.Yes : RightToLeft.No;
            // Inherit manually
            tabControl1.RightToLeftLayout = RightToLeftLayout;
            trayMenu.RightToLeft = RightToLeftLayout ? RightToLeft.Yes : RightToLeft.No;

            // Relocate controls where necessary
            cLanguages.Location = RightToLeftLayout ? new Point(267, 19) : new Point(9, 19);
            bTranslate.Location = RightToLeftLayout ? new Point(172, 17) : new Point(191, 17);
            bBrowseLogs.Location = RightToLeftLayout ? new Point(172, 61) : new Point(191, 61);

            bAddAccount.Location = new Point(RightToLeftLayout ? 14 : 299, 10);
            bRemoveAccount.Location = new Point(RightToLeftLayout ? 95 : 380, 10);
            cProfiles.Location = new Point(RightToLeftLayout ? 170 : 8, 11);
            bConfigureAccount.Location = new Point(RightToLeftLayout ? 6 : 325, 16);

            bConfigureSelectiveSync.Location = new Point(RightToLeftLayout ? 6 : 325, 19);
            bConfigureExtensions.Location = new Point(RightToLeftLayout ? 6 : 325, 48);

            //bRefresh.Location = new Point(RightToLeftLayout ? 9 : 352, 19);
            nSyncFrequency.Location = RightToLeftLayout ? new Point(344, 89) : new Point(35, 89);
            nDownLimit.Location = RightToLeftLayout ? new Point(344, 45) : new Point(35, 45);
            nUpLimit.Location = RightToLeftLayout ? new Point(344, 100) : new Point(35, 100);



            linkLabel4.Location = RightToLeftLayout ? new Point(100, 67) : new Point(272, 67);
            label21.Location = RightToLeftLayout ? new Point(100, 90) : new Point(272, 90);
            labSupportMail.Location = RightToLeftLayout ? new Point(100, 113) : new Point(272, 113);




            labSite.Location = RightToLeftLayout ? new Point(272, 21) : new Point(100, 71);
            labContact.Location = RightToLeftLayout ? new Point(272, 90) : new Point(100, 90);

        }

        private void bShortCut_Click(object sender, EventArgs e)
        {
            // Creation d'un shortcut
            var account = string.Format("{0}@{1}", Program.Account.Account.Username, Program.Account.Account.Host);
            var pathDuRaccourci = string.Format(@"{0}\iwitSync\{1}", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), account + "\\");
            var destinationDuRaccourci = string.Format(@"{0}\Dossier iwit sync.lnk", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            CreateShortcut(pathDuRaccourci, destinationDuRaccourci, null, null, null, null, 1);
        }

        /// <summary>
        /// Create Windows Shorcut
        /// </summary>
        /// <param name="SourceFile">A file you want to make shortcut to</param>
        /// <param name="ShortcutFile">Path and shorcut file name including file extension (.lnk)</param>
        public static void CreateShortcut(string SourceFile, string ShortcutFile)
        {
            CreateShortcut(SourceFile, ShortcutFile, null, null, null, null, 1);
        }

        /// <summary>
        /// Create Windows Shorcut
        /// </summary>
        /// <param name="SourceFile">A file you want to make shortcut to</param>
        /// <param name="ShortcutFile">Path and shorcut file name including file extension (.lnk)</param>
        /// <param name="Description">Shortcut description</param>
        /// <param name="Arguments">Command line arguments</param>
        /// <param name="HotKey">Shortcut hot key as a string, for example "Ctrl+F"</param>
        /// <param name="WorkingDirectory">"Start in" shorcut parameter</param>
        public static void CreateShortcut(string TargetPath, string ShortcutFile, string Description,
           string Arguments, string HotKey, string WorkingDirectory, int logo)
        {
            // Check necessary parameters first:
            if (String.IsNullOrEmpty(TargetPath))
                throw new ArgumentNullException("TargetPath");
            if (String.IsNullOrEmpty(ShortcutFile))
                throw new ArgumentNullException("ShortcutFile");

            // Create WshShellClass instance:
            var wshShell = new WshShell();

            // Create shortcut object:
            IWshRuntimeLibrary.IWshShortcut shorcut = (IWshRuntimeLibrary.IWshShortcut)wshShell.CreateShortcut(ShortcutFile);
            var path = Path.GetTempPath();

            if(logo == 1)
                shorcut.IconLocation = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\Iwit Systems\\IwitSync\\dossier-iwit.ico";
            else if(logo == 2)
                shorcut.IconLocation = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\Iwit Systems\\IwitSync\\regular.ico";
            // Assign shortcut properties:
            shorcut.TargetPath = TargetPath;
            shorcut.Description = Description;
            if (!String.IsNullOrEmpty(Arguments))
                shorcut.Arguments = Arguments;
            if (!String.IsNullOrEmpty(HotKey))
                shorcut.Hotkey = HotKey;
            if (!String.IsNullOrEmpty(WorkingDirectory))
                shorcut.WorkingDirectory = WorkingDirectory;

            // Save the shortcut:
            shorcut.Save();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string msg = string.Format("Etes vous sur de vouloir configurer à nouveau votre compte : {0} ?",
                   FTPboxLib.Settings.ProfileTitles[FTPboxLib.Settings.General.DefaultProfile]);
            if (MessageBox.Show(msg, "Confirmer la modification", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                FTPboxLib.Settings.RemoveCurrentProfile();

                //  Restart
                Process.Start(Application.ExecutablePath);
                KillTheProcess();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Program.Account.Paths.Local);
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            MessageBox.Show(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\Iwit Systems\\IwitSync\\regular.ico");
        }

        private void button2_Click_2(object sender, EventArgs e)
        {
            MessageBox.Show(Program.Account.Paths.Local + "\\" + Program.Account.Account.Username);
        }

        private void aideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process p = new Process();
            p.StartInfo.FileName = (Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\Iwit Systems\\IwitSync\\www\\index.html");
            p.Start();
        }
    }
}
