﻿using System;
using System.Windows.Forms;
using CatalogueLibrary.CommandExecution.AtomicCommands;
using CatalogueManager.CommandExecution.AtomicCommands;
using ReusableUIComponents;
using ReusableUIComponents.CommandExecution;
using ReusableUIComponents.CommandExecution.AtomicCommands;
using ReusableUIComponents.Icons.IconProvision;

namespace CatalogueManager.Menus.MenuItems
{
    [System.ComponentModel.DesignerCategory("")]
    public class AtomicCommandMenuItem : ToolStripMenuItem
    {
        private readonly IAtomicCommand _command;

        public AtomicCommandMenuItem(IAtomicCommand command,IIconProvider iconProvider)
        {
            _command = command;

            Text = command.GetCommandName();
            Tag = command;
            Image = command.GetImage(iconProvider);
            
            //disable if impossible command
            Enabled = !command.IsImpossible;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            try
            {
                _command.Execute();
            }
            catch (Exception exception)
            {
                ExceptionViewer.Show("Failed to execute command '" + _command.GetCommandName() +"' (Type was '" +_command.GetType().Name +"')", exception);
            }

        }
    }
}