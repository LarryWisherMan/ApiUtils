using System;
using System.Management.Automation;
using System.Runtime.InteropServices;

namespace ApiUtils.Commands
{
    /// <summary>
    /// A simple test cmdlet to verify binary module loading is working
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "ApiUtils")]
    [OutputType(typeof(PSObject))]
    public class TestApiUtilsCommand : PSCmdlet
    {
        /// <summary>
        /// Gets or sets whether to show detailed information
        /// </summary>
        [Parameter(Mandatory = false)]
        [Alias("Detail")]
        public SwitchParameter Detailed { get; set; }

        /// <summary>
        /// Processes the record
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                var result = new PSObject();

                // Get PowerShell version table from the current session
                var psVersionTable = SessionState.PSVariable.GetValue("PSVersionTable") as System.Collections.Hashtable;

                // Basic info
                result.Properties.Add(new PSNoteProperty("ModuleName", "ApiUtils"));
                result.Properties.Add(new PSNoteProperty("ModuleType", "Binary"));
                result.Properties.Add(new PSNoteProperty("Status", "Working"));
                result.Properties.Add(new PSNoteProperty("PowerShellVersion", psVersionTable?["PSVersion"]?.ToString() ?? "Unknown"));
                result.Properties.Add(new PSNoteProperty("PowerShellEdition", psVersionTable?["PSEdition"]?.ToString() ?? "Unknown"));

                // .NET Runtime information
                result.Properties.Add(new PSNoteProperty("DotNetVersion", GetDotNetVersion()));
                result.Properties.Add(new PSNoteProperty("RuntimeArchitecture", RuntimeInformation.ProcessArchitecture.ToString()));
                result.Properties.Add(new PSNoteProperty("OperatingSystem", RuntimeInformation.OSDescription));

                if (Detailed.IsPresent)
                {
                    // Add detailed information
                    result.Properties.Add(new PSNoteProperty("AssemblyLocation", GetType().Assembly.Location));
                    result.Properties.Add(new PSNoteProperty("AssemblyVersion", GetType().Assembly.GetName().Version.ToString()));
                    result.Properties.Add(new PSNoteProperty("CLRVersion", psVersionTable?["CLRVersion"]?.ToString() ?? "Unknown"));
                    result.Properties.Add(new PSNoteProperty("BuildVersion", psVersionTable?["BuildVersion"]?.ToString() ?? "Unknown"));
                    result.Properties.Add(new PSNoteProperty("GitCommitId", psVersionTable?["GitCommitId"]?.ToString() ?? "Unknown"));
                    result.Properties.Add(new PSNoteProperty("IsWindows", RuntimeInformation.IsOSPlatform(OSPlatform.Windows)));
                    result.Properties.Add(new PSNoteProperty("IsLinux", RuntimeInformation.IsOSPlatform(OSPlatform.Linux)));
                    result.Properties.Add(new PSNoteProperty("IsMacOS", RuntimeInformation.IsOSPlatform(OSPlatform.OSX)));

                    // Show which framework DLL was loaded
                    var assemblyPath = GetType().Assembly.Location;
                    if (assemblyPath.Contains("net6.0"))
                    {
                        result.Properties.Add(new PSNoteProperty("LoadedFramework", ".NET 6.0"));
                    }
                    else if (assemblyPath.Contains("net48"))
                    {
                        result.Properties.Add(new PSNoteProperty("LoadedFramework", ".NET Framework 4.8"));
                    }
                    else
                    {
                        result.Properties.Add(new PSNoteProperty("LoadedFramework", "Unknown"));
                    }
                }

                WriteVerbose("ApiUtils binary module is functioning correctly");
                WriteObject(result);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "TestApiUtilsFailed", ErrorCategory.InvalidOperation, null));
            }
        }

        /// <summary>
        /// Gets the .NET version information
        /// </summary>
        /// <returns>String describing the .NET version</returns>
        private string GetDotNetVersion()
        {
            try
            {
                // Get .NET version
                var frameworkDescription = RuntimeInformation.FrameworkDescription;
                return frameworkDescription;
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
