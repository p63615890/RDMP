﻿using System.Drawing;
using CatalogueLibrary.Data;
using CatalogueManager.Icons.IconOverlays;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using DataExportLibrary.Data.DataTables;
using ReusableUIComponents.Copying;
using ReusableUIComponents.Icons.IconProvision;

namespace CatalogueManager.CommandExecution.AtomicCommands.WindowArranging
{
    public class ExecuteCommandEditAndRunExistingDataExtractionProject : BasicCommandExecution, IAtomicCommandWithTarget
    {
        public Project Project { get; set; }

        private readonly IActivateItems activator;

        public ExecuteCommandEditAndRunExistingDataExtractionProject(IActivateItems activator)
        {
            this.activator = activator;
        }

        public Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.Project, OverlayKind.Edit);
        }

        public void SetTarget(DatabaseEntity target)
        {
            Project = (Project) target;
        }

        public override void Execute()
        {
            if (Project == null)
                SetImpossible("You must choose a Data Extraction Project to edit.");

            base.Execute();
            activator.WindowArranger.SetupEditDataExtractionProject(this, Project);
        }

        public override string GetCommandHelp()
        {
            return
                "This will take you to the Data Extraction Projects list and allow you to Run the selected project immediately.\r\n" +
                "You must choose a Project from the list before proceeding.";
        }
    }
}