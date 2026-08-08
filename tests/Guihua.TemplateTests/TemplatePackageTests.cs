using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace Guihua.TemplateTests;

public sealed class TemplatePackageTests
{
    [Fact]
    public void RealPackageCanInstallHelpGenerateAndBuildWithinItsBoundary()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runId = Guid.NewGuid().ToString("N");
        var artifactRoot = Path.Combine(repositoryRoot, "artifacts", "template-tests", runId);
        var packageOutput = Path.Combine(artifactRoot, "packages");
        var hive = Path.Combine(artifactRoot, "custom-hive");
        var dotnetHome = Path.Combine(artifactRoot, "dotnet-home");
        var generatedRoot = Path.Combine(repositoryRoot, "artifacts", "generated", "T-12", runId);

        Directory.CreateDirectory(packageOutput);
        Directory.CreateDirectory(hive);
        Directory.CreateDirectory(dotnetHome);
        Directory.CreateDirectory(generatedRoot);
        Environment.SetEnvironmentVariable("DOTNET_CLI_HOME", dotnetHome);
        Environment.SetEnvironmentVariable("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1");
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");

        var packageProject = Path.Combine(repositoryRoot, "packaging", "Guihua.Templates.csproj");
        var pack = Run(repositoryRoot, "pack", packageProject, "-c", "Release", "-o", packageOutput, "--nologo");
        Assert.True(pack.ExitCode == 0, pack.ToString());

        var packagePath = Assert.Single(Directory.EnumerateFiles(packageOutput, "Guihua.Templates.*.nupkg"));
        AssertPackageBoundary(packagePath);

        var install = Run(repositoryRoot, "new", "install", packagePath, "--force", "--debug:custom-hive", hive, "-v", "q");
        Assert.True(install.ExitCode == 0, install.ToString());

        var help = Run(repositoryRoot, "new", "guihua", "-h", "--debug:custom-hive", hive);
        Assert.True(help.ExitCode == 0, help.ToString());
        Assert.Contains("--framework", help.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("net8.0", help.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("net10.0", help.StandardOutput, StringComparison.OrdinalIgnoreCase);

        var moduleHelp = Run(repositoryRoot, "new", "guihua-module", "-h", "--debug:custom-hive", hive);
        Assert.True(moduleHelp.ExitCode == 0, moduleHelp.ToString());
        Assert.Contains("explicit", moduleHelp.StandardOutput, StringComparison.OrdinalIgnoreCase);

        var toolHelp = Run(repositoryRoot, "new", "guihua-tool", "-h", "--debug:custom-hive", hive);
        Assert.True(toolHelp.ExitCode == 0, toolHelp.ToString());
        Assert.Contains("--framework", toolHelp.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("net8.0", toolHelp.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("net10.0", toolHelp.StandardOutput, StringComparison.OrdinalIgnoreCase);

        var invalidOutput = Path.Combine(generatedRoot, "Invalid");
        var invalid = Run(repositoryRoot, "new", "guihua", "-n", "Invalid", "-o", invalidOutput, "--framework", "net7.0", "--no-update-check", "--debug:custom-hive", hive, "-v", "q");
        Assert.NotEqual(0, invalid.ExitCode);

        GenerateAndVerify(repositoryRoot, hive, generatedRoot, "net8.0");
        GenerateAndVerify(repositoryRoot, hive, generatedRoot, "net10.0");
    }

    private static void GenerateAndVerify(string repositoryRoot, string hive, string generatedRoot, string framework)
    {
        var sampleName = framework == "net8.0" ? "Net8Sample" : "Net10Sample";
        var output = Path.Combine(generatedRoot, framework);
        var generationArguments = new List<string>
        {
            "new", "guihua", "-n", sampleName, "-o", output
        };
        if (framework == "net10.0")
        {
            generationArguments.AddRange(["--framework", framework]);
        }

        generationArguments.AddRange(["--no-update-check", "--debug:custom-hive", hive, "-v", "q"]);
        var generate = Run(repositoryRoot, [.. generationArguments]);
        Assert.True(generate.ExitCode == 0, generate.ToString());

        var props = File.ReadAllText(Path.Combine(output, "Directory.Build.props"));
        Assert.Contains($"<TargetFramework>{framework}</TargetFramework>", props, StringComparison.Ordinal);
        Assert.DoesNotContain("__GUIHUA_", props, StringComparison.Ordinal);

        var globalJson = File.ReadAllText(Path.Combine(output, "global.json"));
        var expectedSdk = framework == "net8.0" ? "8.0.423" : "10.0.302";
        Assert.Contains($"\"version\": \"{expectedSdk}\"", globalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("__GUIHUA_", globalJson, StringComparison.Ordinal);

        var packageProps = File.ReadAllText(Path.Combine(output, "Directory.Packages.props"));
        Assert.Contains("Condition=\"'$(TargetFramework)' == 'net8.0'\"", packageProps, StringComparison.Ordinal);
        Assert.Contains("Condition=\"'$(TargetFramework)' == 'net10.0'\"", packageProps, StringComparison.Ordinal);
        Assert.DoesNotContain("<TargetFrameworks>", props, StringComparison.Ordinal);

        AssertGeneratedBoundary(output, sampleName);

        var solution = Path.Combine(output, $"{sampleName}.sln");
        var restore = Run(output, "restore", solution, "--nologo");
        Assert.True(restore.ExitCode == 0, restore.ToString());
        AssertResolvedPackageVersion(output, framework);
        AssertNoOptInPackages(output);

        var build = Run(output, "build", solution, "-c", "Release", "--no-restore", "--nologo", "-warnaserror");
        Assert.True(build.ExitCode == 0, build.ToString());

        var test = Run(output, "test", solution, "-c", "Release", "--no-build", "--no-restore", "--nologo");
        Assert.True(test.ExitCode == 0, test.ToString());

        RunApiAndVerify(output, sampleName);
    }

    private static void RunApiAndVerify(string output, string sampleName)
    {
        var port = GetAvailableLoopbackPort();
        var address = $"http://127.0.0.1:{port}";
        var project = Path.Combine(output, "src", $"{sampleName}.Api", $"{sampleName}.Api.csproj");
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = output,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
                 {
                     "run", "--project", project, "-c", "Release", "--no-build", "--no-restore", "--urls", address
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var started = false;
        var outputLog = new StringBuilder();
        var errorLog = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                outputLog.AppendLine(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                errorLog.AppendLine(eventArgs.Data);
            }
        };

        try
        {
            process.Start();
            started = true;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    Assert.Fail($"Generated API exited before it became ready.\nSTDOUT:\n{outputLog}\nSTDERR:\n{errorLog}");
                }

                try
                {
                    using var response = client.GetAsync($"{address}/api/demo/greeting").GetAwaiter().GetResult();
                    var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    Assert.Contains("\"code\":200", body, StringComparison.Ordinal);

                    using var runtimeResponse = client.GetAsync($"{address}/api/demo/runtime-status").GetAwaiter().GetResult();
                    var runtimeBody = runtimeResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    Assert.Equal(HttpStatusCode.OK, runtimeResponse.StatusCode);
                    Assert.Contains("\"state\":\"Ready\"", runtimeBody, StringComparison.Ordinal);
                    return;
                }
                catch (HttpRequestException)
                {
                    Thread.Sleep(250);
                }
                catch (TaskCanceledException)
                {
                    Thread.Sleep(250);
                }
            }

            Assert.Fail($"Generated API did not become ready at {address}.\nSTDOUT:\n{outputLog}\nSTDERR:\n{errorLog}");
        }
        finally
        {
            if (started && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            if (started)
            {
                process.WaitForExit();
            }
        }
    }

    private static int GetAvailableLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void AssertResolvedPackageVersion(string output, string framework)
    {
        var assets = Directory.EnumerateFiles(output, "project.assets.json", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var expected = framework == "net8.0" ? "microsoft.aspnetcore.mvc.testing/8.0.29" : "microsoft.aspnetcore.mvc.testing/10.0.10";
        var unexpected = framework == "net8.0" ? "microsoft.aspnetcore.mvc.testing/10.0.10" : "microsoft.aspnetcore.mvc.testing/8.0.29";

        Assert.NotEmpty(assets);
        Assert.Contains(assets, content => content.Contains(expected, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(assets, content => content.Contains(unexpected, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertNoOptInPackages(string output)
    {
        var assets = Directory.EnumerateFiles(output, "project.assets.json", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();
        var optInPackages = new[]
        {
            "freesql/",
            "stackexchange.redis/",
            "microsoft.aspnetcore.authentication.jwtbearer/",
            "mediatr/",
            "automapper/",
            "nlog/",
            "restsharp/",
            "refit/"
        };

        foreach (var package in optInPackages)
        {
            Assert.DoesNotContain(assets, content => content.Contains(package, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AssertPackageBoundary(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entries = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();

        Assert.Contains("README.md", entries);
        Assert.Contains("webapi/.template.config/template.json", entries);
        Assert.Contains("webapi/Directory.Build.props", entries);
        Assert.Contains("webapi/Directory.Packages.props", entries);
        Assert.Contains("webapi/.gitignore", entries);
        Assert.Contains("webapi/README.md", entries);
        Assert.Contains("webapi/global.json", entries);
        Assert.Contains("webapi/src/YOU.Api/YOU.Api.csproj", entries);
        Assert.Contains("module/.template.config/template.json", entries);
        Assert.Contains("module/ModuleName/ModuleName.csproj", entries);
        Assert.Contains("module/ModuleName.Tests/ModuleName.Tests.csproj", entries);
        Assert.Contains("tool/.template.config/template.json", entries);
        Assert.Contains("tool/content/.gitignore", entries);
        Assert.Contains("tool/content/src/YOU.Tool/YOU.Tool.csproj", entries);
        Assert.DoesNotContain(entries, entry => entry.StartsWith("templates/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, entry => entry.StartsWith("content/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, entry => ContainsPathPart(entry, "packaging"));
        Assert.DoesNotContain(entries, entry => ContainsPathPart(entry, "docs"));
        Assert.DoesNotContain(entries, entry => ContainsPathPart(entry, "artifacts"));
        Assert.DoesNotContain(entries, entry => ContainsPathPart(entry, "bin"));
        Assert.DoesNotContain(entries, entry => ContainsPathPart(entry, "obj"));
        Assert.DoesNotContain(entries, entry => entry.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, entry => entry.StartsWith("YOU.API/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, entry => entry.StartsWith("YOU.Template", StringComparison.OrdinalIgnoreCase));

        var nuspec = Assert.Single(archive.Entries, entry =>
            entry.FullName.Equals("Guihua.Templates.nuspec", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(nuspec.Open());
        var manifest = reader.ReadToEnd();
        Assert.Contains("<readme>README.md</readme>", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("<dependencies>", manifest, StringComparison.Ordinal);
    }

    private static void AssertGeneratedBoundary(string output, string sampleName)
    {
        var files = Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(output, path).Replace('\\', '/'))
            .ToArray();

        Assert.Contains($"{sampleName}.sln", files);
        Assert.Contains(".gitignore", files);
        Assert.Contains("README.md", files);
        Assert.Contains(files, file => file.EndsWith($"/{sampleName}.Api.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(files, file => ContainsPathPart(file, "packaging"));
        Assert.DoesNotContain(files, file => ContainsPathPart(file, "modules"));
        Assert.DoesNotContain(files, file => ContainsPathPart(file, "docs"));
        Assert.DoesNotContain(files, file => ContainsPathPart(file, "artifacts"));
        Assert.DoesNotContain(files, file => ContainsPathPart(file, "package"));
        Assert.DoesNotContain(files, file => ContainsPathPart(file, "_rels"));
        Assert.DoesNotContain(files, file => file.Contains("Content_Types", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, file => file.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, file => file.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, file => file.Contains("YOU.Template", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, file => file.Contains("YOU.API", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, file => ContainsPathPart(file, "bin"));
        Assert.DoesNotContain(files, file => ContainsPathPart(file, "obj"));

        var generatedSource = files
            .Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(file => File.ReadAllText(Path.Combine(output, file.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
        foreach (var optInMarker in new[]
                 {
                     "AddFreeSqlPersistence",
                     "AddGuihuaHostedService",
                     "AddHttpIntegration",
                     "FreeSqlPersistence:Provider",
                     "GuihuaHostedService:IntervalMilliseconds",
                     "IntegrationHttp:BaseAddress"
                 })
        {
            Assert.DoesNotContain(generatedSource, content => content.Contains(optInMarker, StringComparison.Ordinal));
        }
    }

    private static bool ContainsPathPart(string path, string part) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals(part, StringComparison.OrdinalIgnoreCase));

    private static string FindRepositoryRoot()
    {
        var candidate = new DirectoryInfo(AppContext.BaseDirectory);
        while (candidate is not null)
        {
            if (File.Exists(Path.Combine(candidate.FullName, "Guihua.Template.sln"))
                && File.Exists(Path.Combine(candidate.FullName, "packaging", "Guihua.Templates.csproj")))
            {
                return candidate.FullName;
            }

            candidate = candidate.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the template repository root.");
    }

    private static CommandResult Run(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var variable in new[]
                 {
                     "MSBuildSDKsPath",
                     "MSBuildExtensionsPath",
                     "MSBuildExtensionsPath32",
                     "MSBuildExtensionsPath64",
                     "MSBuildToolsPath",
                     "MSBuildBinPath",
                     "DOTNET_HOST_PATH"
                 })
        {
            startInfo.Environment.Remove(variable);
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                standardOutput.AppendLine(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                standardError.AppendLine(eventArgs.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            process.Kill(entireProcessTree: true);
            process.CancelOutputRead();
            process.CancelErrorRead();
            throw new TimeoutException($"dotnet {string.Join(' ', arguments)} did not exit within five minutes.\nSTDOUT:\n{standardOutput}\nSTDERR:\n{standardError}");
        }

        process.CancelOutputRead();
        process.CancelErrorRead();
        return new CommandResult(arguments, process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    private sealed record CommandResult(string[] Arguments, int ExitCode, string StandardOutput, string StandardError)
    {
        public override string ToString() =>
            $"dotnet {string.Join(' ', Arguments)}\nExitCode: {ExitCode}\nSTDOUT:\n{StandardOutput}\nSTDERR:\n{StandardError}";
    }
}
