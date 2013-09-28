using System;
using System.CodeDom;
using System.Reflection;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Microsoft.Win32;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using DMKSoftware.CodeGenerators;
using DMKSoftware;

namespace DMKSoftware.CodeGenerators
{
    public abstract class BaseResXFileCodeGeneratorEx : BaseCodeGeneratorWithSite, IVsRefactorNotify
    {
		private const string CSharpObfuscationAttributeFinderRegex = @"[ \f\r\t\v]*\[global::System.Reflection.ObfuscationAttribute\(.*\)\][ \f\r\t\v]*\n";
		private readonly string CSharpCompilationDirectiveStart = "#if !SILVERLIGHT" + Environment.NewLine;
		private readonly string CSharpCompilationDirectiveEnd = "#endif" + Environment.NewLine;

		private const string VBClassAttributesFinderRegexTemplate = @"[ \f\r\t\v]*<Global\.[\s\S]+\)> +_\r\n[ \f\r\t\v]*Partial (?:Friend|Public) Class {0}\r\n";
		private const string VBObfuscationAttributeFinderRegex = @"[ \f\r\t\v]*Global\.System\.Reflection\.ObfuscationAttribute\(.*\)\, +_\r\n";
		private readonly string VBCompilationDirectiveStart = "#If Not SILVERLIGHT Then" + Environment.NewLine;
		private readonly string VBCompilationDirectiveElse = "#Else" + Environment.NewLine;
		private readonly string VBCompilationDirectiveEndIf = "#End If" + Environment.NewLine;

		private enum VsNetVersion
		{
			[Description("8.0")]
			V80,
			[Description("9.0")]
			V90
		}

        private enum LanguageSpecifier
        {
			[LanguageGuid("{FAE04EC1-301F-11D3-BF4B-00C04F79EFBC}")]
            CSharp,
			[LanguageGuid("{164B10B9-B200-11D0-8C61-00A0C91E29D5}")]
            VB
        }

        private const string DesignerExtension = ".Designer";

        private IVsRefactorNotify _refactorNotifyImplementer;
        private static readonly Guid _resourceEditorRefactorNotifyServiceGuid;

		/// <summary>
		/// The BaseResXFileCodeGeneratorEx class static constructor.
		/// </summary>
        static BaseResXFileCodeGeneratorEx()
        {
            _resourceEditorRefactorNotifyServiceGuid = new Guid("0407F754-C199-403e-B89B-1D8E1FF3DC79");
        }

		/// <summary>
		/// Gets the boolean flag indicating whether the internal class is generated.
		/// </summary>
		protected abstract bool GenerateInternalClass
		{
			get;
		}

        public override int DefaultExtension(out string ext)
        {
            string baseExtension;
            ext = string.Empty;
            int result = base.DefaultExtension(out baseExtension);
            if (!NativeMethods.Succeeded(result))
                return result;
            
            if (!string.IsNullOrEmpty(baseExtension))
                ext = DesignerExtension + baseExtension;
            
            return 0;
        }

        protected override byte[] GenerateCode(string inputFileName, string inputFileContent)
        {
            CodeCompileUnit codeCompileUnit;
            if (string.IsNullOrEmpty(inputFileContent) || IsLocalizedFile(inputFileName))
            {
                if (base.CodeGeneratorProgress != null)
                    NativeMethods.ThrowOnFailure(base.CodeGeneratorProgress.Progress(100, 100));

                return null;
            }

			using (MemoryStream codeStream = new MemoryStream())
			{
				using (StreamWriter streamWriter = new StreamWriter(codeStream, Encoding.UTF8))
				{
					IDictionary nodeDictionary = new Hashtable(StringComparer.OrdinalIgnoreCase);
					IDictionary nodePositionDictionary = new Hashtable(StringComparer.OrdinalIgnoreCase);
					using (IResourceReader resourceReader = ResXResourceReader.FromFileContents(inputFileContent))
					{
						ResXResourceReader resXResourceReader = resourceReader as ResXResourceReader;
						if (resXResourceReader != null)
						{
							resXResourceReader.UseResXDataNodes = true;
							string inputDirectoryName = Path.GetDirectoryName(inputFileName);
							resXResourceReader.BasePath = Path.GetFullPath(inputDirectoryName);
						}
						foreach (DictionaryEntry resourceEntry in resourceReader)
						{
							ResXDataNode resXNode = (ResXDataNode)resourceEntry.Value;
							nodeDictionary.Add(resourceEntry.Key, resXNode);
							nodePositionDictionary.Add(resourceEntry.Key, resXNode.GetNodePosition());
						}
					}

					string resourceNamespace = this.GetResourcesNamespace();
					string inputFileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputFileName);

					List<Tools.ResourceErrorData> unmatchableResources = new List<Tools.ResourceErrorData>();
					if (resourceNamespace != null)
						codeCompileUnit = Tools.StronglyTypedResourceBuilderEx.Create(this.GetType(),
							nodeDictionary, inputFileNameWithoutExtension, base.FileNameSpace, resourceNamespace,
							this.CodeProvider, GenerateInternalClass, unmatchableResources);
					else
						codeCompileUnit = Tools.StronglyTypedResourceBuilderEx.Create(this.GetType(),
							nodeDictionary, inputFileNameWithoutExtension, base.FileNameSpace, this.CodeProvider,
							GenerateInternalClass, unmatchableResources);

					if (base.CodeGeneratorProgress != null)
					{
						foreach (Tools.ResourceErrorData resourceErrorData in unmatchableResources)
						{
							Point nodePosition = (Point)nodePositionDictionary[resourceErrorData.ResourceKey];
							base.CodeGeneratorProgress.GeneratorError(1, 1, resourceErrorData.ErrorString,
								(uint)nodePosition.Y, (uint)nodePosition.X);
						}

						base.CodeGeneratorProgress.Progress(70, 100);
					}

					this.HandleCodeCompileUnit(codeCompileUnit);

					if (base.CodeGeneratorProgress != null)
						base.CodeGeneratorProgress.Progress(0x4b, 100);

					ICodeGenerator codeGenerator = this.CodeProvider.CreateGenerator();
					codeGenerator.GenerateCodeFromCompileUnit(codeCompileUnit, streamWriter, null);
					if (base.CodeGeneratorProgress != null)
						NativeMethods.ThrowOnFailure(base.CodeGeneratorProgress.Progress(100, 100));

					streamWriter.Flush();
					codeStream.Flush();

					return EnsureSilverlighCompatibility(codeStream.ToArray(), codeCompileUnit);
				}
			}
        }

		protected byte[] EnsureSilverlighCompatibility(byte[] generatedCodeBytes, CodeCompileUnit codeCompileUnit)
		{
			if (null == generatedCodeBytes)
				throw new ArgumentNullException("generatedCodeBytes");

			if (null == codeCompileUnit)
				throw new ArgumentNullException("codeCompileUnit");

			StringBuilder generatedCode = new StringBuilder(Encoding.UTF8.GetString(generatedCodeBytes));

			string compilerlanguage = CodeDomProvider.GetLanguageFromExtension(this.CodeProvider.FileExtension);
			switch (compilerlanguage)
			{
				case "c#":
				case "cs":
				case "csharp":
					Match csMatch = Regex.Match(generatedCode.ToString(), CSharpObfuscationAttributeFinderRegex);
					if ((null != csMatch) && csMatch.Success)
					{
						generatedCode.Insert(csMatch.Index, CSharpCompilationDirectiveStart);
						generatedCode.Insert(csMatch.Index + CSharpCompilationDirectiveStart.Length +
							csMatch.Length, CSharpCompilationDirectiveEnd);
					}
					break;
				case "vb":
				case "visualbasic":
					string className = codeCompileUnit.Namespaces[0].Types[0].Name;
					Match vbMatch = Regex.Match(generatedCode.ToString(), string.Format(VBClassAttributesFinderRegexTemplate, className));
					if ((null != vbMatch) && vbMatch.Success)
					{
						string fullClassAttributesAndDeclaration = vbMatch.Value;
						Match obfuscationAttributeMatch = Regex.Match(fullClassAttributesAndDeclaration, VBObfuscationAttributeFinderRegex);
						if ((null != obfuscationAttributeMatch) && obfuscationAttributeMatch.Success)
						{
							string silverlightClassAttributesAndDeclaration = fullClassAttributesAndDeclaration.Remove(obfuscationAttributeMatch.Index,
								obfuscationAttributeMatch.Length);
							generatedCode.Insert(vbMatch.Index, VBCompilationDirectiveStart);
							string elseDirectiveAndSilverlightAttributes = VBCompilationDirectiveElse + silverlightClassAttributesAndDeclaration +
								VBCompilationDirectiveEndIf;
							generatedCode.Insert(vbMatch.Index + VBCompilationDirectiveStart.Length + vbMatch.Length, elseDirectiveAndSilverlightAttributes);
						}
					}
					break;
				default:
					throw new ApplicationException(string.Format("The '{0}' language is not currently supported by the resource generator."));
			}

			return Encoding.UTF8.GetBytes(generatedCode.ToString());
		}

        protected string GetResourcesNamespace()
        {
            string resourcesNamespace = null;

            try
            {
                IntPtr siteInterfacePointer;
                Guid vsBrowseObjectGuid = typeof(IVsBrowseObject).GUID;
                GetSite(ref vsBrowseObjectGuid, out siteInterfacePointer);
                if (IntPtr.Zero != siteInterfacePointer)
                {
                    IVsHierarchy vsHierarchy;
                    uint pItemId;
                    object propertyValue;
                    IVsBrowseObject vsBrowseObject = Marshal.GetObjectForIUnknown(siteInterfacePointer) as IVsBrowseObject;
                    Marshal.Release(siteInterfacePointer);
                    if (null == vsBrowseObject)
                        return resourcesNamespace;

                    vsBrowseObject.GetProjectItem(out vsHierarchy, out pItemId);
                    if (null == vsHierarchy)
                        return resourcesNamespace;

                    vsHierarchy.GetProperty(pItemId, -2049, out propertyValue);
                    string propertyText = propertyValue as string;
                    if (null == propertyText)
                        return resourcesNamespace;

                    resourcesNamespace = propertyText;
                }
            }
            catch (Exception ex)
            {
                if (Tools.ProjectUtilities.IsCriticalException(ex))
                {
                    throw;
                }
            }

            return resourcesNamespace;
        }

        protected virtual void HandleCodeCompileUnit(CodeCompileUnit ccu)
        {
        }

        private static bool IsLocalizedFile(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName) && fileName.EndsWith(".resx", true, CultureInfo.InvariantCulture))
            {
                string fileNameWithoutExtension = fileName.Substring(0, fileName.Length - 5);
                if (!string.IsNullOrEmpty(fileNameWithoutExtension))
                {
                    int lastDotIndex = fileNameWithoutExtension.LastIndexOf('.');
                    if (lastDotIndex > 0)
                    {
                        string cultureName = fileNameWithoutExtension.Substring(lastDotIndex + 1);
                        if (!string.IsNullOrEmpty(cultureName))
                        {
                            try
                            {
                                if (new CultureInfo(cultureName) != null)
                                {
                                    return true;
                                }
                            }
                            catch (ArgumentException)
                            {
                            }
                        }
                    }
                }
            }

            return false;
        }

        int IVsRefactorNotify.OnAddParams(IVsHierarchy phier, uint itemId, string lpszRQName, uint cParams, uint[] rgszParamIndexes, string[] rgszRQTypeNames, string[] rgszParamNames)
        {
            if (null != RefactorNotifyImplementer)
                return RefactorNotifyImplementer.OnAddParams(phier, itemId, lpszRQName, cParams,
                    rgszParamIndexes, rgszRQTypeNames, rgszParamNames);

            return -2147418113;
        }

        int IVsRefactorNotify.OnBeforeAddParams(IVsHierarchy phier, uint itemId, string lpszRQName, uint cParams, uint[] rgszParamIndexes, string[] rgszRQTypeNames, string[] rgszParamNames, out Array prgAdditionalCheckoutVSITEMIDS)
        {
            if (null != RefactorNotifyImplementer)
                return RefactorNotifyImplementer.OnBeforeAddParams(phier, itemId, lpszRQName, cParams,
                    rgszParamIndexes, rgszRQTypeNames, rgszParamNames, out prgAdditionalCheckoutVSITEMIDS);

            prgAdditionalCheckoutVSITEMIDS = null;
            
            return -2147418113;
        }

        int IVsRefactorNotify.OnBeforeGlobalSymbolRenamed(IVsHierarchy phier, uint itemId, uint cRQNames, string[] rglpszRQName, string lpszNewName, out Array prgAdditionalCheckoutVSITEMIDS)
        {
            if (null != RefactorNotifyImplementer)
                return RefactorNotifyImplementer.OnBeforeGlobalSymbolRenamed(phier, itemId, cRQNames,
                    rglpszRQName, lpszNewName, out prgAdditionalCheckoutVSITEMIDS);

            prgAdditionalCheckoutVSITEMIDS = null;
            
            return -2147418113;
        }

        int IVsRefactorNotify.OnBeforeRemoveParams(IVsHierarchy phier, uint itemId, string lpszRQName, uint cParamIndexes, uint[] rgParamIndexes, out Array prgAdditionalCheckoutVSITEMIDS)
        {
            if (null != RefactorNotifyImplementer)
                return RefactorNotifyImplementer.OnBeforeRemoveParams(phier, itemId, lpszRQName,
                    cParamIndexes, rgParamIndexes, out prgAdditionalCheckoutVSITEMIDS);

            prgAdditionalCheckoutVSITEMIDS = null;
            
            return -2147418113;
        }

        int IVsRefactorNotify.OnBeforeReorderParams(IVsHierarchy phier, uint itemId, string lpszRQName, uint cParamIndexes, uint[] rgParamIndexes, out Array prgAdditionalCheckoutVSITEMIDS)
        {
            if (null != RefactorNotifyImplementer)
                return RefactorNotifyImplementer.OnBeforeReorderParams(phier, itemId, lpszRQName,
                    cParamIndexes, rgParamIndexes, out prgAdditionalCheckoutVSITEMIDS);

            prgAdditionalCheckoutVSITEMIDS = null;
            
            return -2147418113;
        }

        int IVsRefactorNotify.OnGlobalSymbolRenamed(IVsHierarchy phier, uint itemId, uint cRQNames, string[] rglpszRQName, string lpszNewName)
        {
            if (null != RefactorNotifyImplementer)
                return RefactorNotifyImplementer.OnGlobalSymbolRenamed(phier, itemId, cRQNames,
                    rglpszRQName, lpszNewName);

            return -2147418113;
        }

        int IVsRefactorNotify.OnRemoveParams(IVsHierarchy phier, uint itemId, string lpszRQName, uint cParamIndexes, uint[] rgParamIndexes)
        {
            if (null != RefactorNotifyImplementer)
                return RefactorNotifyImplementer.OnRemoveParams(phier, itemId, lpszRQName,
                    cParamIndexes, rgParamIndexes);

            return -2147418113;
        }

        int IVsRefactorNotify.OnReorderParams(IVsHierarchy phier, uint itemId, string lpszRQName, uint cParamIndexes, uint[] rgParamIndexes)
        {
            if (null != RefactorNotifyImplementer)
                return RefactorNotifyImplementer.OnReorderParams(phier, itemId, lpszRQName,
                    cParamIndexes, rgParamIndexes);

            return -2147418113;
        }

        private IVsRefactorNotify RefactorNotifyImplementer
        {
            get
            {
                if (null == _refactorNotifyImplementer)
                {
                    Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProviderInterface = Package.GetGlobalService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider)) as Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
                    if (null != serviceProviderInterface)
                    {
                        ServiceProvider serviceProvider = new ServiceProvider(serviceProviderInterface);
                        _refactorNotifyImplementer = serviceProvider.GetService(_resourceEditorRefactorNotifyServiceGuid) as IVsRefactorNotify;
                    }
                }

                return _refactorNotifyImplementer;
            }
        }

        #region COM Registration

		private static string GetVisualStugioRegistryKeyPath(VsNetVersion vsNetVersion, bool expressEdition)
		{
			StringBuilder registryKeyPathBuilder = new StringBuilder(100);

			FieldInfo fieldInfo = vsNetVersion.GetType().GetField(vsNetVersion.ToString());

			DescriptionAttribute descriptionAttribute = Attribute.GetCustomAttribute(fieldInfo,
				typeof(DescriptionAttribute)) as DescriptionAttribute;
			if (null == descriptionAttribute)
				throw new InvalidOperationException(string.Format("The Description attribute is not applied to the VsNetVersion.{0} enumeration value.",
					vsNetVersion.ToString()));

			registryKeyPathBuilder.AppendFormat(CultureInfo.InvariantCulture,
				expressEdition? @"SOFTWARE\Microsoft\VCSExpress\{0}" : @"SOFTWARE\Microsoft\VisualStudio\{0}",
				descriptionAttribute.Description);

			return registryKeyPathBuilder.ToString();
		}

		private static Guid GetLanguageGuid(LanguageSpecifier languageSpecifier)
		{
			FieldInfo fieldInfo = languageSpecifier.GetType().GetField(languageSpecifier.ToString());

			LanguageGuidAttribute languageGuidAttribute = Attribute.GetCustomAttribute(fieldInfo,
				typeof(LanguageGuidAttribute)) as LanguageGuidAttribute;
			if (null == languageGuidAttribute)
				throw new InvalidOperationException(string.Format("The LanguageGuid attribute is not applied to the LanguageSpecifier.{0} enumeration value.",
					languageSpecifier.ToString()));

			return languageGuidAttribute.Guid;
		}

		private static IEnumerable<Type> ExtendedGeneratorTypes
		{
			get
			{
				Type baseGeneratorType = typeof(BaseResXFileCodeGeneratorEx);

				Type[] types = baseGeneratorType.Assembly.GetTypes();
				foreach (Type type in types)
				{
					if (type.IsSubclassOf(baseGeneratorType))
						yield return type;
				}
			}
		}

		private static string GetTypeDescription(Type type)
		{
			if (null == type)
				throw new ArgumentNullException("type");

			DescriptionAttribute descriptionAttribute = Attribute.GetCustomAttribute(type,
				typeof(DescriptionAttribute)) as DescriptionAttribute;

			return (null == descriptionAttribute) ? string.Empty : descriptionAttribute.Description;
		}

		private static void ComRegisterGenerator(Type generatorType, string vsNetRegistryKeyPath)
		{
			using (RegistryKey vsNetRegistryKey = Registry.LocalMachine.OpenSubKey(vsNetRegistryKeyPath,
						RegistryKeyPermissionCheck.ReadWriteSubTree))
			{
				if (null == vsNetRegistryKey)
					return;

				LanguageSpecifier[] languageSpecifiers = (LanguageSpecifier[])Enum.GetValues(typeof(LanguageSpecifier));
				foreach (LanguageSpecifier languageSpecifier in languageSpecifiers)
				{
					string generatorRegistryKeyPath = string.Format(@"Generators\{0}\{1}",
						GetLanguageGuid(languageSpecifier).ToString("B"), generatorType.Name);

					using (RegistryKey generatorRegistryKey = vsNetRegistryKey.CreateSubKey(generatorRegistryKeyPath,
						RegistryKeyPermissionCheck.ReadWriteSubTree))
					{
						generatorRegistryKey.SetValue("", GetTypeDescription(generatorType));
						generatorRegistryKey.SetValue("CLSID", generatorType.GUID.ToString("B"));
						generatorRegistryKey.SetValue("GeneratesDesignTimeSource", 1);
					}
				}
			}
		}

        [ComRegisterFunction]
        public static void ComRegisterFunction(Type type)
        {
			VsNetVersion[] vsNetVersions = (VsNetVersion[])Enum.GetValues(typeof(VsNetVersion));
			foreach (Type generatorType in ExtendedGeneratorTypes)
			{
				foreach (VsNetVersion vsNetVersion in vsNetVersions)
				{
					// Register for Visual Studio
					ComRegisterGenerator(generatorType, GetVisualStugioRegistryKeyPath(vsNetVersion, false));

					// Register for Visual Studio Express
					ComRegisterGenerator(generatorType, GetVisualStugioRegistryKeyPath(vsNetVersion, true));
				}
			}
        }

        [ComUnregisterFunction]
        public static void ComUnregisterFunction(Type type)
        {
			VsNetVersion[] vsNetVersions = (VsNetVersion[])Enum.GetValues(typeof(VsNetVersion));
			foreach (Type generatorType in ExtendedGeneratorTypes)
			{
				foreach (VsNetVersion vsNetVersion in vsNetVersions)
				{
					LanguageSpecifier[] languageSpecifiers = (LanguageSpecifier[])Enum.GetValues(typeof(LanguageSpecifier));
					foreach (LanguageSpecifier languageSpecifier in languageSpecifiers)
					{
						// Unregister from Visual Studio
						string generatorRegistryKeyPath = string.Format(@"{0}\Generators\{1}\{2}",
							GetVisualStugioRegistryKeyPath(vsNetVersion, false),
							GetLanguageGuid(languageSpecifier).ToString("B"), generatorType.Name);

						Registry.LocalMachine.DeleteSubKey(generatorRegistryKeyPath, false);

						// Unregister from Visual Studio Express
						generatorRegistryKeyPath = string.Format(@"{0}\Generators\{1}\{2}",
							GetVisualStugioRegistryKeyPath(vsNetVersion, true),
							GetLanguageGuid(languageSpecifier).ToString("B"), generatorType.Name);

						Registry.LocalMachine.DeleteSubKey(generatorRegistryKeyPath, false);
					}
				}
			}
        }

        #endregion
    }
}

