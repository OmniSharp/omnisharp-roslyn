using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Main.Test
{
    [TestClass]
    public class MainTest
    {
        [TestMethod]
        public void Test()
        {
            Assert.IsTrue(true);
        }

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

        private void UtilityFunction()
        {
            
        }
    }
}
