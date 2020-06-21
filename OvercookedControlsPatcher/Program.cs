using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Assembly = System.Reflection.Assembly;

namespace OvercookedControlsPatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            string installDir = SearchInstallDir();
            
            while (installDir == null)
            {
                Console.WriteLine("Could not find Overcooked, please enter path to Overcooked.exe:");
                string path = Console.In.ReadLine();
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    Console.WriteLine("'"+path+"' does not exist");
                    continue;
                }
                string dir = Path.GetDirectoryName(path);
                string curDllPath = Path.Combine(dir, "Overcooked_Data", "Managed", "Assembly-CSharp.dll");
                if (File.Exists(curDllPath))
                {
                    installDir = dir;
                }
            }
            string dllPath = Path.Combine(installDir, "Overcooked_Data", "Managed", "Assembly-CSharp.dll");

            File.Copy(dllPath, dllPath + ".bak");

            PatchAssembly(dllPath);

            WriteResourceToDisk(installDir, "OvercookedControlsPatcher.default_controls.input_combined.txt", Path.Combine(installDir, "input_combined.txt"));
            WriteResourceToDisk(installDir, "OvercookedControlsPatcher.default_controls.input_split.txt", Path.Combine(installDir, "input_split.txt"));

            Console.WriteLine("Patch complete :)");
        }

        private static void WriteResourceToDisk(string installDir, string resName, string filename)
        {
            string[] names = typeof(Program).Assembly.GetManifestResourceNames();
            using (Stream input = typeof(Program).Assembly.GetManifestResourceStream(resName))
            using (Stream output = File.Create(Path.Combine(installDir, filename)))
            {
                input.CopyTo(output);
            }
        }

        private static string SearchInstallDir()
        {
            var curdir = Directory.GetCurrentDirectory();
            var exePath = Path.Combine(curdir, "Overcooked.exe");
            var dllPath = Path.Combine(curdir, "Overcooked_Data", "Managed", "Assembly-CSharp.dll");
            if (File.Exists(exePath) && File.Exists(dllPath))
            {
                return curdir;
            }

            return null;
        }

        private static void PatchAssembly(string file)
        {
            var asmSelf = AssemblyDefinition.ReadAssembly(Assembly.GetExecutingAssembly().Location);
            var patchType = asmSelf.MainModule.GetType(nameof(OvercookedControlsPatcher)+"."+nameof(PatchSource));

            var asmTarget = AssemblyDefinition.ReadAssembly(file, new ReaderParameters() { ReadWrite = true });

            foreach (var patchMethod in patchType.Methods)
            {
                var addMethodMeta = AddMethod.Read(patchMethod);
                var replaceMethodMeta = ReplaceMethod.Read(patchMethod);

                if (addMethodMeta != null)
                {
                    var targetType = asmTarget.MainModule.GetType(addMethodMeta.targetType);
                    PatchTools.CopyMethod(targetType, patchMethod);
                }
                else if (replaceMethodMeta != null)
                {
                    var targetType = asmTarget.MainModule.GetType(replaceMethodMeta.targetType);

                    // Not exactly right, but good enough for now.
                    var targetMethod = targetType.Methods.First(m =>
                        m.Name == patchMethod.Name && patchMethod.Parameters.Count == m.Parameters.Count);

                    PatchTools.ReplaceMethod(targetMethod, patchMethod);
                }
            }

            asmTarget.Write();
        }
    }
}
