/////////////////////////////////////////////////////////////////////
// ADDINS
/////////////////////////////////////////////////////////////////////

#addin "nuget:?package=Polly&version=5.0.6"
#addin "nuget:?package=NuGet.Core&version=2.12.0"
#addin "nuget:?package=SharpZipLib&version=0.86.0"
#addin "nuget:?package=Cake.Compression&version=0.1.1"

//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////

#tool "nuget:https://dotnet.myget.org/F/nuget-build/?package=NuGet.CommandLine&version=4.3.0-beta1-2361&prerelease"

///////////////////////////////////////////////////////////////////////////////
// USINGS
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using Polly;
using NuGet;

///////////////////////////////////////////////////////////////////////////////
// SCRIPTS
///////////////////////////////////////////////////////////////////////////////

#load "./fileDownload.cake"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var platform = Argument("platform", "AnyCPU");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// CONFIGURATION
///////////////////////////////////////////////////////////////////////////////

var MainRepo = "VitalElement/AvalonStudio.Languages.CSharpPackage";
var MasterBranch = "master";
var ReleasePlatform = "Any CPU";
var ReleaseConfiguration = "Release";

///////////////////////////////////////////////////////////////////////////////
// PARAMETERS
///////////////////////////////////////////////////////////////////////////////

var isPlatformAnyCPU = StringComparer.OrdinalIgnoreCase.Equals(platform, "Any CPU");
var isPlatformX86 = StringComparer.OrdinalIgnoreCase.Equals(platform, "x86");
var isPlatformX64 = StringComparer.OrdinalIgnoreCase.Equals(platform, "x64");
var isLocalBuild = BuildSystem.IsLocalBuild;
var isRunningOnUnix = IsRunningOnUnix();
var isRunningOnWindows = IsRunningOnWindows();
var isRunningOnAppVeyor = BuildSystem.AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = BuildSystem.AppVeyor.Environment.PullRequest.IsPullRequest;
var isMainRepo = StringComparer.OrdinalIgnoreCase.Equals(MainRepo, BuildSystem.AppVeyor.Environment.Repository.Name);
var isMasterBranch = StringComparer.OrdinalIgnoreCase.Equals(MasterBranch, BuildSystem.AppVeyor.Environment.Repository.Branch);
var isTagged = BuildSystem.AppVeyor.Environment.Repository.Tag.IsTag 
               && !string.IsNullOrWhiteSpace(BuildSystem.AppVeyor.Environment.Repository.Tag.Name);
var isReleasable = StringComparer.OrdinalIgnoreCase.Equals(ReleasePlatform, platform) 
                   && StringComparer.OrdinalIgnoreCase.Equals(ReleaseConfiguration, configuration);
var isMyGetRelease = !isTagged && isReleasable;
var isNuGetRelease = isTagged && isReleasable;

var editbin = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\VC\Tools\MSVC\14.10.25017\bin\HostX86\x86\editbin.exe";

///////////////////////////////////////////////////////////////////////////////
// VERSION
///////////////////////////////////////////////////////////////////////////////

var version = "0.9.0";

if (isRunningOnAppVeyor)
{
    if (isTagged)
    {
        // Use Tag Name as version
        version = BuildSystem.AppVeyor.Environment.Repository.Tag.Name;
    }
    else
    {
        // Use AssemblyVersion with Build as version
        version += "-build" + EnvironmentVariable("APPVEYOR_BUILD_NUMBER") + "-alpha";
    }
}

///////////////////////////////////////////////////////////////////////////////
// DIRECTORIES
///////////////////////////////////////////////////////////////////////////////

var artifactsDir = (DirectoryPath)Directory("./artifacts");
var zipRootDir = artifactsDir.Combine("zip");
var nugetRoot = artifactsDir.Combine("nuget");
var fileZipSuffix = ".zip";


///////////////////////////////////////////////////////////////////////////////
// NUGET NUSPECS
///////////////////////////////////////////////////////////////////////////////

public NuGetPackSettings GetPackSettings(string rid)
{
    var nuspecNuGetBehaviors = new NuGetPackSettings()
    {
        Id = "AvalonStudio.Languages.CSharp." + rid,
        Version = version,
        Authors = new [] { "VitalElement" },
        Owners = new [] { "Dan Walmsley (dan at walms.co.uk)" },
        LicenseUrl = new Uri("http://opensource.org/licenses/MIT"),
        ProjectUrl = new Uri("https://github.com/VitalElement/"),
        RequireLicenseAcceptance = false,
        Symbols = false,
        NoPackageAnalysis = true,
        Description = "CSharp Support for AvalonStudio.",
        Copyright = "Copyright 2017",
        Tags = new [] { "AvalonStudio", "CSharp" },
        Files = new []
        {
            new NuSpecContent { Source = "**", Target = "content/" },
        },
        BasePath = Directory("artifacts/" + rid + "/"),
        OutputDirectory = nugetRoot
    };

    return nuspecNuGetBehaviors;
}

///////////////////////////////////////////////////////////////////////////////
// 3rd Party Downloads
///////////////////////////////////////////////////////////////////////////////

var toolchainDownloads = new List<ToolchainDownloadInfo> 
{ 
    new ToolchainDownloadInfo (artifactsDir)
    { 
        RID = "win-x64", 
        Downloads = new List<ArchiveDownloadInfo>()
        { 
            new ArchiveDownloadInfo()
            { 
                Format = "zip", 
                DestinationFile = "omnisharp.zip", 
                URL =  "https://github.com/OmniSharp/omnisharp-roslyn/releases/download/v1.22.0/omnisharp-win-x64-net46.zip",
                Name = "omnisharp-win-x64-net46",
                PostExtract = (curDir, info) =>{
                    
                }
            }
        }
    },
    new ToolchainDownloadInfo (artifactsDir)
    { 
        RID = "ubuntu-x64", 
        Downloads = new List<ArchiveDownloadInfo>()
        { 
            new ArchiveDownloadInfo()
            { 
                Format = "tar.gz", 
                DestinationFile = "omnisharp.tar.gz", 
                URL =  "https://github.com/OmniSharp/omnisharp-roslyn/releases/download/v1.22.0/omnisharp-debian.8-x64-netcoreapp1.1.tar.gz",
                Name = "omnisharp-debian.8-x64-netcoreapp1.1",
                PostExtract = (curDir, info) =>{
                    
                }
            }
        }
    },
    new ToolchainDownloadInfo (artifactsDir)
    { 
        RID = "osx-x64", 
        Downloads = new List<ArchiveDownloadInfo>()
        { 
            new ArchiveDownloadInfo()
            { 
                Format = "tar.gz", 
                DestinationFile = "omnisharp.tar.gz", 
                URL =  "https://github.com/OmniSharp/omnisharp-roslyn/releases/download/v1.22.0/omnisharp-osx-x64-netcoreapp1.1.tar.gz",
                Name = "omnisharp-osx-x64-netcoreapp1.1",
                PostExtract = (curDir, info) =>{
                    
                }
            }
        }
    }
};

///////////////////////////////////////////////////////////////////////////////
// INFORMATION
///////////////////////////////////////////////////////////////////////////////

Information("Building version {0} of AvalonStudio.Lanaguages.CSharp ({1}, {2}, {3}) using version {4} of Cake.", 
    version,
    platform,
    configuration,
    target,
    typeof(ICakeContext).Assembly.GetName().Version.ToString());

if (isRunningOnAppVeyor)
{
    Information("Repository Name: " + BuildSystem.AppVeyor.Environment.Repository.Name);
    Information("Repository Branch: " + BuildSystem.AppVeyor.Environment.Repository.Branch);
}

Information("Target: " + target);
Information("Platform: " + platform);
Information("Configuration: " + configuration);
Information("IsLocalBuild: " + isLocalBuild);
Information("IsRunningOnUnix: " + isRunningOnUnix);
Information("IsRunningOnWindows: " + isRunningOnWindows);
Information("IsRunningOnAppVeyor: " + isRunningOnAppVeyor);
Information("IsPullRequest: " + isPullRequest);
Information("IsMainRepo: " + isMainRepo);
Information("IsMasterBranch: " + isMasterBranch);
Information("IsTagged: " + isTagged);
Information("IsReleasable: " + isReleasable);
Information("IsMyGetRelease: " + isMyGetRelease);
Information("IsNuGetRelease: " + isNuGetRelease);

///////////////////////////////////////////////////////////////////////////////
// TASKS
/////////////////////////////////////////////////////////////////////////////// 

Task("Clean")
.Does(()=>{    
    CleanDirectory(artifactsDir);
    CleanDirectory(nugetRoot);

    foreach(var tc in toolchainDownloads)
    {
        CleanDirectory(tc.BaseDir);   
        CleanDirectory(tc.ZipDir);
    }
});

Task("Download")
.Does(()=>{
    foreach(var tc in toolchainDownloads)
    {
        foreach(var downloadInfo in tc.Downloads)
        {
            var fileName = tc.ZipDir.CombineWithFilePath(downloadInfo.DestinationFile);

            if(!FileExists(fileName))
            {
                DownloadFile(downloadInfo.URL, fileName);
            }
        }
    }
});

Task("Extract")
.Does(()=>{
    foreach(var tc in toolchainDownloads)
    {
        foreach(var downloadInfo in tc.Downloads)
        {
            var fileName = tc.ZipDir.CombineWithFilePath(downloadInfo.DestinationFile);
            var dest = tc.BaseDir;

            switch (downloadInfo.Format)
            {
                case "tar.bz2":
                BZip2Uncompress(fileName, dest);
                break;

                case "tar.gz":
                GZipUncompress(fileName, dest);
                break;

                case "zip":
                ZipUncompress(fileName, dest);
                break;

                case "none":
                break;

                default:
                case "tar.xz":
                StartProcess("7z", new ProcessSettings{ Arguments = string.Format("x {0} -o{1}", fileName, dest) });
                break;
            }        

            if(downloadInfo.PostExtract != null)
            {
                downloadInfo.PostExtract(dest, downloadInfo);
            }
        }
    }
});

Task("Generate-NuGetPackages")
.IsDependentOn("Download")
.IsDependentOn("Extract")
.Does(()=>{
    foreach(var tc in toolchainDownloads)
    {
        NuGetPack(GetPackSettings(tc.RID));
    }
});

Task("Publish-AppVeyorNuget")
    .IsDependentOn("Generate-NuGetPackages")        
    .WithCriteria(() => isMainRepo)
    .WithCriteria(() => isMasterBranch)    
    .Does(() =>
{
    var apiKey = EnvironmentVariable("MYGET_API_KEY");
    if(string.IsNullOrEmpty(apiKey)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API key.");
    }

    var apiUrl = EnvironmentVariable("MYGET_API_URL");
    if(string.IsNullOrEmpty(apiUrl)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API url.");
    }

    foreach(var tc in toolchainDownloads)
    {
        var nuspec = GetPackSettings(tc.RID);
        var settings  = nuspec.OutputDirectory.CombineWithFilePath(string.Concat(nuspec.Id, ".", nuspec.Version, ".nupkg"));

        NuGetPush(settings, new NuGetPushSettings
        {
            Source = apiUrl,
            ApiKey = apiKey
        });
    }
});

Task("Default")    
    .IsDependentOn("Clean")
    .IsDependentOn("Publish-AppVeyorNuget");
    
RunTarget(target);
