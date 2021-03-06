// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.CommandExecution;
using Rdmp.Core.CommandExecution.AtomicCommands;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.ImportExport;
using Rdmp.Core.Icons.IconProvision;
using Rdmp.Core.Repositories;
using Rdmp.UI.Collections;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.CommandExecution.AtomicCommands.UIFactory;
using Rdmp.UI.Icons.IconProvision;
using Rdmp.UI.ItemActivation;
using Rdmp.UI.Refreshing;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Icons.IconProvision;

namespace Rdmp.UI.Menus
{
    /// <summary>
    /// Base class for all right click context menus in <see cref="RDMPCollectionUI"/> controls.  These menus are built by reflection
    /// when the selected object is changed.
    /// </summary>
    
    [System.ComponentModel.DesignerCategory("")]
    public class RDMPContextMenuStrip:ContextMenuStrip
    {
        private readonly object _o;
        public IRDMPPlatformRepositoryServiceLocator RepositoryLocator { get; private set; }
        protected IActivateItems _activator;
        
        private readonly AtomicCommandUIFactory AtomicCommandUIFactory;

        protected ToolStripMenuItem ActivateCommandMenuItem;
        private RDMPContextMenuStripArgs _args;

        private Dictionary <string,ToolStripMenuItem> _subMenuDictionary = new Dictionary<string, ToolStripMenuItem>();

        public const string Inspection = "Inspection";
        public const string Tree = "Tree";
        public const string Alter = "Alter";

        public RDMPContextMenuStrip(RDMPContextMenuStripArgs args, object o)
        {
            _o = o;
            _args = args;
            
            _activator = _args.ItemActivator;

            _activator.Theme.ApplyTo(this);

            AtomicCommandUIFactory = new AtomicCommandUIFactory(_activator);
            
            RepositoryLocator = _activator.RepositoryLocator;
                    
            if(o != null && !(o is RDMPCollection))
                ActivateCommandMenuItem = Add(new ExecuteCommandActivate(_activator,args.Masquerader?? o));

            if(o is DatabaseEntity e)
            {
                var gotoMenuBuilder = new GoToMenuBuilder(_activator);
                Items.Add(gotoMenuBuilder.GetMenu(e));

            }
        }

        protected void ReBrandActivateAs(string newTextForActivate, RDMPConcept newConcept, OverlayKind overlayKind = OverlayKind.None)
        {
            //Activate is currently branded edit by parent lets tailor that
            ActivateCommandMenuItem.Image = _activator.CoreIconProvider.GetImage(newConcept, overlayKind);
            ActivateCommandMenuItem.Text = newTextForActivate;
        }


        protected ToolStripMenuItem Add(IAtomicCommand cmd, Keys shortcutKey = Keys.None, ToolStripMenuItem toAddTo = null)
        {
            var mi = AtomicCommandUIFactory.CreateMenuItem(cmd);

            if (shortcutKey != Keys.None)
                mi.ShortcutKeys = shortcutKey;

            if (toAddTo == null)
                Items.Add(mi);
            else
                toAddTo.DropDownItems.Add(mi);

            return mi;
        }

        /// <summary>
        /// Creates a new command under a submenu named <paramref name="submenu"/> (if this doesn't exist yet it will be created).
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="shortcutKey"></param>
        /// <param name="submenu"></param>
        /// <param name="image"></param>
        protected void Add(IAtomicCommand cmd, Keys shortcutKey, string submenu, Image image = null)
        {
            Add(cmd,shortcutKey,AddMenuIfNotExists(submenu,image));
        }


        private ToolStripMenuItem AddMenuIfNotExists(string submenu, Image image = null)
        {
            if(!_subMenuDictionary.ContainsKey(submenu))
            {
                var m = new ToolStripMenuItem(submenu,image);
                _subMenuDictionary.Add(submenu,m);
            }

            return _subMenuDictionary[submenu];
        }

        public void AddCommonMenuItems(RDMPCollectionCommonFunctionality commonFunctionality)
        {
            var deletable = _o as IDeleteable;
            var nameable = _o as INamed;
            var databaseEntity = _o as DatabaseEntity;

            var treeMenuItem = AddMenuIfNotExists(Tree);
            var inspectionMenuItem =  AddMenuIfNotExists(Inspection);
            
            //ensure all submenus appear in the same place
            foreach(var mi in _subMenuDictionary.Values)
                Items.Add(mi);

            //add plugin menu items
            foreach (var plugin in _activator.PluginUserInterfaces)
            {
                try
                {
                    ToolStripMenuItem[] toAdd = plugin.GetAdditionalRightClickMenuItems(_o);

                    if (toAdd != null && toAdd.Any())
                        Items.AddRange(toAdd);
                }
                catch (Exception ex)
                {
                    _activator.GlobalErrorCheckNotifier.OnCheckPerformed(new CheckEventArgs(ex.Message,
                        CheckResult.Fail, ex));
                }
            }

            if(Items.Count > 0)
                Items.Add(new ToolStripSeparator());

            //add delete/rename etc
            if (deletable != null)
            {
                if (_args.Masquerader is IDeleteable)
                    deletable = (IDeleteable)_args.Masquerader;

                Add(new ExecuteCommandDelete(_activator, deletable), Keys.Delete);
            }

            if (nameable != null)
                Add(new ExecuteCommandRename(_activator, nameable), Keys.F2);

            //add seldom used submenus (pin, view dependencies etc)
            Items.Add(inspectionMenuItem);
            PopulateInspectionMenu(commonFunctionality, inspectionMenuItem);

            Items.Add(treeMenuItem);
            PopulateTreeMenu(commonFunctionality, treeMenuItem);

            //add refresh and then finally help
            if (databaseEntity != null) 
                Add(new ExecuteCommandRefreshObject(_activator, databaseEntity), Keys.F5);
            
            Add(new ExecuteCommandShowKeywordHelp(_activator, _args));}

        private void PopulateTreeMenu(RDMPCollectionCommonFunctionality commonFunctionality, ToolStripMenuItem treeMenuItem)
        {
            var databaseEntity = _o as DatabaseEntity;

            if (databaseEntity != null)
            {
                if (databaseEntity.Equals(_args.CurrentlyPinnedObject))
                    Add(new ExecuteCommandUnpin(_activator, databaseEntity), Keys.None, treeMenuItem);
                else
                    Add(new ExecuteCommandPin(_activator, databaseEntity), Keys.None, treeMenuItem);
            }

            if (_args.Tree != null && !commonFunctionality.Settings.SuppressChildrenAdder)
            {
                Add(new ExecuteCommandExpandAllNodes(_activator, commonFunctionality, _args.Model), Keys.None, treeMenuItem);
                Add(new ExecuteCommandCollapseChildNodes(_activator, commonFunctionality, _args.Model), Keys.None, treeMenuItem);
            }
            treeMenuItem.Enabled = treeMenuItem.HasDropDown;
        }

        private void PopulateInspectionMenu(RDMPCollectionCommonFunctionality commonFunctionality, ToolStripMenuItem inspectionMenuItem)
        {
            var databaseEntity = _o as DatabaseEntity;

            if (commonFunctionality.CheckColumnProvider != null)
            {
                if (databaseEntity != null)
                    Add(new ExecuteCommandCheck(_activator, databaseEntity, commonFunctionality.CheckColumnProvider.RecordWorst), Keys.None, inspectionMenuItem);

                var checkAll = new ToolStripMenuItem("Check All", null, (s, e) => commonFunctionality.CheckColumnProvider.CheckCheckables());
                checkAll.Image = CatalogueIcons.TinyYellow;
                checkAll.Enabled = commonFunctionality.CheckColumnProvider.GetCheckables().Any();
                inspectionMenuItem.DropDownItems.Add(checkAll);
            }
            
            inspectionMenuItem.Enabled = inspectionMenuItem.HasDropDown;
        }

        protected void Activate(DatabaseEntity o)
        {
            var cmd = new ExecuteCommandActivate(_activator, o);
            cmd.Execute();
        }

        protected void Publish(DatabaseEntity o)
        {
            _activator.RefreshBus.Publish(this, new RefreshObjectEventArgs(o));
        }

        protected Image GetImage(object concept, OverlayKind shortcut = OverlayKind.None)
        {
            return _activator.CoreIconProvider.GetImage(concept, shortcut);
        }

        protected void Emphasise(DatabaseEntity o, int expansionDepth = 0)
        {
            _activator.RequestItemEmphasis(this, new EmphasiseRequest(o, expansionDepth));
        }
    }
}
