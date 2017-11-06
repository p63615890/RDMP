using System.Drawing;
using System.Windows.Forms;
using CatalogueLibrary.CommandExecution.AtomicCommands;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Cohort;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using MapsDirectlyToDatabaseTableUI;
using RDMPObjectVisualisation.Copying.Commands;
using ReusableLibraryCode.CommandExecution;
using ReusableUIComponents.CommandExecution;
using ReusableUIComponents.CommandExecution.AtomicCommands;
using ReusableUIComponents.Icons.IconProvision;

namespace CatalogueManager.CommandExecution.AtomicCommands
{
    public class ExecuteCommandAddCatalogueToCohortIdentificationAsPatientIndexTable : BasicUICommandExecution,IAtomicCommand
    {
        private readonly CohortIdentificationConfiguration _configuration;
        private CatalogueCommand _catalogue;

        public ExecuteCommandAddCatalogueToCohortIdentificationAsPatientIndexTable(IActivateItems activator, CohortIdentificationConfiguration configuration) : base(activator)
        {
            _configuration = configuration;
        }

        public ExecuteCommandAddCatalogueToCohortIdentificationAsPatientIndexTable(IActivateItems activator,CatalogueCommand catalogue, CohortIdentificationConfiguration configuration):this(activator,configuration)
        {
            _catalogue = catalogue;
            if(!_catalogue.ContainsAtLeastOneExtractionIdentifier)
                SetImpossible("Catalogue " + _catalogue.Catalogue + " does not contain any IsExtractionIdentifier columns");
        }

        public override void Execute()
        {
            base.Execute();

            if (_catalogue == null)
            {
                var dialog = new SelectIMapsDirectlyToDatabaseTableDialog(Activator.RepositoryLocator.CatalogueRepository.GetAllCatalogues(),false,false);
                if (dialog.ShowDialog() == DialogResult.OK)
                    _catalogue = new CatalogueCommand((Catalogue) dialog.Selected);
                else
                    return;
            }
            AggregateConfigurationCommand aggregateCommand = _catalogue.GenerateAggregateConfigurationFor(_configuration);

            var joinableCommandExecution = new ExecuteCommandConvertAggregateConfigurationToPatientIndexTable(Activator, aggregateCommand, _configuration);
            joinableCommandExecution.Execute();
        }

        public Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.Catalogue, OverlayKind.Import);
        }
    }
}