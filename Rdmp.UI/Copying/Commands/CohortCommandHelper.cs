// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Windows.Forms;
using MapsDirectlyToDatabaseTableUI;
using Rdmp.Core.Curation.Data;

namespace Rdmp.UI.Copying.Commands
{
    public class CohortCommandHelper
    {

        public static ExtractionInformation PickOneExtractionIdentifier(Catalogue c, ExtractionInformation[] candidates)
        {
            if (candidates.Length == 0)
                throw new Exception("None of the ExtractionInformations in Catalogue " + c + " are marked IsExtractionIdentifier.  You will need to edit the Catalogue in CatalogueManager and select one of the columns in the dataset as the extraction identifier");

            MessageBox.Show("Dataset " + c + " has " + candidates.Length + " columns marked IsExtractionInformation, which one do you want to do cohort identification on?");

            var dialog = new SelectIMapsDirectlyToDatabaseTableDialog(candidates, false, false);

            dialog.ShowDialog();

            if (dialog.DialogResult == DialogResult.OK)
                return (ExtractionInformation)dialog.Selected;


            throw new Exception("User refused to choose an extraction identifier");
        }
    }
}