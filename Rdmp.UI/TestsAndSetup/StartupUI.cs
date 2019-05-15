// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using MapsDirectlyToDatabaseTableUI;
using Rdmp.Core.Databases;
using Rdmp.Core.Startup;
using Rdmp.Core.Startup.Events;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.Icons.IconProvision;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Settings;
using ReusableUIComponents;
using ReusableUIComponents.Dialogs;

namespace Rdmp.UI.TestsAndSetup
{
    /// <summary>
    /// Shows every time an RDMP application is launched.  The 'User Friendly' view tells you whether there are any problems with your current platform databases / plugins by way of a large
    /// smiley face.  If you get an error (Red face) then there may be a hyperlink to resolve the problem (e.g. if a platform database needs patching or you have not yet configured your 
    /// platform databases (See ChoosePlatformDatabases).
    /// 
    /// <para>Green means that everything is working just fine.</para>
    /// 
    /// <para>Yellow means that something non-critical is not working e.g. a specific plugin is not working correctly</para>
    /// 
    /// <para>Red means that something critical is not working (Check for a fix hyperlink or look at the 'Technical' view to see the exact nature of the problem).</para>
    /// 
    /// <para>The 'Technical' view shows the progress of the discovery / version checking of all tiers of platform databases.  This includes checking that the software version matches the database
    /// schema version  (See ManagedDatabaseUI) and that plugins have loaded correctly (See MEFStartupUI).</para>
    /// </summary>
    public partial class StartupUI : Form, ICheckNotifier
    {
        private readonly Startup _startup;
        //Constructor
        public StartupUI(Startup startup)
        {
            _startup = startup;
            
            InitializeComponent();
            
            if(_startup == null)
                return;
            
            _startup.DatabaseFound += StartupDatabaseFound;
            _startup.MEFFileDownloaded += StartupMEFFileDownloaded;
            _startup.PluginPatcherFound += StartupPluginPatcherFound;

            pbDisconnected.Image = CatalogueIcons.ExternalDatabaseServer;

            var icon = new IconFactory();

            this.Icon = icon.GetIcon(CatalogueIcons.Main);
        }

        public bool DoNotContinue { get; set; }

        void StartupDatabaseFound(object sender, PlatformDatabaseFoundEventArgs eventArgs)
        {
            if(IsDisposed || !IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => StartupDatabaseFound(sender, eventArgs)));
                return;
            }

            HandleDatabaseFoundOnSimpleUI(eventArgs);
        }

        private void StartupMEFFileDownloaded(object sender, MEFFileDownloadProgressEventArgs eventArgs)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => StartupMEFFileDownloaded(sender, eventArgs)));
                return;
            }
                        
            //25% to 50% is downloading MEF
            pbLoadProgress.Value = (int) (250 + ((float)eventArgs.CurrentDllNumber / (float)eventArgs.DllsSeenInCatalogue * 250f));

            lblProgress.Text = "Downloading MEF File " + eventArgs.FileBeingProcessed;
            
            if (eventArgs.Status == MEFFileDownloadEventStatus.OtherError)
                ragSmiley1.Fatal(eventArgs.Exception);
        }

        private void StartupPluginPatcherFound(object sender, PluginPatcherFoundEventArgs eventArgs)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => StartupPluginPatcherFound(sender, eventArgs)));
                return;
            }
                        
            pbLoadProgress.Value = 800;//80% done
        }

        private bool escapePressed = false;
        private int countDownToClose = 5;

        private void StartupComplete()
        {
            if(InvokeRequired)
            {
                this.Invoke(new MethodInvoker(StartupComplete));
                return;
            }
            
            if (_startup != null && _startup.RepositoryLocator != null && _startup.RepositoryLocator.CatalogueRepository != null)
                WideMessageBox.CommentStore = _startup.RepositoryLocator.CatalogueRepository.CommentStore;
            
            //when things go badly leave the form
            if(ragSmiley1.IsFatal())
                return;

            Timer t = new Timer
            {
                Interval = 1000
            };
            t.Tick += TimerTick;
            t.Start();

            pbLoadProgress.Value = 1000;
        }

        void TimerTick(object sender, EventArgs e)
        {
            var t = (Timer) sender;
            
            if(escapePressed)
            {
                t.Stop();
                return;
            }

            countDownToClose --;

            lblProgress.Text = string.Format("Startup Complete... Closing in {0}s (Esc to cancel)",countDownToClose);

            if (!UserSettings.Wait5SecondsAfterStartupUI || countDownToClose == 0)
            {
                t.Stop();
                Close();
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (_startup == null)
                return;

            StartOrRestart(false);
            
        }

        private void StartOrRestart(bool forceClearRepositorySettings)
        {
            pbLoadProgress.Maximum = 1000;

            if (_startup.RepositoryLocator == null || forceClearRepositorySettings)
            {
                try
                {
                    lblProgress.Text = "Constructing UserSettingsRepositoryFinder";
                    UserSettingsRepositoryFinder finder = new UserSettingsRepositoryFinder();
                    _startup.RepositoryLocator = finder;
                }
                catch (Exception ex)
                {
                    lblProgress.Text = "Constructing UserSettingsRepositoryFinder Failed";
                    ragSmiley1.Fatal(ex);
                }
            }

            escapePressed = false;
            countDownToClose = 5;
            lastStatus = RDMPPlatformDatabaseStatus.Healthy;
            
            //10% progress because we connected to user settings
            pbLoadProgress.Value = 100;

            lblProgress.Text = "Awaiting Platform Database Discovery...";

            Task t = new Task(
                () =>
                    {
                        try
                        {
                            _startup.DoStartup(this);
                            StartupComplete();
                        }
                        catch (Exception ex)
                        {
                            if(IsDisposed || !IsHandleCreated)
                                ExceptionViewer.Show(ex);
                            else
                                Invoke(new MethodInvoker(() => ragSmiley1.Fatal(ex)));
                        }

                    }
                );
            t.Start();
        }

        RDMPPlatformDatabaseStatus lastStatus = RDMPPlatformDatabaseStatus.Healthy;
        private bool _couldNotReachTier1Database;

        private void HandleDatabaseFoundOnSimpleUI(PlatformDatabaseFoundEventArgs eventArgs)
        {
            //if status got worse
            if (eventArgs.Status < lastStatus )
                lastStatus = eventArgs.Status;

            //if we are unable to reach a tier 1 database don't report anything else
            if(_couldNotReachTier1Database)
                return;

            lblProgress.Text = eventArgs.Patcher.Name + " database status was " + eventArgs.Status;

            switch (eventArgs.Status)
            {
                case RDMPPlatformDatabaseStatus.Unreachable:

                    if (eventArgs.Patcher.Tier == 1)
                    {
                        pbDisconnected.Visible = true;
                        lblProgress.Text = "Could not reach " + eventArgs.Patcher.Name;
                        _couldNotReachTier1Database = true;
                        ragSmiley1.Fatal(new Exception(string.Format("Core Platform Database was {0} ({1})",eventArgs.Status , eventArgs.Patcher.Name)));
                    }
                    else
                        ragSmiley1.Warning(new Exception(string.Format("Tier {0} Database was {1} ({2})",eventArgs.Patcher.Tier ,eventArgs.Status , eventArgs.Patcher.Name)));
                    break;

                    case RDMPPlatformDatabaseStatus.Broken:
                    if (eventArgs.Patcher.Tier == 1)
                        ragSmiley1.Fatal(new Exception(string.Format("Core Platform Database was {0} ({1})",eventArgs.Status , eventArgs.Patcher.Name)));
                    else
                        ragSmiley1.Warning(new Exception(string.Format("Tier {0} Database was {1} ({2})",eventArgs.Patcher.Tier ,eventArgs.Status , eventArgs.Patcher.Name)));
                    break;

                case RDMPPlatformDatabaseStatus.RequiresPatching:
                    
                    if (MessageBox.Show("Patching Required on database of type " + eventArgs.Patcher.Name, "Patch",
                            MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        PatchingUI.ShowIfRequired(
                            (SqlConnectionStringBuilder) eventArgs.Repository.ConnectionStringBuilder,
                            eventArgs.Repository, eventArgs.Patcher);
                        DoNotContinue = true;
                    }
                    else
                    {
                        MessageBox.Show("Patching was cancelled, application will exit");
                        Application.Exit();
                    }

                    break;
                case RDMPPlatformDatabaseStatus.Healthy:
                    ragSmiley1.OnCheckPerformed(new CheckEventArgs(eventArgs.SummariseAsString(),CheckResult.Success));
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        //MEF only!
        public bool OnCheckPerformed(CheckEventArgs args)
        {
            if(InvokeRequired)
            {

                Invoke(new MethodInvoker(() => OnCheckPerformed(args)));
                return false;
            }
            
            //if the message starts with a percentage translate it into the progress bars movement
            Regex progressHackMessage = new Regex("^(\\d+)%");
            var match = progressHackMessage.Match(args.Message);

            if (match.Success)
            {
                var percent = float.Parse(match.Groups[1].Value);
                pbLoadProgress.Value = (int) (500 + (percent*2.5));//500-750
            }
             
            switch (args.Result)
            {
                case CheckResult.Success:
                    break;
                case CheckResult.Warning:
                case CheckResult.Fail:
                    
                    //MEF failures are only really warnings
                    args.Result = CheckResult.Warning;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            lblProgress.Text = args.Message;

            return ragSmiley1.OnCheckPerformed(args);
        }

        
        private void StartupUIMainForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                escapePressed = true;
        }

        private void StartupUIMainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
                if (ragSmiley1.IsFatal())
                {
                    bool loadAnyway = 
                    MessageBox.Show(
                        "Setup failed in a serious way, do you want to try to load the rest of the program anyway?",
                        "Try to load anyway?", MessageBoxButtons.YesNo) == DialogResult.Yes;

                    if(!loadAnyway)
                        DoNotContinue = true;
                }
        }
        
        private void BtnSetupPlatformDatabases_Click(object sender, EventArgs e)
        {
            var cmd = new ExecuteCommandChoosePlatformDatabase(new UserSettingsRepositoryFinder());
            cmd.Execute();
            StartOrRestart(true);
        }

        private void BtnChoosePlatformDatabases_Click(object sender, EventArgs e)
        {
            var cmd = new ExecuteCommandChoosePlatformDatabase(_startup.RepositoryLocator);
            cmd.Execute();
            DoNotContinue = true;
        }
    }
}
