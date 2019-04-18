// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Linq;
using CatalogueLibrary.Data;
using CatalogueManager.MainFormUITabs;
using CatalogueManager.SimpleDialogs;
using NUnit.Framework;
using ScintillaNET;

namespace CatalogueLibraryTests.UserInterfaceTests
{
    class CatalogueItemUITests : UITests
    {
        [Test, UITimeout(20000)]
        public void Test_CatalogueItemUI_NormalState()
        {
            //when I have two CatalogueItems that have the same name
            var catalogueItem = WhenIHaveA<CatalogueItem>();
            var catalogueItem2 = WhenIHaveA<CatalogueItem>();

            var ui = AndLaunch<CatalogueItemUI>(catalogueItem);

            //when I change the description of the first
            var scintilla = ui._scintillaDescription;
            scintilla.Text = "what is in the column";

            //and save it
            var saver = ui.GetObjectSaverButton();
            saver.Save();

            //the new description shuold be set in my class
            Assert.AreEqual("what is in the column", catalogueItem.Description);

            //and the UI should have shown the Propagate changes dialog
            Assert.AreEqual(1, ItemActivator.Results.WindowsShown.Count);
            Assert.IsInstanceOf(typeof(PropagateCatalogueItemChangesToSimilarNamedUI),ItemActivator.Results.WindowsShown.Single());

            AssertNoErrors(ExpectedErrorType.Any);
        }
    }
}