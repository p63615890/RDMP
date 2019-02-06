// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CatalogueLibrary.CommandExecution.AtomicCommands;
using CatalogueLibrary.Data;
using CatalogueManager.CommandExecution.AtomicCommands;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using CohortManager.Wizard;
using DataExportLibrary.Data.DataTables;
using ReusableLibraryCode.Icons.IconProvision;

namespace CohortManager.CommandExecution.AtomicCommands
{
    /// <summary>
    /// Creates a new persistent database query configuration for identifying cohort sets of patients.
    /// </summary>
    public class ExecuteCommandCreateNewCohortIdentificationConfiguration: BasicUICommandExecution,IAtomicCommandWithTarget
    {
        private Project _associateWithProject;

        public ExecuteCommandCreateNewCohortIdentificationConfiguration(IActivateItems activator) : base(activator)
        {
            if(!activator.CoreChildProvider.AllCatalogues.Any())
                SetImpossible("There are no datasets loaded yet into RDMP");
        }

        public Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.CohortIdentificationConfiguration,OverlayKind.Add);
        }

        public IAtomicCommandWithTarget SetTarget(DatabaseEntity target)
        {
            _associateWithProject = target as Project;
            return this;
        }

        public override void Execute()
        {
            base.Execute();
            var wizard = new CreateNewCohortIdentificationConfigurationUI(Activator);

            if(wizard.ShowDialog() == DialogResult.OK)
            {
                var cic = wizard.CohortIdentificationCriteriaCreatedIfAny;
                if(cic == null)
                    return;

                if (_associateWithProject != null)
                {
                    var assoc = _associateWithProject.AssociateWithCohortIdentification(cic);
                    Publish(assoc);
                    Emphasise(assoc, int.MaxValue);

                }
                else
                {
                    Publish(cic);
                    Emphasise(cic, int.MaxValue);    
                }

                Activate(cic);
            }   
        }


        public override string GetCommandHelp()
        {
            return
                "This will open a window which will guide you in the steps for creating a Cohort based on Inclusion and Exclusion criteria.\r\n" +
                "You will be asked to choose one or more Dataset and the associated column filters to use as inclusion or exclusion criteria for the cohort.";
        }
    }
}