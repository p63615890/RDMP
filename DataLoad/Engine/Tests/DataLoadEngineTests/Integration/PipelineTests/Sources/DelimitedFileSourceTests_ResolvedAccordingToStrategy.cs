using System;
using LoadModules.Generic.DataFlowSources;
using LoadModules.Generic.Exceptions;
using NUnit.Framework;

namespace DataLoadEngineTests.Integration.PipelineTests.Sources
{
    public class DelimitedFileSourceTests_ResolvedAccordingToStrategy : DelimitedFileSourceTestsBase
    {
        [TestCase(true)]
        [TestCase(false)]
        public void EmptyFile_TotallyEmpty(bool throwOnEmpty)
        {
            var file = CreateTestFile(); //create completely empty file

            if (throwOnEmpty)
            {
                var ex = Assert.Throws<FlatFileLoadException>(() => RunGetChunk(file, BadDataHandlingStrategy.ThrowException, throwOnEmpty));
                Assert.AreEqual("File DelimitedFileSourceTests.txt is empty", ex.Message);
            }
            else
            {
                Assert.IsNull(RunGetChunk(file, BadDataHandlingStrategy.ThrowException, throwOnEmpty));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void EmptyFile_AllWhitespace(bool throwOnEmpty)
        {
            var file = CreateTestFile(@" 
     
    ");

            if(throwOnEmpty)
            {
                var ex = Assert.Throws<FlatFileLoadException>(() => RunGetChunk(file, BadDataHandlingStrategy.ThrowException, throwOnEmpty));
                StringAssert.StartsWith("File DelimitedFileSourceTests.txt is empty", ex.Message);
            }
            else
            {
                Assert.IsNull(RunGetChunk(file, BadDataHandlingStrategy.ThrowException,throwOnEmpty));
            }
        }


        [TestCase(true)]
        [TestCase(false)]
        public void EmptyFile_HeaderOnly(bool throwOnEmpty)
        {
            var file = CreateTestFile(@"Name,Address

 
     
    ");

            if (throwOnEmpty)
            {
                var ex = Assert.Throws<FlatFileLoadException>(() => RunGetChunk(file, s=>s.ThrowOnEmptyFiles = true));
                Assert.AreEqual("File DelimitedFileSourceTests.txt is empty", ex.Message);
            }
            else
            {
                Assert.IsNull(RunGetChunk(file,s => s.ThrowOnEmptyFiles = false));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void EmptyFile_ForceHeader(bool throwOnEmpty)
        {
            var file = CreateTestFile(@"Name,Address

 
     
    ");
            
            if (throwOnEmpty)
            {
                var ex = Assert.Throws<FlatFileLoadException>(() => RunGetChunk(file,  
                    s =>{ s.ThrowOnEmptyFiles = true; s.ForceHeaders="Name,Address"; s.ForceHeadersReplacesFirstLineInFile = true;}));
                Assert.AreEqual("File DelimitedFileSourceTests.txt is empty", ex.Message);
            }
            else
            {
                Assert.IsNull(RunGetChunk(file,
                    s =>{ s.ThrowOnEmptyFiles = false; s.ForceHeaders="Name,Address"; s.ForceHeadersReplacesFirstLineInFile = true;}));
            }
        }

        [TestCase(BadDataHandlingStrategy.DivertRows)]
        [TestCase(BadDataHandlingStrategy.ThrowException)]
        [TestCase(BadDataHandlingStrategy.IgnoreRows)]
        public void BadCSV_TooManyCellsInRow(BadDataHandlingStrategy strategy)
        {
            var file = CreateTestFile(
                "Name,Description,Age",
                "Frank,Is the greatest,100",
                "Bob,He's also dynamite, seen him do a lot of good work,30",
                "Dennis,Hes ok,35");

            switch (strategy)
            {
                case BadDataHandlingStrategy.ThrowException:
                    var ex = Assert.Throws<FlatFileLoadException>(() => RunGetChunk(file, strategy, true));
                    StringAssert.StartsWith("Bad data found on line 3", ex.Message);
                    break;
                case BadDataHandlingStrategy.IgnoreRows:
                    var dt = RunGetChunk(file, strategy, true);
                    Assert.AreEqual(2,dt.Rows.Count);
                    break;
                case BadDataHandlingStrategy.DivertRows:
                    var dt2 = RunGetChunk(file, strategy, true);
                    Assert.AreEqual(2,dt2.Rows.Count);

                    AssertDivertFileIsExactly("Bob,He's also dynamite, seen him do a lot of good work,30\r\n");

                    break;
                default:
                    throw new ArgumentOutOfRangeException("strategy");
            }
        }
        
        [TestCase(BadDataHandlingStrategy.DivertRows,true)]
        [TestCase(BadDataHandlingStrategy.ThrowException,false)]
        [TestCase(BadDataHandlingStrategy.ThrowException,true)]
        [TestCase(BadDataHandlingStrategy.IgnoreRows,false)]
        public void BadCSV_TooFewCellsInRow(BadDataHandlingStrategy strategy,bool tryToResolve)
        {
            var file = CreateTestFile(
                "Name,Description,Age",
                "Frank,Is the greatest,100",
                "",
                "Other People To Investigate",
                "Dennis,Hes ok,35");

            Action<DelimitedFlatFileDataFlowSource> adjust = (a) =>
            {
                a.BadDataHandlingStrategy = strategy;
                a.AttemptToResolveNewlinesInRecords = tryToResolve;
                a.ThrowOnEmptyFiles = true;
            };

            switch (strategy)
            {
                case BadDataHandlingStrategy.ThrowException:
                    var ex = Assert.Throws<FlatFileLoadException>(() => RunGetChunk(file, adjust));
                    StringAssert.StartsWith("Bad data found on line 4", ex.Message);
                    break;
                case BadDataHandlingStrategy.IgnoreRows:
                    var dt = RunGetChunk(file, adjust);
                    Assert.AreEqual(2, dt.Rows.Count);
                    break;
                case BadDataHandlingStrategy.DivertRows:
                    var dt2 = RunGetChunk(file, adjust);
                    Assert.AreEqual(2, dt2.Rows.Count);

                    AssertDivertFileIsExactly("\r\nOther People To Investigate\r\n");

                    break;
                default:
                    throw new ArgumentOutOfRangeException("strategy");
            }
        }

        [TestCase(BadDataHandlingStrategy.DivertRows, true)]
        [TestCase(BadDataHandlingStrategy.ThrowException, false)]
        [TestCase(BadDataHandlingStrategy.ThrowException, true)]
        [TestCase(BadDataHandlingStrategy.IgnoreRows, false)]
        public void BadCSV_TooFewColumnsOnLastLine(BadDataHandlingStrategy strategy, bool tryToResolve)
        {
            var file = CreateTestFile(
                "Name,Description,Age",
                "Frank,Is the greatest,100",
                "Bob");

            Action<DelimitedFlatFileDataFlowSource> adjust = (a) =>
            {
                a.BadDataHandlingStrategy = strategy;
                a.AttemptToResolveNewlinesInRecords = tryToResolve;
                a.ThrowOnEmptyFiles = true;
            };

            switch (strategy)
            {
                case BadDataHandlingStrategy.ThrowException:
                    var ex = Assert.Throws<FlatFileLoadException>(() => RunGetChunk(file, adjust));
                    StringAssert.StartsWith("Bad data found on line 3", ex.Message);
                    break;
                case BadDataHandlingStrategy.IgnoreRows:
                    var dt = RunGetChunk(file, adjust);
                    Assert.AreEqual(1, dt.Rows.Count);
                    break;
                case BadDataHandlingStrategy.DivertRows:
                    var dt2 = RunGetChunk(file, adjust);
                    Assert.AreEqual(1, dt2.Rows.Count);

                    AssertDivertFileIsExactly("Bob\r\n");

                    break;
                default:
                    throw new ArgumentOutOfRangeException("strategy");
            }
        }

        [Test]
        public void BadCSV_FreeTextMiddleColumn()
        {
            //This is recoverable
            var file = CreateTestFile(
                "Name,Description,Age",
                "Frank,Is the greatest,100",
                @"Bob,He's
not too bad
to be honest,20",
                "Dennis,Hes ok,35");

            var dt = RunGetChunk(file,s=> { s.AttemptToResolveNewlinesInRecords = true; });
            Assert.AreEqual(3, dt.Rows.Count);
            Assert.AreEqual("He's\r\nnot too bad\r\nto be honest", dt.Rows[1][1]);
        }

        [Test]
        public void BadCSV_FreeTextFirstColumn()
        {
            var file = CreateTestFile(
                "Description,Name,Age",
                "Is the greatest,Frank,100",
                @"He's
not too bad
to be honest,Bob,20",
                "Hes ok,Dennis,35");

            var ex = Assert.Throws<FlatFileLoadException>(()=>RunGetChunk(file, s => { s.AttemptToResolveNewlinesInRecords = true; }));
            Assert.AreEqual("Bad data found on line 3",ex.Message);

            //looks like a good record followed by 2 bad records
            var dt = RunGetChunk(file, s => { s.AttemptToResolveNewlinesInRecords = true; s.BadDataHandlingStrategy = BadDataHandlingStrategy.IgnoreRows; });
            Assert.AreEqual(3, dt.Rows.Count);
            Assert.AreEqual("to be honest", dt.Rows[1][0]);
            Assert.AreEqual("Bob", dt.Rows[1][1]);
            Assert.AreEqual(20, dt.Rows[1][2]);
        }

        [Test]
        public void BadCSV_FreeTextLastColumn()
        {
            var file = CreateTestFile(
                "Name,Age,Description",
                "Frank,100,Is the greatest",
                @"Bob,20,He's
not too bad
to be honest",
                "Dennis,35,Hes ok");

            var ex = Assert.Throws<FlatFileLoadException>(()=>RunGetChunk(file, s => { s.AttemptToResolveNewlinesInRecords = true; }));
            Assert.AreEqual("Bad data found on line 4",ex.Message);

            //looks like a good record followed by 2 bad records
            var dt = RunGetChunk(file, s => { s.AttemptToResolveNewlinesInRecords = true;s.BadDataHandlingStrategy = BadDataHandlingStrategy.IgnoreRows; });
            Assert.AreEqual(3,dt.Rows.Count);
            Assert.AreEqual("He's", dt.Rows[1][2]);


        }

        [Test]
        public void BadCSV_ForceHeaders()
        {
            var file = CreateTestFile(
                "Patient's first name, Patients blood glucose, measured in mg",
                "Thomas,100",
                "Frank,300");

            var ex = Assert.Throws<FlatFileLoadException>(() => RunGetChunk(file, s => { s.AttemptToResolveNewlinesInRecords = false; }));
            Assert.AreEqual("Bad data found on line 2", ex.Message);


            var dt = RunGetChunk(file, s =>
            {
                s.AttemptToResolveNewlinesInRecords = false;
                s.ForceHeaders = "Name,BloodGlucose";
                s.ForceHeadersReplacesFirstLineInFile = true;
            });

            Assert.AreEqual(2,dt.Rows.Count);
            Assert.AreEqual(2, dt.Columns.Count);
            Assert.AreEqual("Thomas", dt.Rows[0]["Name"]);
            Assert.AreEqual(100, dt.Rows[0]["BloodGlucose"]);

        }

        [Test]
        public void BadCSV_ForceHeaders_NoReplace()
        {
            var file = CreateTestFile(
                "Thomas,100",
                "Frank,300");

            var dt = RunGetChunk(file, s =>
            {
                s.AttemptToResolveNewlinesInRecords = false;
                s.ForceHeaders = "Name,BloodGlucose";
            });

            Assert.AreEqual(2,dt.Rows.Count);
            Assert.AreEqual(2, dt.Columns.Count);
            Assert.AreEqual("Thomas", dt.Rows[0]["Name"]);
            Assert.AreEqual(100, dt.Rows[0]["BloodGlucose"]);

        }
        
    }
}