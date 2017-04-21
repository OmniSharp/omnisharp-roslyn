using System.Linq;
using System.Threading.Tasks;
using OmniSharp.Models.Navigate;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using TestUtility;
using Xunit;
using Xunit.Abstractions;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    enum Direction
    {
        Up,
        Down
    }

    public class NavigateUpDownFacts : AbstractTestFixture
    {
        public NavigateUpDownFacts(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task NavigateUp_ReturnsCorrectPositionFromMethodBodyToMethodName()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> {|end:|}NavigateUp(Request request)
                        {
                            return await Navi{|start:|}gate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }";

            await AssertEndPosition(fileContent, Direction.Up);
        }

        [Fact]
        public async Task NavigateUp_ReturnsCorrectPositionFromMethodNameToPreviousMethodName()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> {|end:|}NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        [HttpPost(""navigatedown"")]
                        public async {|start:|}Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Up);
        }

        [Fact]
        public async Task NavigateUp_ReturnsCorrectPositionFromMethodNameToPreviousField()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        private string {|end:|}text;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> {|start:|}NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Up);
        }

        [Fact]
        public async Task NavigateUp_ReturnsCorrectPositionFromPropertyBodyToPropertyName()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        private string text;

                        public string {|end:|}MoreText
                        {
                            get
                            {
                                return text;
                            }
                            set
                            {
                                text = valu{|start:|}e;
                            }
                        }

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Up);
        }

        [Fact]
        public async Task NavigateUp_ReturnsCorrectPositionFromFirstMemberToEnclosingClass()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class {|end:|}NavigateController
                    {
                        public string navigationText;{|start:|}

                            [HttpPost(""navigateup"")]
                            public async Task<NavigateResponse> NavigateUp(Request request)
                            {
                                return await Navigate(request);
                            }
                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Up);
        }

        [Fact]
        public async Task NavigateUp_ReturnsCorrectPositionFromClassToPreviousClassLastMember()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        public string navigationText;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> {|end:|}Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                    p{|start:|}ublic class AnotherController
                    {
                        public string navigationText;
                    }

                }";

            await AssertEndPosition(fileContent, Direction.Up);
        }

        [Fact]
        public async Task NavigateUp_ReturnsCorrectPositionFromNestedClassConstructorToPreviousField()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        public string navigationText;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        private class NestedController
                        {
                            private string {|end:|}text;

                            private Nested{|start:|}Controller()
                            {
                                Console.WriteLine(""In nested controller constructor"");
                            }
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Up);
        }

        [Fact]
        public async Task NavigateUp_ReturnsCorrectPositionFromNestedClassNameToEnclosingClassPreviousMember()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        public string navigationText;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> {|end:|}NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        //Introducing a commented member
                        //public void PrintString()
                        //{
                        //    Console.Writeline(""Do nothing else"");
                        //}

                        {|start:|}private class NestedController
                        {
                            public string Text;
                            public NestedController()
                            {
                                Console.WriteLine(""In nested controller constructor"");
                            }
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Up);
        }

        [Fact]
        public async Task NavigateUp_ReturnsCorrectPositionFromBeforeFirstClassToNoChange()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace {|start:|}{|end:|}OmniSharp
                {
                    public class NavigateController
                    {
                        public string navigationText;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Up);
        }

        [Fact]
        public async Task NavigateDown_ReturnsCorrectPositionFromMethodBodyToNextMethodName()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> NavigateUp(Request request)
                        {
                            return await Nav{|start:|}igate(request);
                        }

                        private async Task<NavigateResponse> {|end:|}Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }

                }";

            await AssertEndPosition(fileContent, Direction.Down);
        }

        [Fact]
        public async Task NavigateDown_ReturnsCorrectPositionFromMethodNameToNextMethodName()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        [HttpPost(""navigateup"")]
                        public async {|start:|}Task<NavigateResponse> NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> {|end:|}NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Down);
        }

        [Fact]
        public async Task NavigateDown_ReturnsCorrectPositionFromPropertyBodyToNextMemberName()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        public string text;

                        public string MoreText
                        {
                            get
                            {
                                return text;
                            }
                            set
                            {
                                this.te{|start:|}xt = value;
                            }
                        }

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> {|end:|}NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Down);
        }


        [Fact]
        public async Task NavigateDown_ReturnsCorrectPositionFromFieldToNextMethodName()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        public stri{|start:|}ng Text;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> {|end:|}NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Down);
        }

        [Fact]
        public async Task NavigateDown_ReturnsCorrectPositionFromClassNameToNextMember()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class Navigat{|start:|}eController
                    {
                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> {|end:|}NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }
                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Down);
        }

        [Fact]
        public async Task NavigateDown_ReturnsCorrectPositionFromClassLastMemberToNextClass()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        public string navigationText;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateRespo{|start:|}nse();
                        }
                    }
                    public class {|end:|}AnotherController
                    {
                        public string navigationText;
                    }

                }";

            await AssertEndPosition(fileContent, Direction.Down);
        }

        [Fact]
        public async Task NavigateDown_ReturnsCorrectPositionFromNestedClassConstructorToNextField()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        public string navigationText;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        private class NestedController
                        {
                            public string Text;
                            public Nested{|start:|}Controller()
                            {
                                Console.WriteLine(""In nested controller constructor"");
                            }
                            public void {|end:|}AnotherMethod()
                            {
                                Console.WriteLine(""In nested controller method"");
                            }
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Down);
        }

        [Fact]
        public async Task NavigateDown_ReturnsCorrectPositionFromNestedClassNameToEnclosingClassNextMember()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        public string navigationText;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        //Introducing a commented member
                        //public void PrintString()
                        //{
                        //    Console.Writeline(""Do nothing else"");
                        //}

                        private class NestedController
                        {
                            public string Text;
                            public NestedController()
                            {
                                Consol{|start:|}e.WriteLine(""In nested controller constructor"");
                            }
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> {|end:|}Navigate(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Down);
        }

        [Fact]
        public async Task NavigateDown_ReturnsCorrectPositionFromLastClassLastMemberToNoChange()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace OmniSharp
                {
                    public class NavigateController
                    {
                        public string navigationText;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new {|start:|}{|end:|}NavigateResponse();
                        }
                    }
                }";

            await AssertEndPosition(fileContent, Direction.Down);
        }

        private async Task AssertEndPosition(string source, Direction direction)
        {
            var testFile = new TestFile("test.cs", source);

            var start = testFile.Content.GetSpans("start").Single().Start;
            var end = testFile.Content.GetSpans("end").Single().Start;

            var startPoint = testFile.Content.Text.GetPointFromPosition(start);
            var endPoint = testFile.Content.Text.GetPointFromPosition(end);

            using (var host = CreateOmniSharpHost(testFile))
            {
                var response = await SendRequest(host, testFile, startPoint.Line, startPoint.Offset, direction);

                Assert.Equal(endPoint.Line, response.Line);
                Assert.Equal(endPoint.Offset, response.Column);
            }
        }

        private static async Task<NavigateResponse> SendRequest(
            OmniSharpTestHost host,
            TestFile testFile,
            int startLine,
            int startColumn,
            Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    {
                        var requestHandler = host.GetRequestHandler<NavigateUpService>(OmniSharpEndpoints.NavigateUp);
                        var request = new NavigateUpRequest
                        {
                            Line = startLine,
                            Column = startColumn,
                            FileName = testFile.FileName,
                            Buffer = testFile.Content.Code
                        };

                        return await requestHandler.Handle(request);
                    }

                case Direction.Down:
                    {
                        var requestHandler = host.GetRequestHandler<NavigateDownService>(OmniSharpEndpoints.NavigateDown);
                        var request = new NavigateDownRequest
                        {
                            Line = startLine,
                            Column = startColumn,
                            FileName = testFile.FileName,
                            Buffer = testFile.Content.Code
                        };

                        return await requestHandler.Handle(request);
                    }
            }

            return null;
        }
    }
}
