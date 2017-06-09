namespace OmniSharp
{
    public static class OmniSharpEndpoints
    {
        public const string GotoDefinition = "/gotodefinition";
        public const string FindSymbols = "/findsymbols";
        public const string UpdateBuffer = "/updatebuffer";
        public const string ChangeBuffer = "/changebuffer";
        public const string CodeCheck = "/codecheck";
        public const string FilesChanged = "/filesChanged";
        public const string FormatAfterKeystroke = "/formatAfterKeystroke";
        public const string FormatRange = "/formatRange";
        public const string CodeFormat = "/codeformat";
        public const string Highlight = "/highlight";
        public const string AutoComplete = "/autocomplete";
        public const string FindImplementations = "/findimplementations";
        public const string FindUsages = "/findusages";
        public const string GotoFile = "/gotofile";
        public const string GotoRegion = "/gotoregion";
        public const string NavigateUp = "/navigateup";
        public const string NavigateDown = "/navigatedown";
        public const string TypeLookup = "/typelookup";
        public const string GetCodeAction = "/getcodeactions";
        public const string RunCodeAction = "/runcodeaction";
        public const string Rename = "/rename";
        public const string SignatureHelp = "/signatureHelp";
        public const string MembersTree = "/currentfilemembersastree";
        public const string MembersFlat = "/currentfilemembersasflat";
        public const string TestCommand = "/gettestcontext";
        public const string Metadata = "/metadata";
        public const string PackageSource = "/packagesource";
        public const string PackageSearch = "/packagesearch";
        public const string PackageVersion = "/packageversion";
        public const string WorkspaceInformation = "/projects";
        public const string ProjectInformation = "/project";
        public const string FixUsings = "/fixusings";

        public const string CheckAliveStatus = "/checkalivestatus";
        public const string CheckReadyStatus = "/checkreadystatus";
        public const string StopServer = "/stopserver";

        public const string Open = "/open";
        public const string Close = "/close";
        public const string Diagnostics = "/diagnostics";

        public static class V2
        {
            public const string GetCodeActions = "/v2/getcodeactions";
            public const string RunCodeAction = "/v2/runcodeaction";

            public const string GetTestStartInfo = "/v2/getteststartinfo";
            public const string RunTest = "/v2/runtest";
            public const string DebugTestGetStartInfo = "/v2/debugtest/getstartinfo";
            public const string DebugTestLaunch = "/v2/debugtest/launch";
            public const string DebugTestStop = "/v2/debugtest/stop";
        }
    }
}
