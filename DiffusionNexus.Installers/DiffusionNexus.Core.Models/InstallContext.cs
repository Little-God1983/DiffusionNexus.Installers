using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiffusionNexus.Core.Models
{
    // Common installation context
    public class InstallContext
    {
        public string InstallPath { get; set; }
        public string PythonPath { get; set; }
        public bool CreateVirtualEnvironment { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; }
        public bool UseCuda { get; set; }
        public string CudaVersion { get; set; }

        public InstallContext()
        {
            EnvironmentVariables = new Dictionary<string, string>();
        }
    }
}
