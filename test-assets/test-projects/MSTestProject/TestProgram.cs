using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Main.Test
{
    [TestClass]
    public class MainTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Test()
        {
            Assert.IsTrue(true);
        } // This is here for boundary testing

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void DataDrivenTest1(int i)
        {
            Assert.IsTrue(i > 0);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void DataDrivenTest2(int i)
        {
            Assert.IsTrue(i >= 0);
        }

        [TestMethod]
        public void FailingTest()
        {
            Assert.AreEqual(1, 2);
        }

        [TestMethod]
        public void CheckStandardOutput()
        {
            int a = 1, b = 1;
            Console.WriteLine($"a = {a}, b = {b}");
            Assert.AreEqual(a,b);
        }

        [TestMethod]
        public void CheckRunSettings()
        {
            Assert.AreEqual(TestContext.Properties["TestRunSetting"].ToString(), "CorrectValue");
        }

        private void UtilityFunction()
        {

        }

        private class NestedClass
        {
            public void M()
            {

            }
        }
    }

    public class NoTests
    {
        public void M()
        {

        }
    }
}
