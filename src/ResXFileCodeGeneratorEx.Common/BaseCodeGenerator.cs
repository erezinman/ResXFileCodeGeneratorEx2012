using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell.Interop;

namespace DMKSoftware.CodeGenerators
{
    public abstract class BaseCodeGenerator : IVsSingleFileGenerator
    {
        protected BaseCodeGenerator()
        {
            FileNameSpace = string.Empty;
            InputFilePath = string.Empty;
        }

        public IVsGeneratorProgress CodeGeneratorProgress { get; private set; }

        protected string FileNameSpace { get; private set; }

        protected string InputFilePath { get; private set; }

        public abstract int DefaultExtension(out string ext);

        public int Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace,
                            IntPtr[] pbstrOutputFileContents,
                            out uint pbstrOutputFileContentSize, IVsGeneratorProgress pGenerateProgress)
        {
            if (null == bstrInputFileContents) throw new ArgumentNullException("bstrInputFileContents");

            InputFilePath = wszInputFilePath;
            FileNameSpace = wszDefaultNamespace;
            CodeGeneratorProgress = pGenerateProgress;

            var codeBuffer = GenerateCode(wszInputFilePath, bstrInputFileContents);
            if (null == codeBuffer)
            {
                pbstrOutputFileContents[0] = IntPtr.Zero;
                pbstrOutputFileContentSize = 0;
            }
            else
            {
                pbstrOutputFileContents[0] = Marshal.AllocCoTaskMem(codeBuffer.Length);
                Marshal.Copy(codeBuffer, 0, pbstrOutputFileContents[0], codeBuffer.Length);
                pbstrOutputFileContentSize = (uint) codeBuffer.Length;
            }

            return 0;
        }

        protected abstract byte[] GenerateCode(string inputFileName, string inputFileContent);

        protected virtual void GeneratorErrorCallback(int warning, uint level, string message, uint line, uint column)
        {
            var vsGeneratorProgress = CodeGeneratorProgress;
            if (null != vsGeneratorProgress)
                NativeMethods.ThrowOnFailure(vsGeneratorProgress.GeneratorError(warning, level, message, line, column));
        }
    }
}