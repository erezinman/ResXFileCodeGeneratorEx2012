using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Designer.Interfaces;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace DMKSoftware.CodeGenerators
{
    public abstract class BaseCodeGeneratorWithSite : BaseCodeGenerator, IObjectWithSite
    {
        private static readonly Guid CodeDomInterfaceGuid;
        private static readonly Guid CodeDomServiceGuid;
        private CodeDomProvider _codeDomProvider;
        private ServiceProvider _serviceProvider;
        private object _site;

        static BaseCodeGeneratorWithSite()
        {
            CodeDomInterfaceGuid = new Guid("{73E59688-C7C4-4a85-AF64-A538754784C5}");
            CodeDomServiceGuid = CodeDomInterfaceGuid;
        }

        protected virtual CodeDomProvider CodeProvider
        {
            get
            {
                if (null == _codeDomProvider)
                {
                    var vsMDCodeDomProvider = (IVSMDCodeDomProvider) GetService(CodeDomServiceGuid);
                    if (null != vsMDCodeDomProvider)
                        _codeDomProvider = (CodeDomProvider) vsMDCodeDomProvider.CodeDomProvider;
                }

                return _codeDomProvider;
            }
            set
            {
                if (null == value)
                    throw new ArgumentNullException();

                _codeDomProvider = value;
            }
        }

        private ServiceProvider SiteServiceProvider
        {
            get
            {
                if (null == _serviceProvider)
                {
                    var serviceProvider = _site as IServiceProvider;
                    _serviceProvider = new ServiceProvider(serviceProvider);
                }

                return _serviceProvider;
            }
        }

        public virtual void GetSite(ref Guid riid, out IntPtr ppvSite)
        {
            if (null == _site)
                throw new Win32Exception(-2147467259);

            var siteIUnknown = Marshal.GetIUnknownForObject(_site);
            try
            {
                Marshal.QueryInterface(siteIUnknown, ref riid, out ppvSite);
                if (IntPtr.Zero == ppvSite)
                    throw new Win32Exception(-2147467262);
            }
            finally
            {
                if (IntPtr.Zero != siteIUnknown)
                {
                    Marshal.Release(siteIUnknown);
                    siteIUnknown = IntPtr.Zero;
                }
            }
        }

        public virtual void SetSite(object pUnkSite)
        {
            _site = pUnkSite;
            _codeDomProvider = null;
            _serviceProvider = null;
        }

        public override int DefaultExtension(out string ext)
        {
            var defaultExtension = CodeProvider.FileExtension;
            if ((!string.IsNullOrEmpty(defaultExtension)) && (defaultExtension[0] != '.'))
                defaultExtension = "." + defaultExtension;

            ext = defaultExtension;

            return 0;
        }

        protected virtual ICodeGenerator GetCodeWriter()
        {
            var codeComProvider = CodeProvider;
            if (null != codeComProvider)
                return codeComProvider.CreateGenerator();

            return null;
        }

        protected object GetService(Guid serviceGuid)
        {
            return SiteServiceProvider.GetService(serviceGuid);
        }

        protected object GetService(Type serviceType)
        {
            return SiteServiceProvider.GetService(serviceType);
        }
    }
}