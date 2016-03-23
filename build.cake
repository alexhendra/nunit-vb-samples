//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");

//////////////////////////////////////////////////////////////////////
// DISCOVERY VARS
//////////////////////////////////////////////////////////////////////

string[] SolutionList = null;
string[] ProjList = null;
var PROJ_EXT = "*.vbproj";

//////////////////////////////////////////////////////////////////////
// DEFINE RUN CONSTANTS
//////////////////////////////////////////////////////////////////////

var ROOT_DIR = Context.Environment.WorkingDirectory.FullPath + "/";
var TOOLS_DIR = ROOT_DIR + "tools/";
var NUNIT3_CONSOLE = TOOLS_DIR + "NUnit.ConsoleRunner.3.2.0/tools/nunit3-console.exe";

//////////////////////////////////////////////////////////////////////
// ERROR LOG
//////////////////////////////////////////////////////////////////////

var ErrorDetail = new List<string>();

//////////////////////////////////////////////////////////////////////
// DISCOVER SOLUTIONS
//////////////////////////////////////////////////////////////////////

Task("DiscoverSolutions")
.Does(() =>
    {
        SolutionList = System.IO.Directory.GetFiles(ROOT_DIR, "*.sln", SearchOption.AllDirectories);
        ProjList = System.IO.Directory.GetFiles(ROOT_DIR, PROJ_EXT, SearchOption.AllDirectories);
    });

//////////////////////////////////////////////////////////////////////
// CLEAN
//////////////////////////////////////////////////////////////////////

Task("Clean")
.IsDependentOn("DiscoverSolutions")
.Does(() =>
    {
        foreach(var proj in ProjList)
            CleanDirectory(DirFrom(proj) + "/bin");
    });

//////////////////////////////////////////////////////////////////////
// RESTORE PACKAGES
//////////////////////////////////////////////////////////////////////

Task("InitializeBuild")
.IsDependentOn("DiscoverSolutions")
.Does(() =>
    {
        foreach(var sln in SolutionList)
            NuGetRestore(sln);
    });

//////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////

Task("Build")
.IsDependentOn("InitializeBuild")
.Does(() =>
    {
        foreach(var proj in ProjList)
            BuildProject(proj);
        
    });

//////////////////////////////////////////////////////////////////////
// RESTORE NUNIT CONSOLE
//////////////////////////////////////////////////////////////////////

Task("RestoreNUnitConsole")
.Does(() => 
    {
        NuGetInstall("NUnit.ConsoleRunner", 
            new NuGetInstallSettings()
            {
                Version = "3.2.0",
                OutputDirectory = TOOLS_DIR
            });
    });

//////////////////////////////////////////////////////////////////////
// TEST
//////////////////////////////////////////////////////////////////////

Task("Test")
.IsDependentOn("Rebuild")
.IsDependentOn("RestoreNUnitConsole")
.OnError(exception => ErrorDetail.Add(exception.Message))
.Does(() =>
    {
        foreach(var proj in ProjList)
        {
            var bin = DirFrom(proj) + "/bin/";
            var dllName = bin + System.IO.Path.GetFileNameWithoutExtension(proj) + ".dll";

            int rc = StartProcess(NUNIT3_CONSOLE,
                                new ProcessSettings()
                                {
                                    Arguments = dllName
                                });

            if (rc > 0)
                ErrorDetail.Add(string.Format("{0}: {1} tests failed", dllName, rc));
            else if (rc < 0)
                ErrorDetail.Add(string.Format("{0} exited with rc = {1}", dllName, rc));
        }
    });

//////////////////////////////////////////////////////////////////////
// TEARDOWN TASK
//////////////////////////////////////////////////////////////////////

Teardown(() =>
{
    CheckForError(ref ErrorDetail);
});

void CheckForError(ref List<string> errorDetail)
{
    if(errorDetail.Count != 0)
    {
        var copyError = new List<string>();
        copyError = errorDetail.Select(s => s).ToList();
        errorDetail.Clear();
        throw new Exception("One or more unit tests failed, breaking the build.\n"
            + copyError.Aggregate((x,y) => x + "\n" + y));
    }
}

//////////////////////////////////////////////////////////////////////
// HELPER METHODS
//////////////////////////////////////////////////////////////////////

void BuildProject(string projPath)
{
    if (IsRunningOnWindows())
    {
        MSBuild(projPath, new MSBuildSettings()
            .SetConfiguration(configuration)
            .SetMSBuildPlatform(MSBuildPlatform.Automatic)
            .SetVerbosity(Verbosity.Minimal)
            .SetNodeReuse(false));
    }
    else
    {
        XBuild(projPath, new XBuildSettings()
            .WithTarget("Build")
            .WithProperty("Configuration", configuration)
            .SetVerbosity(Verbosity.Minimal));
    }
}

string DirFrom(string filePath)
{
    return System.IO.Path.GetDirectoryName(filePath);
}

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Rebuild")
.IsDependentOn("Clean")
.IsDependentOn("Build");

Task("Default")
.IsDependentOn("Rebuild");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);