﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using CatalogueLibrary.Data.Aggregation;
using CatalogueLibrary.Data.Cohort.Joinables;
using CatalogueLibrary.Data.DataLoad;
using CatalogueManager.Collections.Providers;
using CatalogueManager.Icons.IconOverlays;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using CatalogueManager.Menus;
using CatalogueManager.Refreshing;
using DataExportLibrary.Data.DataTables;
using DataExportLibrary.Interfaces.Data.DataTables;
using DataExportManager.CohortUI;
using DataExportManager.CohortUI.CohortSourceManagement;
using DataExportManager.CohortUI.ImportCustomData;
using DataExportManager.Collections.Nodes.UsedByProject;
using DataExportManager.Collections.Providers;
using DataExportManager.CommandExecution.AtomicCommands;
using DataExportManager.DataViewing.Collections;
using MapsDirectlyToDatabaseTableUI;
using RDMPStartup;
using ReusableUIComponents;
using ReusableUIComponents.Icons.IconProvision;

namespace DataExportManager.Menus
{
    [System.ComponentModel.DesignerCategory("")]
    public class ExtractableCohortMenu:RDMPContextMenuStrip
    {
        private readonly ExtractableCohort _cohort;

        public ExtractableCohortMenu(IActivateItems activator, ExtractableCohort cohort)
            : base(activator,cohort)
        {
            _cohort = cohort;
            Items.Add("View TOP 100 identifiers",null, (s, e) => ViewTop100());

            Add(new ExecuteCommandImportFileAsCustomDataForCohort(activator,cohort));

            Items.Add("Import CohortIdentificationConfiguration PatientIndexTable as custom data", activator.CoreIconProvider.GetImage(RDMPConcept.CohortIdentificationConfiguration, OverlayKind.Import), (s, e) => ExecutePatientIndexTableAndImportAsCustomData());

            AddCommonMenuItems();

        }

        public ExtractableCohortMenu(IActivateItems activator, ExtractableCohortUsedByProjectNode cohortNode)
            : this(activator, cohortNode.Cohort)
        {

        }


        private void ExecutePatientIndexTableAndImportAsCustomData()
        {
            var patientIndexTables = RepositoryLocator.CatalogueRepository.GetAllObjects<JoinableCohortAggregateConfiguration>().Select(j => j.AggregateConfiguration).Distinct().ToArray();

            var chooser = new SelectIMapsDirectlyToDatabaseTableDialog(patientIndexTables, false, false);

            if (chooser.ShowDialog() == DialogResult.OK)
            {
                var chosen = chooser.Selected as AggregateConfiguration;
                if (chosen != null)
                {
                    var importer = new ImportCustomDataFileUI(_activator,_cohort, chosen);
                    importer.RepositoryLocator = RepositoryLocator;
                    _activator.ShowWindow(importer, true);
                }
            }
        }


        private void ViewTop100()
        {
            _activator.ViewDataSample(new ViewCohortExtractionUICollection(_cohort));
        }

    }
}
