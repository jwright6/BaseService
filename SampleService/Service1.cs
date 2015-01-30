using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security;
using BaseService;

namespace SampleService
{
    public partial class Service1 : Service
    {
        /// <exception cref="SecurityException"><paramref>
        ///         <name>source</name>
        ///     </paramref>
        ///     was not found, but some or all of the event logs could not be searched.</exception>
        /// <exception cref="Win32Exception">The operating system reported an error when writing the event entry to the event log. A Windows error code is not available.</exception>
        /// <exception cref="InvalidOperationException">The registry key for the event log could not be opened.</exception>
        /// <exception cref="ArgumentException">The <see cref="P:System.Diagnostics.EventLog.Source" /> property of the <see cref="T:System.Diagnostics.EventLog" /> has not been set.-or- The method attempted to register a new event source, but the computer name in <see cref="P:System.Diagnostics.EventLog.MachineName" />  is not valid.- or -The source is already registered for a different event log.- or -The message string is longer than 32766 bytes.- or -The source name results in a registry key path longer than 254 characters.</exception>
        /// <exception cref="ArgumentNullException"><paramref>
        ///         <name>key</name>
        ///     </paramref>
        ///     is null.</exception>
        /// <exception cref="KeyNotFoundException">The property is retrieved and <paramref>
        ///         <name>key</name>
        ///     </paramref>
        ///     does not exist in the collection.</exception>
        public Service1() : base("SampleLog", "SampleSource")
        {
            ContinueService += (sender, e) => WriteLog("Service Continuing");
            CustomCommand += (sender, e) => WriteLog("Service Custom Command: " + e.Command);
            Disposed += (sender, e) => WriteLog("Service Disposed");
            PauseService += (sender, e) => WriteLog("Service Paused");
            PowerEvent += (sender, e) => WriteLog("Service Power Event");
            SessionChange += (sender, e) => WriteLog("Service Session Changing");
            Shutdown += (sender, e) => WriteLog("Service Shutdown Event");
            StartService += (sender, e) => WriteLog("Service Started");
            StopService += (sender, e) => WriteLog("Service Stopping");

            AddMonitor("TestMonitor", 5000, (sender, e) => WriteLog("Monitor Event"));
            StartMonitor("TestMonitor");
            InitializeComponent();
        }
    }
}