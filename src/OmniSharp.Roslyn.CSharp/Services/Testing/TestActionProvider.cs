using OmniSharp.Models.V2;

namespace OmniSharp.Roslyn.CSharp.Services.Testing
{
    public class TestActionProvider
    {
        // private readonly IEventEmitter _emitter;

        public TestActionProvider()//IEventEmitter emitter)
        {
            // _emitter = emitter;
        }

        public ITestActionExecutor GetTestAction(RunCodeActionRequest request)
        {
            var identifier = request.Identifier;
            if (string.IsNullOrEmpty(identifier) || !request.Identifier.StartsWith("test"))
            {
                return null;
            }

            var sep = identifier.IndexOf('|', 4);
            if (sep == -1)
            {
                return null;
            }

            var action = identifier.Substring(5, sep - 5);
            var method = identifier.Substring(sep + 1);

            if (action != "run" && action != "debug")
            {
                return null;
            }
            else if (string.IsNullOrEmpty(method))
            {
                return null;
            }

            return new XunitTestActionExecutor(action, method);
        }

        private class XunitTestActionExecutor : ITestActionExecutor
        {
            private readonly string _action;
            private readonly string _method;
            
            public XunitTestActionExecutor(string action, string method)
            {
                _action = action;
                _method = method;
            }
            
            public void Run()
            {
                
            }
            
            public override string ToString()
            {
                return $"{nameof(XunitTestActionExecutor)} action: {_action}, method: {_method}";
            }
        }
    }
}