using NUnit.Framework;

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

        public void UtilityFunction()
        {

        }

        private static int[] _items = new int[1] { 1 };
    }
}
