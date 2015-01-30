using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.ServiceProcess;
using System.Timers;

namespace BaseService
{
    public partial class Service : ServiceBase
    {
        private readonly EventLog _eventLog;
        private readonly Dictionary<object, Monitor> _monitors;

        // ReSharper disable once MemberCanBePrivate.Global
        public bool Started { get; protected set; }

        // ReSharper disable once MemberCanBeProtected.Global
        /// <summary>
        ///     Creates a new service
        /// </summary>
        /// <exception cref="SecurityException"><paramref name="source" /> was not found, but some or all of the event logs could not be searched.</exception>
        public Service(string log, string source)
        {
            _monitors = new Dictionary<object, Monitor>();
            _eventLog = new EventLog {Log = log, Source = source};

            if (!EventLog.SourceExists(_eventLog.Source))
                EventLog.CreateEventSource(_eventLog.Source, _eventLog.Log);

            InitializeComponent();
        }

        public class Monitor : IDisposable
        {
            /// <summary>
            ///     Delegate for monitor event
            /// </summary>
            /// <param name="sender">Service that notified of the event</param>
            /// <param name="e">Arguments from monitor event</param>
            public delegate void MonitorDelegate(Monitor sender, MonitorEventArgs e);

            private readonly Timer _timer;

            /// <summary>
            ///     Constructs a new monitor
            /// </summary>
            /// <param name="parent">Parent service using monitor</param>
            /// <param name="monitorTime">Time in milliseconds between each monitor event</param>
            protected internal Monitor(Service parent, double monitorTime)
            {
                Parent = parent;
                _timer = new Timer(monitorTime);
                _timer.Elapsed += _timerElapsed;
            }

            // ReSharper disable once MemberCanBePrivate.Global
            /// <summary>
            ///     Parent service
            /// </summary>
            public Service Parent { get; private set; }

            /// <summary>
            /// Whether or not the monitor is running
            /// </summary>
            /// <exception cref="ObjectDisposedException">This property cannot be set because the timer has been disposed.</exception>
            /// <exception cref="ArgumentException">The <see cref="P:System.Timers.Timer.Interval" /> property was set to a value greater than <see cref="F:System.Int32.MaxValue" /> before the timer was enabled. </exception>
            public bool Running
            {
                get { return _timer.Enabled; }
                // ReSharper disable once MemberCanBePrivate.Global
                protected set { _timer.Enabled = value; }
            }

            /// <summary>
            ///     Disposes monitor
            /// </summary>
            public void Dispose()
            {
                Stop();
                _timer.Dispose();
            }

            /// <summary>
            ///     Called every monitor event
            /// </summary>
            public event MonitorDelegate MonitorEvent;

            /// <summary>
            ///     Stops monitoring
            /// </summary>
            public void Stop(bool fromService = false)
            {
                if(_timer.Enabled)
                    _timer.Stop();
                if(!fromService)
                    Running = false;
            }

            /// <summary>
            ///     Starts monitoring
            /// </summary>
            public void Start(bool fromService = false)
            {
                if(Parent.Started && !_timer.Enabled)
                    _timer.Start();
                if(!fromService)
                    Running = true;
            }

            private void _timerElapsed(object sender, ElapsedEventArgs e)
            {
                if (MonitorEvent != null)
                    MonitorEvent(this, new MonitorEventArgs(e));
            }

            /// <summary>
            ///     EventArgs for monitor events
            /// </summary>
            public class MonitorEventArgs : ServiceEventArgs
            {
                /// <summary>
                ///     Constructs a new MonitorEventArgs object
                /// </summary>
                /// <param name="e">Gets the time the monitor event was raised</param>
                public MonitorEventArgs(ElapsedEventArgs e)
                {
                    SignalTime = e.SignalTime;
                }

                // ReSharper disable once MemberCanBePrivate.Global
                // ReSharper disable once UnusedAutoPropertyAccessor.Global
                /// <summary>
                ///     Gets the time the monitor event was raised
                /// </summary>
                public DateTime SignalTime { get; protected set; }
            }
        }

        #region Windows API

        private enum ServiceState
        {
            ServiceStopped = 0x00000001,
            ServiceStartPending = 0x00000002,
            ServiceStopPending = 0x00000003,
            ServiceRunning = 0x00000004,
            ServiceContinuePending = 0x00000005,
            ServicePausePending = 0x00000006,
            ServicePaused = 0x00000007
        }

        [StructLayout(LayoutKind.Sequential)]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        private struct ServiceStatus
        {
            public long dwServiceType;
            public ServiceState dwCurrentState;
            public long dwControlsAccepted;
            public long dwWin32ExitCode;
            public long dwServiceSpecificExitCode;
            public long dwCheckPoint;
            public long dwWaitHint;
        };

        // ReSharper disable once StringLiteralTypo
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);

        #endregion

        #region EventArgs

        /// <summary>
        ///     EventArgs for services
        /// </summary>
        public class ServiceEventArgs : EventArgs
        {
        }

        /// <summary>
        ///     EventArgs for service start
        /// </summary>
        public class ServiceStartEventArgs : ServiceEventArgs
        {
            // ReSharper disable once CommentTypo
            // ReSharper disable once IdentifierTypo
            /// <summary>
            ///     Constructs a new ServiceStartEventArgs object
            /// </summary>
            /// <param name="args">Arguments passed to service</param>
            public ServiceStartEventArgs(string[] args)
            {
                Args = args;
            }

            // ReSharper disable once MemberCanBePrivate.Global
            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            /// <summary>
            ///     Args passed to service
            /// </summary>
            public string[] Args { get; protected set; }
        }

        /// <summary>
        ///     EventArgs for custom commands
        /// </summary>
        public class CustomCommandEventArgs : ServiceEventArgs
        {
            /// <summary>
            ///     Constructs a new CustomCommandEventArgs object
            /// </summary>
            /// <param name="command">Command requested</param>
            public CustomCommandEventArgs(int command)
            {
                Command = command;
            }

            // ReSharper disable once MemberCanBePrivate.Global
            /// <summary>
            ///     Command requested
            /// </summary>
            public int Command { get; protected set; }
        }

        /// <summary>
        ///     EventArgs for shutdown events
        /// </summary>
        public class ShutdownEventArgs : ServiceEventArgs
        {
            /// <summary>
            ///     Constructs a new ShutdownEventArgs object
            /// </summary>
            /// <param name="runBase">Whether or not to run the base function</param>
            public ShutdownEventArgs(bool runBase = true)
            {
                RunBase = runBase;
            }

            // ReSharper disable once MemberCanBePrivate.Global
            /// <summary>
            ///     Whether or not to run the base function
            /// </summary>
            public bool RunBase { get; set; }
        }

        /// <summary>
        ///     EventArgs for shutdown event
        /// </summary>
        public class PowerEventArgs : ServiceEventArgs
        {
            /// <summary>
            ///     Creates a new ShutdownEventArgs object
            /// </summary>
            /// <param name="powerStatus">A notification from the system about it's power status</param>
            /// <param name="rejectQuery">Determines whether or not to reject the query</param>
            /// <param name="runBase">Whether or not to run the base function</param>
            public PowerEventArgs(PowerBroadcastStatus powerStatus, bool rejectQuery = false, bool runBase = true)
            {
                PowerStatus = powerStatus;
                RejectQuery = rejectQuery;
                RunBase = runBase;
            }

            // ReSharper disable once MemberCanBePrivate.Global
            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            /// <summary>
            ///     A notification from the system about it's power status
            /// </summary>
            public PowerBroadcastStatus PowerStatus { get; protected set; }

            // ReSharper disable once MemberCanBePrivate.Global
            /// <summary>
            ///     Determines whether or not to reject the Shutdown query
            /// </summary>
            public bool RejectQuery { get; set; }

            // ReSharper disable once MemberCanBePrivate.Global
            /// <summary>
            ///     Whether or not to run the base function
            /// </summary>
            public bool RunBase { get; set; }
        }

        /// <summary>
        ///     EventArgs for session change events
        /// </summary>
        public class SessionChangeEventArgs : ServiceEventArgs
        {
            /// <summary>
            ///     Creates a new SessionChangeEventArgs
            /// </summary>
            /// <param name="description">Description of the session change</param>
            public SessionChangeEventArgs(SessionChangeDescription description)
            {
                Description = description;
            }

            // ReSharper disable once MemberCanBePrivate.Global
            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            /// <summary>
            ///     Description of the session change
            /// </summary>
            public SessionChangeDescription Description { get; protected set; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            /// <summary>
            ///     Whether or not to run the base function
            /// </summary>
            public bool RunBase { get; set; }
        }

        #endregion

        #region Delegates

        /// <summary>
        ///     Delegate for service start event
        /// </summary>
        /// <param name="sender">Service that notified of the event</param>
        /// <param name="e">Arguments from start event</param>
        public delegate void StartDelegate(ServiceBase sender, ServiceStartEventArgs e);

        /// <summary>
        ///     Delegate for service pause event
        /// </summary>
        /// <param name="sender">Service that notified of the event</param>
        /// <param name="e">Arguments from pause event</param>
        public delegate void PauseDelegate(ServiceBase sender, ServiceEventArgs e);

        /// <summary>
        ///     Delegate for the service continue event
        /// </summary>
        /// <param name="sender">Service that notified of the event</param>
        /// <param name="e">Arguments from the continue event</param>
        public delegate void ContinueDelegate(ServiceBase sender, ServiceEventArgs e);

        /// <summary>
        ///     Delegate for service stop event
        /// </summary>
        /// <param name="sender">Service that notified of the event</param>
        /// <param name="e">Arguments from the stop event</param>
        public delegate void StopDelegate(ServiceBase sender, ServiceEventArgs e);

        /// <summary>
        ///     Delegate for service custom commands
        /// </summary>
        /// <param name="sender">Service that notified of the event</param>
        /// <param name="e">Arguments from the custom command event</param>
        public delegate void CustomCommandDelegate(ServiceBase sender, CustomCommandEventArgs e);

        /// <summary>
        ///     Delegate for shutdown event
        /// </summary>
        /// <param name="sender">Service that notified of the event</param>
        /// <param name="e">Arguments from start event</param>
        public delegate void ShutdownDelegate(ServiceBase sender, ShutdownEventArgs e);

        /// <summary>
        ///     Delegate for power event
        /// </summary>
        /// <param name="sender">Service that notified of the event</param>
        /// <param name="e">Arguments from power event</param>
        public delegate void PowerEventDelegate(ServiceBase sender, PowerEventArgs e);

        /// <summary>
        ///     Delegate for session change event
        /// </summary>
        /// <param name="sender">Service that notified of the event</param>
        /// <param name="e">Arguments from session change event</param>
        public delegate void SessionChangeDelegate(ServiceBase sender, SessionChangeEventArgs e);

        #endregion

        #region Events

        /// <summary>
        ///     Called every start event
        /// </summary>
        public event StartDelegate StartService;

        /// <summary>
        ///     Called every pause event
        /// </summary>
        public event PauseDelegate PauseService;

        /// <summary>
        ///     Called every continue event
        /// </summary>
        public event ContinueDelegate ContinueService;

        /// <summary>
        ///     Called every stop event
        /// </summary>
        public event StopDelegate StopService;

        /// <summary>
        ///     Called every custom command event
        /// </summary>
        public event CustomCommandDelegate CustomCommand;

        /// <summary>
        ///     Called every shutdown event (runs base by default)
        /// </summary>
        public event ShutdownDelegate Shutdown;

        /// <summary>
        ///     Called every power event (runs base and accepts query by default)
        /// </summary>
        public event PowerEventDelegate PowerEvent;

        /// <summary>
        ///     Called every session change event (runs base by default)
        /// </summary>
        public event SessionChangeDelegate SessionChange;

        #endregion

        #region WriteLog

        // ReSharper disable once MemberCanBeProtected.Global
        /// <summary>
        ///     Writes an entry with the given message text, application-defined event identifier, and application-defined category
        ///     to the event log, and appends binary data to the message.
        /// </summary>
        /// <param name="log">The string to write to the event log.</param>
        /// <exception cref="Win32Exception">The operating system reported an error when writing the event entry to the event log. A Windows error code is not available.</exception>
        /// <exception cref="InvalidOperationException">The registry key for the event log could not be opened.</exception>
        /// <exception cref="ArgumentException">The <see cref="P:System.Diagnostics.EventLog.Source" /> property of the <see cref="T:System.Diagnostics.EventLog" /> has not been set.-or- The method attempted to register a new event source, but the computer name in <see cref="P:System.Diagnostics.EventLog.MachineName" />  is not valid.- or -The source is already registered for a different event log.- or -The message string is longer than 32766 bytes.- or -The source name results in a registry key path longer than 254 characters.</exception>
        public void WriteLog(string log)
        {
            _eventLog.WriteEntry(log);
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        ///     Writes an entry with the given message text, application-defined event identifier, and application-defined category
        ///     to the event log, and appends binary data to the message.
        /// </summary>
        /// <param name="log">The string to write to the event log.</param>
        /// <param name="type">One of the System.Diagnostics.EventLogEntryType values.</param>
        /// <exception cref="ArgumentException">The <see cref="P:System.Diagnostics.EventLog.Source" /> property of the <see cref="T:System.Diagnostics.EventLog" /> has not been set.-or- The method attempted to register a new event source, but the computer name in <see cref="P:System.Diagnostics.EventLog.MachineName" />  is not valid.- or -The source is already registered for a different event log.- or -The message string is longer than 32766 bytes.- or -The source name results in a registry key path longer than 254 characters.</exception>
        /// <exception cref="InvalidEnumArgumentException"><paramref name="type" /> is not a valid <see cref="T:System.Diagnostics.EventLogEntryType" />.</exception>
        /// <exception cref="InvalidOperationException">The registry key for the event log could not be opened.</exception>
        /// <exception cref="Win32Exception">The operating system reported an error when writing the event entry to the event log. A Windows error code is not available.</exception>
        public void WriteLog(string log, EventLogEntryType type)
        {
            _eventLog.WriteEntry(log, type);
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        ///     Writes an entry with the given message text, application-defined event identifier, and application-defined category
        ///     to the event log, and appends binary data to the message.
        /// </summary>
        /// <param name="log">The string to write to the event log.</param>
        /// <param name="type">One of the System.Diagnostics.EventLogEntryType values.</param>
        /// <param name="eventId">The application-specific identifier for the event.</param>
        /// <exception cref="InvalidOperationException">The registry key for the event log could not be opened.</exception>
        /// <exception cref="InvalidEnumArgumentException"><paramref name="type" /> is not a valid <see cref="T:System.Diagnostics.EventLogEntryType" />.</exception>
        /// <exception cref="ArgumentException">The <see cref="P:System.Diagnostics.EventLog.Source" /> property of the <see cref="T:System.Diagnostics.EventLog" /> has not been set.-or- The method attempted to register a new event source, but the computer name in <see cref="P:System.Diagnostics.EventLog.MachineName" /> is not valid.- or -The source is already registered for a different event log.- or -<paramref>
        ///         <name>eventID</name>
        ///     </paramref>
        ///     is less than zero or greater than <see cref="F:System.UInt16.MaxValue" />.- or -The message string is longer than 32766 bytes.- or -The source name results in a registry key path longer than 254 characters.</exception>
        /// <exception cref="Win32Exception">The operating system reported an error when writing the event entry to the event log. A Windows error code is not available.</exception>
        public void WriteLog(string log, EventLogEntryType type, int eventId)
        {
            _eventLog.WriteEntry(log, type, eventId);
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        ///     Writes an entry with the given message text, application-defined event identifier, and application-defined category
        ///     to the event log, and appends binary data to the message.
        /// </summary>
        /// <param name="log">The string to write to the event log.</param>
        /// <param name="type">One of the System.Diagnostics.EventLogEntryType values.</param>
        /// <param name="eventId">The application-specific identifier for the event.</param>
        /// <param name="category">The application-specific subcategory associated with the message.</param>
        /// <exception cref="ArgumentException">The <see cref="P:System.Diagnostics.EventLog.Source" /> property of the <see cref="T:System.Diagnostics.EventLog" /> has not been set.-or- The method attempted to register a new event source, but the computer name in <see cref="P:System.Diagnostics.EventLog.MachineName" /> is not valid.- or -The source is already registered for a different event log.- or -<paramref>
        ///         <name>eventID</name>
        ///     </paramref>
        ///     is less than zero or greater than <see cref="F:System.UInt16.MaxValue" />.- or -The message string is longer than 32766 bytes.- or -The source name results in a registry key path longer than 254 characters.</exception>
        /// <exception cref="InvalidOperationException">The registry key for the event log could not be opened.</exception>
        /// <exception cref="InvalidEnumArgumentException"><paramref name="type" /> is not a valid <see cref="T:System.Diagnostics.EventLogEntryType" />.</exception>
        /// <exception cref="Win32Exception">The operating system reported an error when writing the event entry to the event log. A Windows error code is not available.</exception>
        public void WriteLog(string log, EventLogEntryType type, int eventId, short category)
        {
            _eventLog.WriteEntry(log, type, eventId, category);
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        ///     Writes an entry with the given message text, application-defined event identifier, and application-defined category
        ///     to the event log, and appends binary data to the message.
        /// </summary>
        /// <param name="log">The string to write to the event log.</param>
        /// <param name="type">One of the System.Diagnostics.EventLogEntryType values.</param>
        /// <param name="eventId">The application-specific identifier for the event.</param>
        /// <param name="category">The application-specific subcategory associated with the message.</param>
        /// <param name="rawData">An array of bytes that holds the binary data associated with the entry.</param>
        /// <exception cref="ArgumentException">The <see cref="P:System.Diagnostics.EventLog.Source" /> property of the <see cref="T:System.Diagnostics.EventLog" /> has not been set.-or- The method attempted to register a new event source, but the computer name in <see cref="P:System.Diagnostics.EventLog.MachineName" /> is not valid.- or -The source is already registered for a different event log.- or -<paramref>
        ///         <name>eventID</name>
        ///     </paramref>
        ///     is less than zero or greater than <see cref="F:System.UInt16.MaxValue" />.- or -The message string is longer than 32766 bytes.- or -The source name results in a registry key path longer than 254 characters.</exception>
        /// <exception cref="InvalidOperationException">The registry key for the event log could not be opened.</exception>
        /// <exception cref="InvalidEnumArgumentException"><paramref name="type" /> is not a valid <see cref="T:System.Diagnostics.EventLogEntryType" />.</exception>
        /// <exception cref="Win32Exception">The operating system reported an error when writing the event entry to the event log. A Windows error code is not available.</exception>
        public void WriteLog(string log, EventLogEntryType type, int eventId, short category, byte[] rawData)
        {
            _eventLog.WriteEntry(log, type, eventId, category, rawData);
        }

        #endregion

        #region Monitor Functions

        // ReSharper disable once MemberCanBeProtected.Global
        // ReSharper disable once UnusedMethodReturnValue.Global
        /// <summary>
        ///     Adds a new monitor
        /// </summary>
        /// <param name="reference">Reference for the monitor</param>
        /// <param name="monitorTime">Time for each monitor event</param>
        /// <param name="callback">Function to call on monitor event</param>
        /// <returns></returns>
        public bool AddMonitor(object reference, double monitorTime, Monitor.MonitorDelegate callback)
        {
            if (_monitors.ContainsKey(reference))
                return false;
            var m = new Monitor(this, monitorTime);
            m.MonitorEvent += callback;
            return AddMonitor(reference, m);
        }

        // ReSharper disable once MemberCanBePrivate.Global
        /// <summary>
        ///     Adds a new monitor
        /// </summary>
        /// <param name="reference">Reference for monitor</param>
        /// <param name="m">Monitor to add</param>
        /// <returns>True if monitor was added</returns>
        protected bool AddMonitor(object reference, Monitor m)
        {
            if (_monitors.ContainsKey(reference))
                return false;
            _monitors.Add(reference, m);
            return true;
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        ///     Removes a monitor
        /// </summary>
        /// <param name="reference">Reference for monitor</param>
        /// <returns>True if monitor was removed</returns>
        /// <exception cref="ArgumentNullException"><paramref>
        ///         <name>key</name>
        ///     </paramref>
        ///     is null.</exception>
        /// <exception cref="KeyNotFoundException">The property is retrieved and <paramref>
        ///         <name>key</name>
        ///     </paramref>
        ///     does not exist in the collection.</exception>
        public bool RemoveMonitor(object reference)
        {
            if (!_monitors.ContainsKey(reference))
                return true;
            _monitors[reference].Dispose();
            return _monitors.Remove(reference);
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        // ReSharper disable once MemberCanBeProtected.Global
        /// <summary>
        ///     Starts a monitor
        /// </summary>
        /// <param name="reference">Reference for monitor</param>
        /// <returns>True if monitor is started</returns>
        /// <exception cref="ArgumentNullException"><paramref>
        ///         <name>key</name>
        ///     </paramref>
        ///     is null.</exception>
        /// <exception cref="KeyNotFoundException">The property is retrieved and <paramref>
        ///         <name>key</name>
        ///     </paramref>
        ///     does not exist in the collection.</exception>
        public bool StartMonitor(object reference)
        {
            if (!_monitors.ContainsKey(reference))
                return false;
            _monitors[reference].Start();
            return true;
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        ///     Stops a monitor
        /// </summary>
        /// <param name="reference">Reference for monitor</param>
        /// <returns>True if monitor was stopped</returns>
        /// <exception cref="ArgumentNullException"><paramref>
        ///         <name>key</name>
        ///     </paramref>
        ///     is null.</exception>
        /// <exception cref="KeyNotFoundException">The property is retrieved and <paramref>
        ///         <name>key</name>
        ///     </paramref>
        ///     does not exist in the collection.</exception>
        public bool StopMonitor(object reference)
        {
            if (!_monitors.ContainsKey(reference))
                return false;
            _monitors[reference].Stop();
            return true;
        }

        // ReSharper disable once UnusedMember.Global
        /// <summary>
        ///     Gets a monitor
        /// </summary>
        /// <param name="reference">Reference for monitor</param>
        /// <returns>Monitor from reference</returns>
        /// <exception cref="ArgumentNullException"><paramref>
        ///         <name>key</name>
        ///     </paramref>
        ///     is null.</exception>
        /// <exception cref="KeyNotFoundException">The property is retrieved and <paramref>
        ///         <name>key</name>
        ///     </paramref>
        ///     does not exist in the collection.</exception>
        public Monitor GetMonitor(object reference)
        {
            return !_monitors.ContainsKey(reference) ? null : _monitors[reference];
        }

        #endregion

        #region Overrides

        // ReSharper disable once UnusedMember.Global
        /// <exception cref="ArgumentNullException">The value of 'list' cannot be null. </exception>
        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        /// <exception cref="NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only. </exception>
        public new void Dispose()
        {
            _monitors.DisposeAll();
            base.Dispose();
        }

        // ReSharper disable once IdentifierTypo
        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        protected override void OnStart(string[] args)
        {
            var status = new ServiceStatus();
            var e = new ServiceStartEventArgs(args);

            status.dwCurrentState = ServiceState.ServiceStartPending;
            status.dwWaitHint = 100000;
            SetServiceStatus(ServiceHandle, ref status);

            if (StartService != null)
                StartService(this, e);

            Started = true;

            _monitors.Where(x=>x.Value.Running).Each(x => x.Value.Start(true));

            status.dwCurrentState = ServiceState.ServiceRunning;
            SetServiceStatus(ServiceHandle, ref status);
        }

        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        protected override void OnPause()
        {
            var status = new ServiceStatus();
            var e = new ServiceEventArgs();

            status.dwCurrentState = ServiceState.ServicePausePending;
            status.dwWaitHint = 100000;
            SetServiceStatus(ServiceHandle, ref status);

            if (PauseService != null)
                PauseService(this, e);

            Started = false;

            _monitors.Each(x => x.Value.Stop(true));

            status.dwCurrentState = ServiceState.ServicePaused;
            SetServiceStatus(ServiceHandle, ref status);
        }

        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        protected override void OnContinue()
        {
            var status = new ServiceStatus();
            var e = new ServiceEventArgs();

            status.dwCurrentState = ServiceState.ServiceContinuePending;
            status.dwWaitHint = 100000;
            SetServiceStatus(ServiceHandle, ref status);

            if (ContinueService != null)
                ContinueService(this, e);

            Started = true;

            _monitors.Where(x => x.Value.Running).Each(x => x.Value.Start(true));

            status.dwCurrentState = ServiceState.ServiceRunning;
            SetServiceStatus(ServiceHandle, ref status);
        }

        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        protected override void OnStop()
        {
            var status = new ServiceStatus();
            var e = new ServiceEventArgs();

            status.dwCurrentState = ServiceState.ServiceStopPending;
            status.dwWaitHint = 100000;
            SetServiceStatus(ServiceHandle, ref status);

            if (StopService != null)
                StopService(this, e);

            Started = false;

            _monitors.Each(x => x.Value.Stop(true));

            status.dwCurrentState = ServiceState.ServiceStopped;
            SetServiceStatus(ServiceHandle, ref status);
        }

        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        protected override void OnCustomCommand(int command)
        {
            var e = new CustomCommandEventArgs(command);

            if (CustomCommand != null)
                CustomCommand(this, e);
        }

        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        protected override void OnShutdown()
        {
            var e = new ShutdownEventArgs();

            if (Shutdown != null)
                Shutdown(this, e);

            if (e.RunBase)
                base.OnShutdown();
        }

        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            var e = new PowerEventArgs(powerStatus);

            if (PowerEvent != null)
                PowerEvent(this, e);

            if (e.RunBase)
                return base.OnPowerEvent(powerStatus) | e.RejectQuery;

            return e.RejectQuery;
        }

        /// <exception cref="Exception">A delegate callback throws an exception. </exception>
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            var e = new SessionChangeEventArgs(changeDescription);

            if (SessionChange != null)
                SessionChange(this, e);

            if (e.RunBase)
                base.OnSessionChange(changeDescription);
        }

        #endregion
    }
}