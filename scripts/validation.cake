#load "common.cake"
#load "runhelpers.cake"

void ValidateMonoVersion(BuildPlan plan)
{
    if (Platform.Current.IsWindows)
    {
        return;
    }

    // We require a minimum version of Mono for building on macOS/Linux. The version is specified in
    // 'RequiredMonoVersion' of build.json.

    var monoVersionOutput = new List<string>();
    Run("mono", "--version", new RunOptions(output: monoVersionOutput))
        .ExceptionOnError($"Could not launch 'mono'. Please ensure that Mono {plan.RequiredMonoVersion} or later is installed and on the path.");

    var ErrorMessage = $"Could not detect Mono version. Please ensure that Mono {plan.RequiredMonoVersion} or later is installed and on the path.";

    if (monoVersionOutput.Count == 0)
    {
        throw new Exception(ErrorMessage);
    }

    var monoVersionText = monoVersionOutput[0];

    if (string.IsNullOrWhiteSpace(monoVersionText))
    {
        throw new Exception(ErrorMessage);
    }

    // The first line of Mono's '--version' output looks like so:
    //
    //     Mono JIT compiler version 5.2.0.196 (2017-02/5077205 Thu May 18 16:11:37 EDT 2017)
    //
    // Our approach at parsing out the version number is to search for the open parenthesis,
    // and then grab the word before that.

    var openParenIndex = monoVersionText.IndexOf("(");
    if (openParenIndex < 0)
    {
        throw new Exception(ErrorMessage);
    }

    monoVersionText = monoVersionText.Substring(0, openParenIndex).Trim();

    var lastSpaceIndex = monoVersionText.LastIndexOf(" ");
    if (lastSpaceIndex < 0)
    {
        throw new Exception(ErrorMessage);
    }

    monoVersionText = monoVersionText.Substring(lastSpaceIndex).Trim();

    var monoVersion = new System.Version(monoVersionText);

    if (monoVersion < new System.Version(plan.RequiredMonoVersion))
    {
        throw new Exception($"Detected Mono {monoVersion}, but a version >= {plan.RequiredMonoVersion} is required.");
    }

    Information("Detected Mono version {0}", monoVersion);
}