﻿using CatalogueLibrary.DataHelper;
using NUnit.Framework;
using ReusableLibraryCode.DatabaseHelpers.Discovery.Microsoft;

namespace CatalogueLibraryTests.Unit
{
    public class SqlSyntaxHelperTests
    {
        [Test]
        public void GetNullSubstituteTests()
        {
            Assert.AreEqual("-999",RDMPQuerySyntaxHelper.GetNullSubstituteForComparisonsWithDataType("decimal(3)", true));
            Assert.AreEqual("-9999999999", RDMPQuerySyntaxHelper.GetNullSubstituteForComparisonsWithDataType("decimal(10)", true));
            Assert.AreEqual("-99.9", RDMPQuerySyntaxHelper.GetNullSubstituteForComparisonsWithDataType("decimal(3,1)", true));
            Assert.AreEqual("-.9999", RDMPQuerySyntaxHelper.GetNullSubstituteForComparisonsWithDataType("decimal(4,4)", true));


            Assert.AreEqual("999", RDMPQuerySyntaxHelper.GetNullSubstituteForComparisonsWithDataType("decimal(3)", false));
            Assert.AreEqual("9999999999", RDMPQuerySyntaxHelper.GetNullSubstituteForComparisonsWithDataType("decimal(10)", false));
            Assert.AreEqual("99.9", RDMPQuerySyntaxHelper.GetNullSubstituteForComparisonsWithDataType("decimal(3,1)", false));
            Assert.AreEqual(".9999", RDMPQuerySyntaxHelper.GetNullSubstituteForComparisonsWithDataType("decimal(4,4)", false));

        }

        [Test]
        public void SplitMethod()
        {
            var syntaxHelper = new MicrosoftQuerySyntaxHelper();

            string contents;
            string method;
            syntaxHelper.SplitLineIntoOuterMostMethodAndContents("count(*)",out method,out contents);
            
            Assert.AreEqual("count",method);
            Assert.AreEqual("*",contents);

            syntaxHelper.SplitLineIntoOuterMostMethodAndContents("count()", out method, out contents);

            Assert.AreEqual("count", method);
            Assert.AreEqual("", contents);


            syntaxHelper.SplitLineIntoOuterMostMethodAndContents("LTRIM(RTRIM([Fish]))", out method, out contents);

            Assert.AreEqual("LTRIM", method);
            Assert.AreEqual("RTRIM([Fish])", contents);
        }
    }
}
