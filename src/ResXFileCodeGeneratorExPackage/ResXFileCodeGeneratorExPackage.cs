using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;

namespace Company.VSPackage2
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.

    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("ResXFileCodeGeneratorEx Package", "Installs the ResXFileCodeGeneratorEx custom tool", "1.0", LanguageIndependentName = "ResXFileCodeGeneratorEx Package")]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideCodeGenerator(typeof(DMKSoftware.CodeGenerators.ResXFileCodeGeneratorEx), "ResXFileCodeGeneratorEx", "", true)]
    [ProvideCodeGeneratorExtension("ResXFileCodeGeneratorEx", ".resx", ProjectSystem = ProvideCodeGeneratorAttribute.CSharpProjectGuid)]
    [ProvideCodeGeneratorExtension("ResXFileCodeGeneratorEx", ".resx", ProjectSystem = ProvideCodeGeneratorAttribute.VisualBasicProjectGuid)]
    [Guid("7077132D-1E2D-4E27-A23A-59EC7E16AA27")]
    public sealed class ResXFileCodeGeneratorExPackage : Package
    {
        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public ResXFileCodeGeneratorExPackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Creating ResXFileCodeGeneratorEx Package"));
        }

        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Initializing ResXFileCodeGeneratorEx Package"));
            base.Initialize();
        }
        #endregion
    }
}
