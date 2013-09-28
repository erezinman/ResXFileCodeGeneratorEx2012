using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using VSLangProj80;

namespace DMKSoftware.CodeGenerators
{
    [ComVisible(true)]
    [Guid("FF2F2841-D6A2-42b5-9E14-86AD00A2917E")]
	[Description("Extended ResX Public File Code Generator")]
    //[CodeGeneratorRegistration(typeof(ResXFileCodeGeneratorEx), "ResXFileCodeGeneratorEx", VSConstants.UICONTEXT.CSharpProject_string, GeneratorRegKeyName = ".doml")]
    [CodeGeneratorRegistration(typeof(ResXFileCodeGeneratorEx), "ResXFileCodeGeneratorEx", VSConstants.UICONTEXT.CSharpProject_string, GeneratesDesignTimeSource = true)]
    [CodeGeneratorRegistration(typeof(ResXFileCodeGeneratorEx), "ResXFileCodeGeneratorEx", vsContextGuids.vsContextGuidVCSEditor, GeneratesDesignTimeSource = true)]
    [ProvideObject(typeof(ResXFileCodeGeneratorEx))]
	public class ResXFileCodeGeneratorEx : BaseResXFileCodeGeneratorEx
    {
		/// <summary>
		/// Initializes a new instance of the ResXFileCodeGeneratorEx class.
		/// </summary>
		public ResXFileCodeGeneratorEx()
		{
		}

		/// <summary>
		/// Gets the boolean flag indicating whether the internal class is generated.
		/// </summary>
		protected override bool GenerateInternalClass
		{
			get
			{
				return false;
			}
		}
    }
}

