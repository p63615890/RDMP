﻿using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using CatalogueLibrary.CommandExecution.AtomicCommands;
using CatalogueLibrary.Data;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using DataExportLibrary.Data.DataTables;
using ReusableLibraryCode.Icons.IconProvision;

namespace DataExportManager.CommandExecution.AtomicCommands.CohortCreationCommands
{
    public class ExecuteCommandCreateNewCohortFromCatalogue : CohortCreationCommandExecution
    {
        private ExtractionInformation _extractionIdentifierColumn;


        public ExecuteCommandCreateNewCohortFromCatalogue(IActivateItems activator,ExtractionInformation extractionInformation) : base(activator)
        {
            SetExtractionIdentifierColumn(extractionInformation);
        }

        public override string GetCommandHelp()
        {
            return "Creates a cohort using ALL of the patient identifiers in the referenced dataset";
        }

        [ImportingConstructor]
        public ExecuteCommandCreateNewCohortFromCatalogue(IActivateItems activator, Catalogue catalogue): base(activator)
        {
            SetExtractionIdentifierColumn(GetExtractionInformationFromCatalogue(catalogue));
        }

        public ExecuteCommandCreateNewCohortFromCatalogue(IActivateItems activator): base(activator)
        {
            
        }

        public ExecuteCommandCreateNewCohortFromCatalogue(IActivateItems activator, ExternalCohortTable externalCohortTable) : base(activator)
        {
            ExternalCohortTable = externalCohortTable;
        }

        public override IAtomicCommandWithTarget SetTarget(DatabaseEntity target)
        {
            var cata = target as Catalogue;
            var ei = target as ExtractionInformation;

            if (cata != null)
                SetExtractionIdentifierColumn(GetExtractionInformationFromCatalogue(cata));

            if (ei != null)
                SetExtractionIdentifierColumn(ei);

            return base.SetTarget(target);
        }

        private ExtractionInformation GetExtractionInformationFromCatalogue(Catalogue catalogue)
        {

            var eis = catalogue.GetAllExtractionInformation(ExtractionCategory.Any);

            if (eis.Count(ei => ei.IsExtractionIdentifier) != 1)
            {
                SetImpossible("Catalogue must have a single IsExtractionIdentifier column");
                return null;
            }

            return eis.Single(e => e.IsExtractionIdentifier);
        }

        private void SetExtractionIdentifierColumn(ExtractionInformation extractionInformation)
        {
            //if they are trying to set the identifier column to something that isn't marked IsExtractionIdentifier
            if (_extractionIdentifierColumn != null && !extractionInformation.IsExtractionIdentifier)
                SetImpossible("Column is not marked IsExtractionIdentifier");

            _extractionIdentifierColumn = extractionInformation;
        }

        public override void Execute()
        {
            if (_extractionIdentifierColumn == null)
            {
                var cata = SelectOne(Activator.RepositoryLocator.CatalogueRepository.GetAllObjects<Catalogue>());

                if(cata == null)
                    return;
                SetExtractionIdentifierColumn(GetExtractionInformationFromCatalogue(cata));
            }

            base.Execute();

            var request = GetCohortCreationRequest("All patient identifiers in ExtractionInformation '" + _extractionIdentifierColumn.CatalogueItem.Catalogue + "." + _extractionIdentifierColumn.GetRuntimeName() + "'  (ID=" + _extractionIdentifierColumn.ID +")");

            //user choose to cancel the cohort creation request dialogue
            if (request == null)
                return;

            request.ExtractionIdentifierColumn = _extractionIdentifierColumn;
            var configureAndExecute = GetConfigureAndExecuteControl(request, "Import column " + _extractionIdentifierColumn + " as cohort and commmit results");
            
            Activator.ShowWindow(configureAndExecute);
        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.ExtractableCohort, OverlayKind.Add);
        }
    }
}