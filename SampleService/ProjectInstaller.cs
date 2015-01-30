using System.ComponentModel;
using System.Configuration.Install;

namespace BaseService
{
    // ReSharper disable once ClassNeverInstantiated.Global
    /// <summary>
    ///     Installer for the service
    /// </summary>
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}