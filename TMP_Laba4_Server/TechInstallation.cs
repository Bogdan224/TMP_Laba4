using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessController_Server
{
    public class TechInstallation
    {
        public enum Status
        {
            Success, Crash, Repair
        }

        public Status InstallationStatus { get; set; }

        public void Working()
        {
            Random random = new Random();

            if (InstallationStatus == Status.Success)
                InstallationStatus = random.NextDouble() <= 0.8 ? Status.Success : Status.Crash;

            else if (InstallationStatus == Status.Repair)
                InstallationStatus = random.NextDouble() <= 0.5 ? Status.Success : Status.Repair;
        }
    }
}
