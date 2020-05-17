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
        } // This is here for boundary testing

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
            Assert.AreEqual(a, b);
        }

        public void UtilityFunction()
        {

        }

        private static int[] _items = new int[1] { 1 };
    }

    [TestFixture(typeof(int))]
    [TestFixture(typeof(double))]
    public class GenericTest<T>
    {
        [Test]
        public void TypedTest()
        {
            Assert.NotNull(default(T));
        }

        [Test]
        public void TypedTest2()
        {
            Assert.Null(default(T));
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
