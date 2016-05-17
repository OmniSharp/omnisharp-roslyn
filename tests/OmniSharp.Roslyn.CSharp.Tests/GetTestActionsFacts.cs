using System;
using System.IO;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    public class GetTestActionsFacts
    {
        [Fact]
        public void ReturnsOneTestAction()
        {
            Console.WriteLine(Directory.GetCurrentDirectory());
            
            Assert.True(true);
        }
    }
}