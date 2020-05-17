using System;
using Xunit;

namespace Main.Test
{
    public class MainTest
    {
        [Fact]
        public void Test()
        {
            Assert.True(true);
        } // This is here for boundary testing

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void DataDrivenTest1(int i)
        {
            Assert.True(i > 0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void DataDrivenTest2(int i)
        {
            Assert.True(i >= 0);
        }

        private void UtilityFunction()
        {

        }

        [Fact(DisplayName = "My Test Name")]
        public void UsesDisplayName()
        {
            Assert.True(true);
        }

        [Fact]
        public void TestWithSimilarName()
        {
            Assert.True(true);
        }

        [Fact]
        public void TestWithSimilarNameFooBar()
        {
            Assert.True(true);
        }

        [Fact]
        public void FailingTest()
        {
            Assert.Equal(1, 2);
        }

        [Fact]
        public void CheckStandardOutput()
        {
            int a = 1, b = 1;
            Console.WriteLine($"a = {a}, b = {b}");
            Assert.Equal(a,b);
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
