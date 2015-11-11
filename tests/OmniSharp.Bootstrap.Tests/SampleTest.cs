using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using OmniSharp.Bootstrap;
using Microsoft.Framework.Runtime;
using System.Runtime.Versioning;
using System.IO;

namespace OmniSharp.Bootstrap.Tests
{
    class FakeApplicationEnvironment : IApplicationEnvironment
    {
        public string ApplicationBasePath
        {
            get
            {
                return Directory.GetCurrentDirectory();
            }
        }

        public string ApplicationName
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Configuration
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public FrameworkName RuntimeFramework
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public string Version
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }

    public class SampleTest
    {
        [Fact]
        public void Test1()
        {
            var program = new Program(new FakeApplicationEnvironment());

            program.ParseArguments(new string[] { });
            Assert.True(false);
        }
    }
}
