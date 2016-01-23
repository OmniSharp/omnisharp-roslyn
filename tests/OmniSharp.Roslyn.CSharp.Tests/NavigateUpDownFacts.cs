using System.Threading.Tasks;
using OmniSharp.Models;
using OmniSharp.Roslyn.CSharp.Services.Navigation;
using OmniSharp.Tests;
using Xunit;

namespace OmniSharp.Roslyn.CSharp.Tests
{
    enum NavigateDirection
    {
        UP,
        DOWN
    }

    public class NavigateUpDownFacts
    {
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
                        public async Task<NavigateResponse> %NavigateUp(Request request)
                        {
                            return await Navi$gate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }";

            await AssertPosition(fileContent, NavigateDirection.UP);
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
                        public async Task<NavigateResponse> %NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        [HttpPost(""navigatedown"")]
                        public async $Task<NavigateResponse> NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertPosition(fileContent, NavigateDirection.UP);
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
                        private string %text;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> $NavigateUp(Request request)
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

            await AssertPosition(fileContent, NavigateDirection.UP);
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

                        public string %MoreText
                        {
                            get
                            {
                                return text;
                            }
                            set
                            {
                                text = valu$e;
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

            await AssertPosition(fileContent, NavigateDirection.UP);
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
                    public class %NavigateController
                    {
                        public string navigationText;$

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

            await AssertPosition(fileContent, NavigateDirection.UP);
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

                        private async Task<NavigateResponse> %Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                    p$ublic class AnotherController
                    {
                        public string navigationText;
                    }

                }";

            await AssertPosition(fileContent, NavigateDirection.UP);
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
                            private string %text;

                            private Nested$Controller()
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

            await AssertPosition(fileContent, NavigateDirection.UP);
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
                        public async Task<NavigateResponse> %NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        //Introducing a commented member
                        //public void PrintString()
                        //{
                        //    Console.Writeline(""Do nothing else"");
                        //}

                        $private class NestedController
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

            await AssertPosition(fileContent, NavigateDirection.UP);
        }

        [Fact]
        public async Task NavigateUp_ReturnsCorrectPositionFromBeforeFirstClassToNoChange()
        {
            const string fileContent = @"using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;
                using Microsoft.AspNetCore.Mvc;
                using OmniSharp.Models;

                namespace $%OmniSharp
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

            await AssertPosition(fileContent, NavigateDirection.UP);
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
                            return await Nav$igate(request);
                        }

                        private async Task<NavigateResponse> %Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }

                }";

            await AssertPosition(fileContent, NavigateDirection.DOWN);
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
                        public async $Task<NavigateResponse> NavigateUp(Request request)
                        {
                            return await Navigate(request);
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> %NavigateDown(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertPosition(fileContent, NavigateDirection.DOWN);
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
                                this.te$xt = value;
                            }
                        }

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> %NavigateUp(Request request)
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

            await AssertPosition(fileContent, NavigateDirection.DOWN);
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
                        public stri$ng Text;

                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> %NavigateUp(Request request)
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

            await AssertPosition(fileContent, NavigateDirection.DOWN);
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
                    public class Navigat$eController
                    {
                        [HttpPost(""navigateup"")]
                        public async Task<NavigateResponse> %NavigateUp(Request request)
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

            await AssertPosition(fileContent, NavigateDirection.DOWN);
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
                            return new NavigateRespo$nse();
                        }
                    }
                    public class %AnotherController
                    {
                        public string navigationText;
                    }

                }";

            await AssertPosition(fileContent, NavigateDirection.DOWN);
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
                            public Nested$Controller()
                            {
                                Console.WriteLine(""In nested controller constructor"");
                            }
                            public void %AnotherMethod()
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

            await AssertPosition(fileContent, NavigateDirection.DOWN);
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
                                Consol$e.WriteLine(""In nested controller constructor"");
                            }
                        }

                        [HttpPost(""navigatedown"")]
                        public async Task<NavigateResponse> %Navigate(Request request)
                        {
                            return await Navigate(request);
                        }

                        private async Task<NavigateResponse> Navigate(Request request) {
                            return new NavigateResponse();
                        }
                    }
                }";

            await AssertPosition(fileContent, NavigateDirection.DOWN);
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
                            return new $%NavigateResponse();
                        }
                    }
                }";

            await AssertPosition(fileContent, NavigateDirection.DOWN);
        }

        private async Task AssertPosition(string fileContent, NavigateDirection navigateDirection)
        {
            var fileContentNoPercentMarker = TestHelpers.RemovePercentMarker(fileContent);
            var workspace = await TestHelpers.CreateSimpleWorkspace(fileContentNoPercentMarker, "test.cs");
            var response = await SendRequest(workspace, "test.cs", fileContentNoPercentMarker, navigateDirection);
            var finalCursorLineColumn = TestHelpers.GetLineAndColumnFromPercent(TestHelpers.RemoveDollarMarker(fileContent));
            Assert.Equal(finalCursorLineColumn.Line, response.Line);
            Assert.Equal(finalCursorLineColumn.Column, response.Column);
        }

        private async Task<NavigateResponse> SendRequest(OmnisharpWorkspace workspace, string fileName, string fileContent, NavigateDirection upOrDown)
        {
            var initialCursorLineColumn = TestHelpers.GetLineAndColumnFromDollar(TestHelpers.RemovePercentMarker(fileContent));
            var fileContentNoDollarMarker = TestHelpers.RemoveDollarMarker(fileContent);
            var naviagteUpService = new NavigateUpService(workspace);
            var navigateDownService = new NavigateDownService(workspace);

            if (upOrDown == NavigateDirection.UP)
            {
                var request = new NavigateUpRequest
                {
                    Line = initialCursorLineColumn.Line,
                    Column = initialCursorLineColumn.Column,
                    FileName = fileName,
                    Buffer = fileContentNoDollarMarker
                };
                return await naviagteUpService.Handle(request);
            }
            else
            {
                var request = new NavigateDownRequest
                {
                    Line = initialCursorLineColumn.Line,
                    Column = initialCursorLineColumn.Column,
                    FileName = fileName,
                    Buffer = fileContentNoDollarMarker
                };
                return await navigateDownService.Handle(request);
            }
        }
    }
}
