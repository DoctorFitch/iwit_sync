/* License
 * This file is part of FTPbox - Copyright (C) 2012-2013 ftpbox.org
 * FTPbox is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published 
 * by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed 
 * in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU General Public License for more details. You should have received a copy of the GNU General Public License along with this program. 
 * If not, see <http://www.gnu.org/licenses/>.
 */
/* Translations.cs
 * Manage all translations, which are loaded from the translations.xml file
 */

using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;

namespace FTPboxLib
{
    public class Translations
    {
        XmlDocument xmlDocument = new XmlDocument();       
        string documentPath = Environment.CurrentDirectory + "\\translations.xml";

        public Translations()
        {
            try { xmlDocument.Load(documentPath); }
            catch (Exception ex) { Log.Write(l.Info, "?>" + ex.Message); xmlDocument.LoadXml("<translations></translations>"); }
        }

        public string this[MessageType t]
        {
            get
            {
                switch (t)
                {
                    default:
                        return null;
                    case MessageType.ItemChanged:
                        return Get("/tray/changed", "{0} was changed.");
                    case MessageType.ItemCreated:
                        return Get("/tray/created", "{0} was created.");
                    case MessageType.ItemDeleted:
                        return Get("/tray/deleted", "{0} was deleted.");
                    case MessageType.ItemRenamed:
                        return Get("/tray/renamed", "{0} was renamed to {1}.");
                    case MessageType.ItemUpdated:
                        return Get("/tray/updated", "{0} was updated.");
                    case MessageType.FilesOrFoldersUpdated:
                        return Get("/tray/FilesOrFoldersUpdated", "{0} {1} have been updated");
                    case MessageType.FilesOrFoldersCreated:
                        return Get("/tray/FilesOrFoldersCreated", "{0} {1} have been created");
                    case MessageType.FilesAndFoldersChanged:
                        return Get("/tray/FilesAndFoldersChanged", "{0} {1} and {2} {3} have been updated");
                    case MessageType.ItemsDeleted:
                        return Get("/tray/ItemsDeleted", "{0} items have been deleted.");
                    case MessageType.File:
                        return Get("/tray/file", "File");
                    case MessageType.Files:
                        return Get("/tray/files", "Files");
                    case MessageType.Folder:
                        return Get("/tray/folder", "Folder");
                    case MessageType.Folders:
                        return Get("/tray/folders", "Folders");
                    case MessageType.LinkCopied:
                        return Get("/tray/link_copied", "Link copied to clipboard");
                    case MessageType.Connecting:
                        return Get("/tray/connecting", "FTPbox - Connecting...");
                    case MessageType.Disconnected:
                        return Get("/tray/disconnected", "FTPbox - Disconnected");
                    case MessageType.Reconnecting:
                        return Get("/tray/reconnecting", "FTPbox - Re-Connecting...");
                    case MessageType.Listing:
                        return Get("/tray/listing", "FTPbox - Listing...");
                    case MessageType.Uploading:
                        return Get("/tray/uploading", "Uploading {0}");
                    case MessageType.Downloading:
                        return Get("/tray/downloading", "Downloading {0}");
                    case MessageType.Syncing:
                        return Get("/tray/syncing", "FTPbox - Syncing");
                    case MessageType.AllSynced:
                        return Get("/tray/synced", "FTPbox - All files synced");
                    case MessageType.Offline:
                        return Get("/tray/offline", "FTPbox - Offline");
                    case MessageType.Ready:
                        return Get("/tray/ready", "FTPbox - Ready");
                    case MessageType.Nothing:
                        return "FTPbox";
                    case MessageType.NotAvailable:
                        return Get("/tray/not_available", "Not Available");
                }
            }
        }

        public string this[WebUiAction a]
        {
            get
            {
                switch (a)
                {
                    case WebUiAction.waiting:
                        return Get("/web_interface/downloading", "The Web Interface will be downloaded.")
                            + Environment.NewLine +
                            Get("/web_interface/in_a_minute", "This will take a minute.");
                    case WebUiAction.removing:
                        return Get("/web_interface/removing", "Removing the Web Interface...");
                    case WebUiAction.updating:
                        return Get("/web_interface/updating", "Updating the web interface...");
                    case WebUiAction.removed:
                        return Get("/web_interface/removed", "Web interface has been removed.");
                    default:
                        return Get("/web_interface/updated", "Web Interface has been updated.")
                            + Environment.NewLine +
                            Get("/web_interface/setup", "Click here to view and set it up!");
                }
            }
        }

        public string this[ChangeAction ca, bool file]
        {
            get
            {
                string fileorfolder = (file) ? this[MessageType.File] : this[MessageType.Folder];
                switch (ca)
                {
                    case ChangeAction.created:
                        return string.Format(this[MessageType.ItemCreated], fileorfolder);
                    case ChangeAction.deleted:
                        return string.Format(this[MessageType.ItemDeleted], fileorfolder);
                    case ChangeAction.renamed:
                        return this[MessageType.ItemRenamed];
                    case ChangeAction.changed:
                        return string.Format(this[MessageType.ItemChanged], fileorfolder);
                    default:
                        return string.Format(this[MessageType.ItemUpdated], fileorfolder);
                }
            }
        }

        public string this[UiControl c]
        {
            get
            {
                switch(c)
                {
                    // Setup
                    case UiControl.LoginDetails:
                        return Get("/new_account/login_details", "FTP login details");
                    case UiControl.Protocol:
                        return Get("/main_form/mode", "Protocol") + ":";
                    case UiControl.Encryption:
                        return Get("/new_account/encryption", "Encryption") + ":";
                    case UiControl.Host:
                        return Get("/main_form/host", "Adresse") + ":";
                    case UiControl.Port:
                        return Get("/main_form/port", "Port") + ":";
                    case UiControl.Username:
                        return Get("/main_form/username", "Nom utilisateur") + ":";
                    case UiControl.Password:
                        return Get("/main_form/password", "Mot de passe") + ":";
                    case UiControl.AskForPassword:
                        return Get("/new_account/ask_for_password", "Toujours demander le mot de passe");
                    case UiControl.Authentication:
                        return Get("/setup/authentication", "Authentication") + ":";
                    case UiControl.LocalFolder:
                        return Get("/paths/local_folder", "Dossier local");
                    case UiControl.DefaultLocalFolder:
                        return Get("/paths/default_local", "Je veux utiliser le dossier local par défaut");
                    case UiControl.CustomLocalFolder:
                        return Get("/paths/custom_local", "Je veux selectionner mon dossier local");
                    case UiControl.Browse:
                        return Get("/paths/browse", "Chercher");
                    case UiControl.RemotePath:
                        return Get("/main_form/remote_path", "Fichiers distants");
                    case UiControl.FullRemotePath:
                        return Get("/paths/full_path", "Chemin d'accès complet") + ":";
                    case UiControl.SelectiveSync:
                        return Get("/main_form/selective", "Synchronisation séléctive");
                    case UiControl.SyncAllFiles:
                        return Get("/setup/sync_all_files", "Je veux synchroniser tous les fichiers");
                    case UiControl.SyncSpecificFiles:
                        return Get("/setup/sync_specific_files", "Je veux séléctionner les fichiers à synchroniser");
                    case UiControl.UncheckFiles:
                        return Get("/main_form/selective_info", "Désélectionner les fichiers que vous ne voullez pas synchroniser") + ":";
                    case UiControl.Previous:
                        return Get("/setup/previous", "Précendent");
                    case UiControl.Next:
                        return Get("/setup/next", "Suivant");
                    case UiControl.Finish:
                        return Get("/new_account/done", "Terminer");
                    // Options
                    case UiControl.Options:
                        return Get("/main_form/options", "Options");
                    case UiControl.General:
                        return Get("/main_form/general", "Generale");
                    case UiControl.Links:
                        return Get("/main_form/links", "Liens");
                    case UiControl.FullAccountPath:
                        return Get("/main_form/account_full_path", "Account's full path") + ":";
                    case UiControl.WhenRecentFileClicked:
                        return Get("/main_form/when_not_clicked", "Lorque vous cliquez sur une notification") + ":";
                    case UiControl.OpenUrl:
                        return Get("/main_form/open_in_browser", "Ouvrir le lien dans le navigateur par defaut");
                    case UiControl.CopyUrl:
                        return Get("/main_form/copy", "Copier le liens dans le presse papeir");
                    case UiControl.OpenLocal:
                        return Get("/main_form/open_local", "Ouvrir le fichier local");
                    case UiControl.Application:
                        return Get("/main_form/application", "Application");
                    case UiControl.ShowNotifications:
                        return Get("/main_form/show_nots", "Montrer les notifications");
                    case UiControl.StartOnStartup:
                        return Get("/main_form/start_on_startup", "Demarrer au démarrage de Windows");
                    case UiControl.EnableLogging:
                        return Get("/main_form/enable_logging", "Actvier l'authentification");
                    case UiControl.ViewLog:
                        return Get("/main_form/view_log", "Voir le fichier Log");
                    case UiControl.AddShellMenu:
                        return Get("/main_form/shell_menus", "Ajouter au menu contextuel");
                    case UiControl.Account:
                        return Get("/main_form/account", "Compte");
                    case UiControl.Profile:
                        return Get("/main_form/profile", "Profile");
                    case UiControl.Add:
                        return Get("/new_account/add", "Ajouter");
                    case UiControl.Remove:
                        return Get("/main_form/remove", "Supprimer");
                    case UiControl.Details:
                        return Get("/main_form/details", "Details");
                    case UiControl.WebUi:
                        return Get("/web_interface/web_int", "Interface web");
                    case UiControl.UseWebUi:
                        return Get("/web_interface/use_webint", "Utiliser l'interface web");
                    case UiControl.ViewInBrowser:
                        return Get("/web_interface/view", "(Voir dans le navigateur)");
                    case UiControl.WayOfSync:
                        return Get("/main_form/way_of_sync", "Way of synchronization") + ":";
                    case UiControl.LocalToRemoteSync:
                        return Get("/main_form/local_to_remote", "Local to remote only");
                    case UiControl.RemoteToLocalSync:
                        return Get("/main_form/remote_to_local", "Remote to local only");
                    case UiControl.BothWaysSync:
                        return Get("/main_form/both_ways", "Dans les deux sens");
                    case UiControl.TempNamePrefix:
                        return Get("/main_form/temp_file_prefix", "Prefixe des fichiers temporaires") + ":";
                    case UiControl.Filters:
                        return Get("/main_form/file_filters", "Filtres");
                    case UiControl.Configure:
                        return Get("/main_form/configure", "Configurer");
                    case UiControl.Refresh:
                        return Get("/main_form/refresh", "Recharger");
                    case UiControl.IgnoredExtensions:
                        return Get("/main_form/ignored_extensions", "Extension ignorées");
                    case UiControl.AlsoIgnore:
                        return Get("/main_form/also_ignore", "Ignorer également") + ":";
                    case UiControl.Dotfiles:
                        return Get("/main_form/dotfiles", "Dossier commençant par un point (Dotfiles)");
                    case UiControl.TempFiles:
                        return Get("/main_form/temp_files", "Fichiers temporaires");
                    case UiControl.FilesModifiedBefore:
                        return Get("/main_form/old_files", "Fichiers modifiés précédements") + ":";

                    case UiControl.Bandwidth:
                        return Get("/main_form/bandwidth", "Bande passante");
                    case UiControl.SyncFrequency:
                        return Get("/main_form/sync_freq", "Fréquence de synchronisation");
                    case UiControl.SyncWhen:
                        return Get("/main_form/sync_when", "Synchroniser les fichiers distants");
                    case UiControl.AutoEvery:
                        return Get("/main_form/auto", "Automatique toutes les 1 à 60 secondes");
                    case UiControl.Seconds:
                        return Get("/main_form/seconds", "secondes");
                    case UiControl.Manually:
                        return Get("/main_form/manually", "Manuel");
                    case UiControl.SpeedLimits:
                        return Get("/main_form/speed_limits", "Limite de vitesse");
                    case UiControl.DownLimit:
                        return Get("/main_form/limit_download", "Limite de vitesse de telechargement");
                    case UiControl.UpLimit:
                        return Get("/main_form/limit_upload", "Limite de vitesse d'envoie");
                    case UiControl.NoLimits:
                        return Get("/main_form/no_limits", "( 0 par défaut pour aucune limite )");

                    case UiControl.Language:
                        return Get("/main_form/language", "Langage");

                    case UiControl.About:
                        return Get("/main_form/about", "A propos");
                    case UiControl.TheTeam:
                        return Get("/main_form/team", "L'équipe") + ":";
                    case UiControl.Website:
                        return Get("/main_form/website", "Site officiel") + ":";
                    case UiControl.Contact:
                        return Get("/main_form/contact", "Contact") + ":";
                    case UiControl.CodedIn:
                        return Get("/main_form/coded_in", "Codé en") + ":";
                    case UiControl.Notes:
                        return Get("/main_form/notes", "Notes");
                    case UiControl.Contribute:
                        return Get("/main_form/contribute", "Contribute");
                    case UiControl.FreeAndAll:
                        return Get("/main_form/ftpbox_is_free", "- FTPbox is free and open-source");
                    case UiControl.GetInTouch:
                        return Get("/main_form/contact_me", "- Feel free to contact me for anything.");
                    case UiControl.ReportBug:
                        return Get("/main_form/report_bug", "Reporter un probleme");
                    case UiControl.RequestFeature:
                        return Get("/main_form/request_feature", "Request a feature");
                    case UiControl.Donate:
                        return Get("/main_form/donate", "Donate") + ":";

                    case UiControl.RecentFiles:
                        return Get("/tray/recent_files", "Fichier récent");
                    case UiControl.Modified:
                        return Get("/tray/modified", "Modifié");
                    case UiControl.StartSync:
                        return Get("/tray/start_syncing", "Commencé la synchronisation");
                    case UiControl.Exit:
                        return Get("/tray/exit", "Quitter");

                    // New Version Form
                    case UiControl.UpdateAvailable:
                        return Get("/new_version/update_available", "Mise à jour disponible");
                    case UiControl.NewVersionAvailable:
                        return Get("/new_version/new_v_available", "New version of FTPbox is available");
                    case UiControl.CurrentVersion:
                        return Get("/new_version/current_version", "Current Version");
                    case UiControl.NewVersion:
                        return Get("/new_version/new_ver", "New Version");
                    case UiControl.AskDownload:
                        return Get("/new_version/wanna_download", "Do you want to download the new version now");
                    case UiControl.DownloadNow:
                        return Get("/new_version/download", "Update Now");
                    case UiControl.LearnMore:
                        return Get("/new_version/learn_more", "Learn More");
                    case UiControl.RemindLater:
                        return Get("/new_version/remind_me_next_time", "Not this time");

                    default:
                        return string.Empty;
                }
            }
        }

        #region parsing from translations file

        public string Get(string xPath, string defaultValue, string lan = null)
        {
            var path = string.Format("translations/{0}{1}", lan ?? Settings.General.Language, xPath);
            XmlNode xmlNode = xmlDocument.SelectSingleNode(path);
            if (xmlNode != null) { return xmlNode.InnerText.Replace("_and", "&"); }
            else { return defaultValue; }
        }

        /// <summary>
        /// Returns a list of all paths to nodes that contain translation strings
        /// </summary>
        public List<string> GetPaths()
        {
            return xmlDocument.SelectNodes("translations/en/*/*").Cast<XmlNode>()
                .Select(x => string.Format("/{0}/{1}", x.ParentNode.Name, x.Name))
                .ToList();
        }
        
        #endregion
    }
}
