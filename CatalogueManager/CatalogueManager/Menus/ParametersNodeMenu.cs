﻿using System;
using System.Linq;
using System.Windows.Forms;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.PerformanceImprovement;
using CatalogueLibrary.Nodes;
using CatalogueLibrary.Providers;
using CatalogueLibrary.Repositories;
using CatalogueManager.Collections.Providers;
using CatalogueManager.Icons.IconOverlays;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using CatalogueManager.Refreshing;
using CatalogueManager.SimpleDialogs;
using MapsDirectlyToDatabaseTableUI;
using RDMPStartup;
using ReusableUIComponents.Icons.IconProvision;

namespace CatalogueManager.Menus
{
    [System.ComponentModel.DesignerCategory("")]
    public class ParametersNodeMenu : RDMPContextMenuStrip
    {
        public ParametersNodeMenu(IActivateItems activator, ParametersNode parameterNode) : base(activator,null)
        {
            var filter = parameterNode.Collector as ExtractionFilter;

            if (filter != null)
                Items.Add(new ToolStripMenuItem("Add New 'Known Good Value(s) Set'", GetImage(RDMPConcept.ExtractionFilterParameterSet, OverlayKind.Add), (s, e) => AddParameterValueSet(filter)));
        }

        private void AddParameterValueSet(ExtractionFilter filter)
        {
            var parameterSet = new ExtractionFilterParameterSet(RepositoryLocator.CatalogueRepository,filter);
            parameterSet.CreateNewValueEntries();
            Publish(filter);
            Activate(parameterSet);
        }
    }
}