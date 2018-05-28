using NUnit.Framework;
using System;

namespace Main.Test
{
    public class MainTest
    {
        [Test]
        public void Test()
        {
            Assert.True(true);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void DataDrivenTest1(int i)
        {
            Assert.True(i > 0);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void DataDrivenTest2(int i)
        {
            Assert.True(i >= 0);
        }

        [TestCaseSource("_items")]
        public void SourceDataDrivenTest(int i)
        {
            Assert.True(i > 0);
        }

        [Test]
        public void FailingTest()
        {
            Assert.AreEqual(1, 2);
        }

        [Test]
        public void CheckStandardOutput()
        {
            int a = 1, b = 1;
            Console.WriteLine($"a = {a}, b = {b}");
            Assert.AreEqual(a,b);
        }

        public void UtilityFunction()
        {

        }

        private static int[] _items = new int[1] { 1 };
    }
}
